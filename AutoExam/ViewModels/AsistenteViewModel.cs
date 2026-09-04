using System.Collections.ObjectModel;
using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>Un paso del asistente. El resumen repite lo ya elegido, no el enunciado del paso.</summary>
public partial class PasoAsistente : ObservableObject
{
    public PasoAsistente(int numero, string titulo)
    {
        Numero = numero;
        Titulo = titulo;
    }

    public int Numero { get; }

    public string Titulo { get; }

    [ObservableProperty]
    private string _resumen = string.Empty;

    [ObservableProperty]
    private bool _esActual;

    [ObservableProperty]
    private bool _completado;
}

/// <summary>
/// Asistente de 3 pasos para armar un examen: material, alcance y formato.
/// Partirlo en pasos no es decoracion: cada paso depende del anterior (sin libro
/// no hay modulos, sin alcance no se puede estimar el examen).
/// </summary>
public partial class AsistenteViewModel : PaginaViewModel
{
    private const int PrimerPaso = 1;
    private const int UltimoPaso = 3;

    private readonly BibliotecaService _biblioteca;
    private readonly PdfExtractorService _pdf;
    private readonly GeminiApiService _gemini;
    private readonly SesionUsuarioService _sesion;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _nav;
    private readonly Random _random = new();

    private CancellationTokenSource? _cts;

    /// <summary>Lo levanta el shell para pasarle el examen recien armado a la pagina de examen.</summary>
    public event Action<ExamenEnCurso>? ExamenGenerado;

    /// <summary>Lo consulta antes de pisar un examen a medio rendir.</summary>
    public Func<bool>? HayExamenSinTerminar { get; set; }

    public AsistenteViewModel(
        BibliotecaService biblioteca,
        PdfExtractorService pdf,
        GeminiApiService gemini,
        SesionUsuarioService sesion,
        IDialogos dialogos,
        INavegacion nav)
        : base("nuevo", "Nuevo examen", "Sparkle24")
    {
        _biblioteca = biblioteca;
        _pdf = pdf;
        _gemini = gemini;
        _sesion = sesion;
        _dialogos = dialogos;
        _nav = nav;

        Pasos = new ObservableCollection<PasoAsistente>
        {
            new(1, "Material"),
            new(2, "Alcance"),
            new(3, "Formato")
        };

        _biblioteca.Libros.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HayLibros));

        // Si la generacion tuvo que cambiar de modelo porque el guardado ya no existe, la
        // correccion se persiste: si no, cada examen volveria a descubrir lo mismo y a
        // pagar la consulta de modelos para averiguarlo.
        _gemini.ModeloCorregido += modelo =>
        {
            _sesion.Config.Modelo = modelo;
            _sesion.GuardarConfig();
            _nav.RefrescarEstadoApi();
        };

        RefrescarPasos();
    }

    public ObservableCollection<PasoAsistente> Pasos { get; }

    public ObservableCollection<Libro> Libros => _biblioteca.Libros;

    /// <summary>Materias existentes, para filtrar los documentos del paso Material.</summary>
    public ObservableCollection<Materia> Materias => _biblioteca.Materias;

    // ------------------------------------------------------------------
    // US-026 / RN-29 — armar el examen con examenes anteriores
    //
    // El punto de entrada principal es este, y no el Historial: el alumno que quiere rendir
    // algo entra por "Nuevo examen", y ahi tiene que estar la opcion sin importar por donde
    // haya llegado. El Historial sigue teniendo su atajo al mismo flujo.
    // ------------------------------------------------------------------

    /// <summary>
    /// De donde salen las preguntas. Es lo primero que se elige en el paso Material.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModoMaterial))]
    [NotifyPropertyChangedFor(nameof(ModoRepaso))]
    [NotifyPropertyChangedFor(nameof(ModoCombinado))]
    [NotifyPropertyChangedFor(nameof(ModoFalladas))]
    [NotifyPropertyChangedFor(nameof(ModoImportado))]
    [NotifyPropertyChangedFor(nameof(AvisoDeGeneracion))]
    [NotifyPropertyChangedFor(nameof(ResumenSeleccion))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerarCommand))]
    private OrigenPreguntas _origen = OrigenPreguntas.Material;

    /// <summary>true cuando se generan preguntas nuevas con IA a partir de material propio.</summary>
    public bool ModoMaterial => Origen == OrigenPreguntas.Material;

    /// <summary>
    /// true en los tres modos que NO generan con IA: combinado (US-026), preguntas falladas
    /// (US-032) y examen importado (US-037).
    ///
    /// El nombre viene de US-026, cuando era el unico. Se conserva porque lo que significa no
    /// cambio y es lo que gobierna todo lo que esos tres modos comparten: las preguntas ya
    /// existen, asi que el paso Alcance no tiene nada que preguntar, no hace falta clave de
    /// Gemini y el armado es instantaneo.
    /// </summary>
    public bool ModoRepaso => Origen != OrigenPreguntas.Material;

    public bool ModoCombinado => Origen == OrigenPreguntas.ExamenesAnteriores;

    public bool ModoFalladas => Origen == OrigenPreguntas.PreguntasFalladas;

    public bool ModoImportado => Origen == OrigenPreguntas.Importado;

    /// <summary>Examenes rendidos que pueden aportar preguntas a un repaso.</summary>
    public ObservableCollection<ExamenRendido> ExamenesParaRepaso { get; } = new();

    /// <summary>
    /// Texto del buscador de examenes. La lista puede tener cientos de intentos: sin filtro,
    /// elegir dos de una materia concreta seria bajar por una lista larguisima.
    /// </summary>
    [ObservableProperty]
    private string _filtroExamenes = string.Empty;

    partial void OnFiltroExamenesChanged(string value) => PoblarExamenesParaRepaso();

    partial void OnOrigenChanged(OrigenPreguntas value)
    {
        switch (value)
        {
            case OrigenPreguntas.ExamenesAnteriores:
                PoblarExamenesParaRepaso();
                break;

            case OrigenPreguntas.PreguntasFalladas:
                PoblarFocosDeRepaso();
                break;

            case OrigenPreguntas.Importado:
                PoblarExamenesImportados();
                break;
        }

        // Cambiar de modo reinicia el paso: los pasos de un modo no aplican al otro.
        Paso = PrimerPaso;
        Recalcular();
    }

    private void PoblarExamenesParaRepaso()
    {
        ExamenesParaRepaso.Clear();

        string filtro = FiltroExamenes.Trim();

        foreach (var examen in _sesion.Historial.Where(e => e.PuedeAlimentarRepaso))
        {
            if (filtro.Length > 0 &&
                !examen.TituloTexto.Contains(filtro, StringComparison.OrdinalIgnoreCase) &&
                !examen.Materia.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ExamenesParaRepaso.Add(examen);
        }

        OnPropertyChanged(nameof(HayExamenesParaRepaso));
        RecalcularSeleccion();
    }

    /// <summary>
    /// true si hay al menos dos examenes que puedan aportar preguntas. Con menos, el modo se
    /// ofrece igual pero explicando por que todavia no se puede usar: esconderlo dejaria al
    /// alumno sin saber que la funcion existe.
    /// </summary>
    public bool HayExamenesParaRepaso => _sesion.Historial.Count(e => e.PuedeAlimentarRepaso) >= 2;

    public IReadOnlyList<ExamenRendido> ExamenesElegidos =>
        ExamenesParaRepaso.Where(e => e.Seleccionado).ToList();

    public int PreguntasDisponiblesParaRepaso =>
        CombinadorDeExamenes.ContarDisponibles(ExamenesElegidos);

    public string ResumenRepaso
    {
        get
        {
            if (!HayExamenesParaRepaso)
            {
                return "Todavia no tenes dos examenes rendidos con detalle guardado. " +
                       "Rendi un par de examenes y despues vas a poder combinarlos.";
            }

            int elegidos = ExamenesElegidos.Count;

            if (elegidos < 2)
            {
                return "Tilda dos o mas examenes para combinar sus preguntas.";
            }

            int disponibles = PreguntasDisponiblesParaRepaso;

            string aviso = Cantidad > disponibles
                ? $" Solo hay {disponibles}, asi que el examen va a salir con esas."
                : string.Empty;

            return $"{elegidos} examenes tildados · {disponibles} preguntas para elegir.{aviso}";
        }
    }

    [RelayCommand]
    private void UsarMaterial() => Origen = OrigenPreguntas.Material;

    [RelayCommand]
    private void UsarExamenesAnteriores() => Origen = OrigenPreguntas.ExamenesAnteriores;

    [RelayCommand]
    private void UsarPreguntasFalladas() => Origen = OrigenPreguntas.PreguntasFalladas;

    [RelayCommand]
    private void UsarExamenImportado() => Origen = OrigenPreguntas.Importado;

    // ------------------------------------------------------------------
    // US-032 — repaso inteligente: solo lo que vengo fallando
    // ------------------------------------------------------------------

    /// <summary>Materias y documentos que hoy tienen preguntas falladas para repasar.</summary>
    public ObservableCollection<FocoDeRepaso> FocosDeRepaso { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenFalladas))]
    [NotifyPropertyChangedFor(nameof(PreguntasFalladasDisponibles))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerarCommand))]
    private FocoDeRepaso? _focoElegido;

    public bool HayPreguntasFalladas => FocosDeRepaso.Count > 0;

    public int PreguntasFalladasDisponibles => FocoElegido?.Falladas ?? 0;

    public string ResumenFalladas
    {
        get
        {
            if (!HayPreguntasFalladas)
            {
                return "No tenés preguntas falladas para repasar. Puede ser que todavía no hayas " +
                       "rendido nada con detalle guardado, o que las hayas acertado todas.";
            }

            if (FocoElegido is null)
            {
                return "Elegí una materia o un documento para repasar lo que fallaste ahí.";
            }

            int disponibles = PreguntasFalladasDisponibles;

            string aviso = Cantidad > disponibles
                ? $" Solo hay {disponibles}, así que el examen va a salir con esas."
                : string.Empty;

            return $"{disponibles} preguntas falladas en {FocoElegido.Nombre}.{aviso}";
        }
    }

    private void PoblarFocosDeRepaso()
    {
        string? claveAnterior = FocoElegido?.Clave;

        FocosDeRepaso.Clear();

        foreach (var foco in RepasoInteligente.Focos(_sesion.Historial))
        {
            FocosDeRepaso.Add(foco);
        }

        // Se repone lo que estaba elegido si sigue existiendo: volver del paso Formato no
        // tiene por que perder la eleccion.
        FocoElegido = FocosDeRepaso.FirstOrDefault(f => f.Clave == claveAnterior) ?? FocosDeRepaso.FirstOrDefault();

        OnPropertyChanged(nameof(HayPreguntasFalladas));
        OnPropertyChanged(nameof(ResumenFalladas));
    }

    [RelayCommand]
    private void ElegirFoco(FocoDeRepaso? foco)
    {
        if (foco is not null)
        {
            FocoElegido = foco;
        }
    }

    // ------------------------------------------------------------------
    // US-037 — examenes que me compartieron
    // ------------------------------------------------------------------

    /// <summary>Examenes compartidos ya importados, guardados en disco para rendirlos cuando sea.</summary>
    public ObservableCollection<ExamenImportado> ExamenesImportados { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenImportado))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerarCommand))]
    private ExamenImportado? _importadoElegido;

    public bool HayExamenesImportados => ExamenesImportados.Count > 0;

    public string ResumenImportado
    {
        get
        {
            if (!HayExamenesImportados)
            {
                return "Todavía no importaste ningún examen. Pedile a un compañero que exporte uno " +
                       "desde su AutoExam y tocá \"Importar un examen\".";
            }

            if (ImportadoElegido is null)
            {
                return "Elegí cuál de los exámenes importados querés rendir.";
            }

            return $"{ImportadoElegido.Preguntas} preguntas · {ImportadoElegido.Materia}";
        }
    }

    private void PoblarExamenesImportados()
    {
        string? rutaAnterior = ImportadoElegido?.Ruta;

        ExamenesImportados.Clear();

        foreach (var examen in BibliotecaDeCompartidos.Listar())
        {
            ExamenesImportados.Add(examen);
        }

        ImportadoElegido = ExamenesImportados.FirstOrDefault(e => e.Ruta == rutaAnterior)
                           ?? ExamenesImportados.FirstOrDefault();

        OnPropertyChanged(nameof(HayExamenesImportados));
        OnPropertyChanged(nameof(ResumenImportado));
    }

    [RelayCommand]
    private void ElegirImportado(ExamenImportado? examen)
    {
        if (examen is not null)
        {
            ImportadoElegido = examen;
        }
    }

    /// <summary>
    /// Importa un archivo compartido y lo guarda para poder rendirlo cuando el alumno quiera
    /// (US-037). Un archivo invalido se rechaza con el motivo y no deja nada a medias.
    /// </summary>
    [RelayCommand]
    private void ImportarExamen()
    {
        string? ruta = _dialogos.ElegirExamenCompartido();

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        var resultado = CompartirExamenService.Leer(ruta);

        if (!resultado.Ok)
        {
            _dialogos.Error("No se pudo importar el examen", resultado.Error ?? "El archivo no es válido.");
            return;
        }

        try
        {
            var guardado = BibliotecaDeCompartidos.Guardar(resultado.Examen!, ruta);

            PoblarExamenesImportados();
            ImportadoElegido = ExamenesImportados.FirstOrDefault(e => e.Ruta == guardado.Ruta) ?? ImportadoElegido;

            Avisar($"Listo: \"{guardado.Titulo}\", {guardado.Preguntas} preguntas. Ya podés rendirlo.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Asistente.ImportarExamen", ex);
            _dialogos.Error("No se pudo importar el examen", ex.Message);
        }
    }

    [RelayCommand]
    private void QuitarImportado(ExamenImportado? examen)
    {
        if (examen is null ||
            !_dialogos.Confirmar($"¿Sacar \"{examen.Titulo}\" de tus exámenes importados?"))
        {
            return;
        }

        BibliotecaDeCompartidos.Borrar(examen);
        PoblarExamenesImportados();
    }

    // ------------------------------------------------------------------
    // US-034 — cronometro
    //
    // RN-43: vive en el paso Formato y no en un modo puntual, asi que aplica igual a un
    // examen generado con IA, a uno combinado, a un repaso de lo fallado y a uno importado.
    // ------------------------------------------------------------------

    /// <summary>Minutos de tiempo total. 0 = sin limite, que es el modo de siempre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConCronometro))]
    [NotifyPropertyChangedFor(nameof(ResumenTiempo))]
    private int _minutosLimite;

    partial void OnMinutosLimiteChanged(int value) => Recalcular();

    public bool ConCronometro => MinutosLimite > 0;

    /// <summary>Opciones de tiempo. El 0 es "sin limite" y es el que viene elegido.</summary>
    public IReadOnlyList<int> PresetsTiempo { get; } = new[] { 0, 20, 40, 60, 90 };

    public string ResumenTiempo => ConCronometro
        ? $"{MinutosLimite} minutos para todo el examen"
        : "Sin límite de tiempo";

    /// <summary>
    /// Que va a pasar al tocar "Generar examen", en una linea.
    ///
    /// Depende del origen y no es un texto fijo porque solo uno de los cuatro modos habla con
    /// Gemini. El tooltip decia "le pide las preguntas a Gemini... consume cuota" tambien en
    /// los tres modos locales, que es exactamente lo contrario de lo que hacen y de lo que la
    /// historia promete.
    /// </summary>
    public string AvisoDeGeneracion => Origen switch
    {
        OrigenPreguntas.Material =>
            "Le pide las preguntas a Gemini con el material y el alcance elegidos. Puede tardar y consume cuota.",
        OrigenPreguntas.ExamenesAnteriores =>
            "Mezcla preguntas de los examenes tildados. Es instantaneo y no gasta cuota de Gemini.",
        OrigenPreguntas.PreguntasFalladas =>
            "Arma el examen con lo que venis fallando. Es instantaneo y no gasta cuota de Gemini.",
        _ =>
            "Abre el examen que te compartieron. Es instantaneo y no gasta cuota de Gemini."
    };

    [RelayCommand]
    private void ElegirTiempo(string? minutos)
    {
        if (int.TryParse(minutos, out int valor))
        {
            MinutosLimite = Math.Max(0, valor);
        }
    }

    /// <summary>
    /// Entra en modo repaso desde afuera. Lo usa el atajo del Historial (RN-29): tildar alla
    /// y tocar "Armar repaso" abre este asistente ya en el modo correcto, con lo tildado
    /// puesto — la seleccion vive en los propios ExamenRendido, que son las mismas
    /// instancias que las dos pantallas listan.
    /// </summary>
    public void EntrarEnModoRepaso()
    {
        Origen = OrigenPreguntas.ExamenesAnteriores;
        PoblarExamenesParaRepaso();
    }

    /// <summary>
    /// Deja el asistente en el paso Material y en el modo de material nuevo. Lo usa el atajo
    /// "Generar examen" del menu principal (US-031), cuyo criterio pide que lleve "directo al
    /// asistente de Nuevo examen, paso Material".
    ///
    /// Es necesario porque el asistente conserva su estado entre visitas: quien entro antes,
    /// avanzo hasta Formato y volvio al menu, al tocar el atajo caeria de nuevo en Formato.
    /// Solo se reponen paso y modo; el libro y el alcance ya elegidos se respetan, que es lo
    /// que uno espera al volver a una pantalla en la que ya venia trabajando.
    /// </summary>
    public void EmpezarDesdeCero()
    {
        Origen = OrigenPreguntas.Material;
        Paso = PrimerPaso;
    }

    [RelayCommand]
    private void DestildarExamenes()
    {
        foreach (var examen in _sesion.Historial)
        {
            examen.Seleccionado = false;
        }

        RecalcularSeleccion();
    }

    /// <summary>
    /// Documentos de la materia elegida. Es la lista que se muestra en el paso "Material":
    /// filtrarla es lo que hace que RN-23 se cumpla por construccion, porque nunca llegan a
    /// verse juntos dos documentos de materias distintas para poder tildarlos.
    /// </summary>
    public ObservableCollection<Libro> LibrosDeLaMateria { get; } = new();

    public ObservableCollection<Modulo> Modulos { get; } = new();

    public bool HayLibros => _biblioteca.Libros.Count > 0;

    public bool HayModulos => Modulos.Count > 0;

    public string ResumenCapitulos
    {
        get
        {
            int marcados = Modulos.Count(m => m.Seleccionado);
            return marcados == 0
                ? $"{Modulos.Count} disponibles"
                : $"{marcados} de {Modulos.Count} · {Modulos.Where(m => m.Seleccionado).Sum(m => m.CantidadPaginas)} pag.";
        }
    }

    // ------------------------------------------------------------------
    // Estado del asistente
    // ------------------------------------------------------------------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsPrimerPaso))]
    [NotifyPropertyChangedFor(nameof(EsUltimoPaso))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnteriorCommand))]
    private int _paso = PrimerPaso;

    public bool EsPrimerPaso => Paso == PrimerPaso;

    public bool EsUltimoPaso => Paso == UltimoPaso;

    // ------------------------------------------------------------------
    // Paso 1: material
    // ------------------------------------------------------------------
    /// <summary>
    /// Materia elegida: acota que documentos se ofrecen para tildar (US-024) y con eso
    /// garantiza RN-23. Vacia significa "todavia no elegi": se muestran todas.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMateriaElegida))]
    private string? _materiaElegida;

    public bool HayMateriaElegida => !string.IsNullOrWhiteSpace(MateriaElegida);

    /// <summary>
    /// Documento en foco. Con varios tildados sigue existiendo: es el que manda en el paso
    /// Alcance, porque los capitulos y el rango de paginas son de un documento concreto
    /// (US-024: "acotar por documento individualmente antes de combinar").
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayLibroElegido))]
    [NotifyPropertyChangedFor(nameof(EsFuentePdf))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerarCommand))]
    private Libro? _libro;

    public bool HayLibroElegido => Libro is not null;

    /// <summary>
    /// Documentos que entran en el examen: los tildados, o el que este en foco si no se
    /// tildo ninguno. Ese respaldo es lo que hace que el flujo de siempre —elegir un
    /// material de la lista y generar— siga funcionando sin tener que tildar nada.
    /// </summary>
    public IReadOnlyList<Libro> Seleccionados
    {
        get
        {
            var tildados = LibrosDeLaMateria.Where(l => l.Seleccionado).ToList();

            if (tildados.Count > 0)
            {
                return tildados;
            }

            return Libro is null ? Array.Empty<Libro>() : new[] { Libro };
        }
    }

    /// <summary>true cuando el examen va a combinar mas de un documento (US-024).</summary>
    public bool EsExamenCombinado => Seleccionados.Count > 1;

    public string ResumenSeleccion
    {
        get
        {
            var elegidos = Seleccionados;

            return elegidos.Count switch
            {
                0 => "Sin elegir",
                1 => elegidos[0].Titulo,
                _ => $"{elegidos.Count} documentos de {elegidos[0].Materia}"
            };
        }
    }

    /// <summary>
    /// true solo para PDF: es el unico formato con paginas/capitulos, asi que el paso
    /// Alcance esconde modulos y rango para el resto (US-009 AC-T45, arquitectura Inc-4 §3).
    /// </summary>
    public bool EsFuentePdf => Libro?.Tipo == TipoFuente.Pdf;

    [ObservableProperty]
    private bool _agregando;

    // ------------------------------------------------------------------
    // Paso 2: alcance
    // ------------------------------------------------------------------
    /// <summary>0 = todo · 1 = primeras · 2 = ultimas · 3 = a mano.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangoAMano))]
    private int _presetRango;

    public bool RangoAMano => PresetRango == 3;

    [ObservableProperty]
    private int _desde = 1;

    [ObservableProperty]
    private int _hasta = 20;

    [ObservableProperty]
    private string _tema = string.Empty;

    // ------------------------------------------------------------------
    // Paso 3: formato
    // ------------------------------------------------------------------
    /// <summary>10, 30, 60 o -1 cuando el usuario escribe su propio numero.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CantidadAMano))]
    private int _presetCantidad = 10;

    public bool CantidadAMano => PresetCantidad == -1;

    [ObservableProperty]
    private int _cantidadPersonalizada = 15;

    [ObservableProperty]
    private bool _incluirImagenes = true;

    // ------------------------------------------------------------------
    // Generacion
    // ------------------------------------------------------------------
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerarCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelarCommand))]
    private bool _generando;

    [ObservableProperty]
    private string _progreso = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    private string _mensaje = string.Empty;

    /// <summary>0 = info · 1 = ok · 2 = aviso · 3 = error.</summary>
    [ObservableProperty]
    private int _severidad = 2;

    public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

    public int Cantidad => CantidadAMano
        ? Math.Clamp(CantidadPersonalizada, 1, 120)
        : PresetCantidad;

    public int PaginasDelAlcance => ConstruirRangos(out _).Sum(r => Math.Abs(r.Hasta - r.Desde) + 1);

    public string ResumenAlcance
    {
        get
        {
            if (Libro is null)
            {
                return "Elegi un material para empezar.";
            }

            if (EsExamenCombinado)
            {
                // Con varios documentos el alcance se describe por documento (cada uno con
                // sus modulos marcados), asi que resumirlo en una sola linea de paginas
                // seria enganioso. El eje tematico si aplica a todos por igual.
                string eje = Tema.Trim();
                string cuantos = $"{Seleccionados.Count} documentos combinados";
                return eje.Length > 0 ? $"{cuantos} · tema \"{eje}\"" : cuantos;
            }

            if (!EsFuentePdf)
            {
                string t = Tema.Trim();
                return t.Length > 0 ? $"material completo · tema \"{t}\"" : "material completo";
            }

            ConstruirRangos(out string descripcion);
            return descripcion;
        }
    }

    // ------------------------------------------------------------------
    // Reacciones a los cambios
    // ------------------------------------------------------------------
    partial void OnMateriaElegidaChanged(string? value)
    {
        // Cambiar de materia limpia lo tildado. Conservarlo dejaria documentos marcados que
        // ya no se ven en pantalla, y el examen saldria combinando materias distintas
        // contra RN-23 sin que el alumno pueda darse cuenta.
        foreach (var libro in _biblioteca.Libros)
        {
            libro.Seleccionado = false;

            // Tambien el recorte por documento: si no, un capitulo marcado en una materia
            // que ya no se ve seguiria acotando un examen futuro sin que se note.
            foreach (var modulo in libro.Modulos)
            {
                modulo.Seleccionado = false;
            }
        }

        PoblarLibrosDeLaMateria();

        if (Libro is null || !LibrosDeLaMateria.Contains(Libro))
        {
            Libro = LibrosDeLaMateria.FirstOrDefault();
        }

        Recalcular();
    }

    private void PoblarLibrosDeLaMateria()
    {
        LibrosDeLaMateria.Clear();

        var fuente = string.IsNullOrWhiteSpace(MateriaElegida)
            ? _biblioteca.Libros.AsEnumerable()
            : _biblioteca.LibrosDe(MateriaElegida!);

        foreach (var libro in fuente)
        {
            LibrosDeLaMateria.Add(libro);
        }

        OnPropertyChanged(nameof(HayLibros));
    }

    /// <summary>La llama la vista al tildar o destildar un documento del paso Material.</summary>
    public void RecalcularSeleccion()
    {
        OnPropertyChanged(nameof(Seleccionados));
        OnPropertyChanged(nameof(EsExamenCombinado));
        OnPropertyChanged(nameof(ResumenSeleccion));
        OnPropertyChanged(nameof(ExamenesElegidos));
        OnPropertyChanged(nameof(PreguntasDisponiblesParaRepaso));
        OnPropertyChanged(nameof(ResumenRepaso));
        GenerarCommand.NotifyCanExecuteChanged();
        SiguienteCommand.NotifyCanExecuteChanged();
        Recalcular();
    }

    partial void OnLibroChanged(Libro? value)
    {
        Modulos.Clear();

        if (value is not null)
        {
            // Los capitulos marcados se conservan si el documento esta tildado para entrar
            // al examen: son su recorte propio (US-024, "acotar por documento
            // individualmente"), y borrarlos al mirar otro documento y volver haria imposible
            // acotar mas de uno. En un documento que solo se esta mirando se limpian, que es
            // el comportamiento de siempre.
            bool conservarMarcas = value.Seleccionado;

            foreach (var m in value.Modulos.OrderBy(m => m.DesdePagina))
            {
                if (!conservarMarcas)
                {
                    m.Seleccionado = false;
                }

                Modulos.Add(m);
            }

            Desde = 1;
            Hasta = Math.Min(50, Math.Max(1, value.CantidadPaginas));
        }

        PresetRango = 0;
        Recalcular();
    }

    partial void OnPresetRangoChanged(int value)
    {
        if (Libro is Libro libro)
        {
            int total = Math.Max(1, libro.CantidadPaginas);

            // Los presets escriben los mismos campos que el modo manual: al pasar
            // a "a mano" el usuario encuentra el rango ya cargado, no un formulario vacio.
            switch (value)
            {
                case 1:
                    Desde = 1;
                    Hasta = Math.Min(50, total);
                    break;
                case 2:
                    Desde = Math.Max(1, total - 49);
                    Hasta = total;
                    break;
            }
        }

        Recalcular();
    }

    partial void OnDesdeChanged(int value) => Recalcular();

    partial void OnHastaChanged(int value) => Recalcular();

    partial void OnTemaChanged(string value) => Recalcular();

    partial void OnPresetCantidadChanged(int value) => Recalcular();

    partial void OnCantidadPersonalizadaChanged(int value) => Recalcular();

    partial void OnIncluirImagenesChanged(bool value) => Recalcular();

    /// <summary>La llama la vista cuando se marca o desmarca un modulo.</summary>
    public void Recalcular()
    {
        OnPropertyChanged(nameof(Cantidad));
        OnPropertyChanged(nameof(PaginasDelAlcance));
        OnPropertyChanged(nameof(ResumenAlcance));
        OnPropertyChanged(nameof(HayModulos));
        OnPropertyChanged(nameof(ResumenCapitulos));
        OnPropertyChanged(nameof(Seleccionados));
        OnPropertyChanged(nameof(EsExamenCombinado));
        OnPropertyChanged(nameof(ResumenSeleccion));
        RefrescarPasos();
    }

    /// <summary>
    /// Trae los capitulos del indice del PDF sin obligar a pasar por la pagina Libros:
    /// el usuario esta armando el examen, mandarlo a otra pantalla lo hace perder el hilo.
    /// </summary>
    [RelayCommand]
    private async Task DetectarCapitulosAsync()
    {
        if (Libro is not Libro libro || !libro.ArchivoDisponible)
        {
            return;
        }

        try
        {
            var capitulos = await _pdf.DetectarCapitulosAsync(libro.RutaArchivo);

            if (capitulos.Count == 0)
            {
                Avisar(
                    "Este PDF no trae indice interno, asi que no hay capitulos que leer. " +
                    "Elegi por paginas, o dividilo en partes iguales desde Libros.");
                return;
            }

            libro.Modulos = capitulos
                .Select(c => new Modulo { Nombre = c.Titulo, DesdePagina = c.Desde, HastaPagina = c.Hasta })
                .ToList();

            _biblioteca.Guardar();

            Modulos.Clear();
            foreach (var m in libro.Modulos)
            {
                m.Seleccionado = false;
                Modulos.Add(m);
            }

            Recalcular();
            _nav.Estado($"Se detectaron {capitulos.Count} capitulos.");
            Avisar($"Listos {capitulos.Count} capitulos. Toca los que quieras incluir.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Asistente.DetectarCapitulos", ex);
            Avisar($"No se pudo leer el indice del PDF: {ex.Message}", error: true);
        }
    }

    private void RefrescarPasos()
    {
        if (ModoRepaso)
        {
            // En los modos locales el riel muestra dos pasos utiles: el Alcance no aplica,
            // porque no hay material del que recortar capitulos ni paginas.
            Pasos[0].Resumen = Origen switch
            {
                OrigenPreguntas.PreguntasFalladas => FocoElegido is null
                    ? "Sin elegir"
                    : $"{FocoElegido.Nombre} · {FocoElegido.Falladas} falladas",
                OrigenPreguntas.Importado => ImportadoElegido?.Titulo ?? "Sin elegir",
                _ => ExamenesElegidos.Count == 0 ? "Sin elegir" : $"{ExamenesElegidos.Count} examenes"
            };
            Pasos[1].Resumen = "No aplica";
        }
        else
        {
            Pasos[0].Resumen = EsExamenCombinado
                ? ResumenSeleccion
                : Libro is null
                    ? "Sin elegir"
                    : EsFuentePdf ? $"{Libro.Titulo} · {Libro.CantidadPaginas} pag." : $"{Libro.Titulo} · {Libro.MedidaTamanio}";
            Pasos[1].Resumen = Libro is null
                ? "-"
                : EsFuentePdf && !EsExamenCombinado ? $"{ResumenAlcance} · ~{PaginasDelAlcance} pag." : ResumenAlcance;
        }

        // US-034: el tiempo entra en el resumen del paso Formato porque es una decision del
        // formato, no del origen — y porque es lo que uno quiere ver antes de generar.
        string reloj = ConCronometro ? $" · {MinutosLimite} min" : string.Empty;

        Pasos[2].Resumen = ModoRepaso
            ? (ModoImportado ? $"{ImportadoElegido?.Preguntas ?? 0} preguntas" : $"{Cantidad} preguntas mezcladas") + reloj
            : $"{Cantidad} preguntas{(IncluirImagenes ? " · con graficos" : string.Empty)}{reloj}";

        foreach (var p in Pasos)
        {
            p.EsActual = p.Numero == Paso;
            p.Completado = p.Numero < Paso;
        }
    }

    partial void OnPasoChanged(int value) => RefrescarPasos();

    // ------------------------------------------------------------------
    // Navegacion del asistente
    // ------------------------------------------------------------------
    private bool PuedeAvanzar() => Paso switch
    {
        1 => HayConQueArmar,
        2 => true,
        _ => false
    };

    /// <summary>
    /// Si el paso Material quedo resuelto. Cada origen tiene su propia condicion, y esta es la
    /// unica definicion: la usan igual el boton de Siguiente y el de Generar, asi que no puede
    /// pasar que uno se habilite y el otro no.
    /// </summary>
    private bool HayConQueArmar => Origen switch
    {
        OrigenPreguntas.Material => Seleccionados.Count > 0,
        OrigenPreguntas.ExamenesAnteriores => ExamenesElegidos.Count >= 2,
        OrigenPreguntas.PreguntasFalladas => FocoElegido is not null && FocoElegido.Falladas > 0,
        OrigenPreguntas.Importado => ImportadoElegido is not null,
        _ => false
    };

    /// <summary>
    /// Avanza un paso. En modo repaso salta del Material al Formato: el paso Alcance define
    /// capitulos y rango de paginas del material, y un repaso no lee material —usa preguntas
    /// que ya existen—, asi que ese paso no tendria nada que preguntar (US-026).
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeAvanzar))]
    private void Siguiente() => Paso = ModoRepaso && Paso == PrimerPaso
        ? UltimoPaso
        : Math.Min(UltimoPaso, Paso + 1);

    /// <summary>Vuelve un paso. En modo repaso el camino de vuelta salta el Alcance igual que el de ida.</summary>
    [RelayCommand(CanExecute = nameof(EsPrimerPasoNo))]
    private void Anterior() => Paso = ModoRepaso && Paso == UltimoPaso
        ? PrimerPaso
        : Math.Max(PrimerPaso, Paso - 1);

    private bool EsPrimerPasoNo() => Paso > PrimerPaso;

    /// <summary>Solo deja volver a pasos ya visitados: saltear adelante rompe la dependencia entre pasos.</summary>
    [RelayCommand]
    private void IrAPaso(int numero)
    {
        // En repaso el paso Alcance no existe: tocarlo en el riel no tiene que llevar a una
        // pantalla que no aplica a este modo (US-026).
        if (ModoRepaso && numero == 2)
        {
            return;
        }

        if (numero >= PrimerPaso && numero <= Paso)
        {
            Paso = numero;
        }
    }

    /// <summary>
    /// Filtra los documentos por materia (US-024). Volver a tocar la elegida la
    /// deselecciona y se ven todos otra vez.
    /// </summary>
    [RelayCommand]
    private void ElegirMateria(string? materia)
    {
        MateriaElegida = string.Equals(MateriaElegida, materia, StringComparison.OrdinalIgnoreCase)
            ? null
            : materia;
    }

    [RelayCommand]
    private void MarcarTodos() => MarcarModulos(true);

    [RelayCommand]
    private void DesmarcarTodos() => MarcarModulos(false);

    private void MarcarModulos(bool valor)
    {
        foreach (var m in Modulos)
        {
            m.Seleccionado = valor;
        }

        Recalcular();
    }

    /// <summary>
    /// Alta de una fuente desde la zona de arrastre o el selector. Multi-archivo: varias
    /// imagenes = un unico material (arquitectura Inc-4 §3). Al arrastrar puede venir
    /// cualquier extension (el behavior no filtra): las no admitidas se descartan aca con
    /// un aviso que nombra los formatos validos (NFR-37), igual que hace el selector.
    /// "No se combinan tipos" y "un examen = una fuente" los valida
    /// <see cref="BibliotecaService.AgregarFuenteAsync"/> (lanza
    /// <see cref="FuenteInvalidaException"/>), aca solo se traduce a aviso.
    /// </summary>
    [RelayCommand]
    private async Task SoltarAsync(string[]? rutas)
    {
        if (rutas is null || rutas.Length == 0)
        {
            return;
        }

        var admitidas = rutas.Where(EsFormatoAdmitido).ToArray();
        int ignoradas = rutas.Length - admitidas.Length;

        if (admitidas.Length == 0)
        {
            Avisar(new FormatoNoSoportadoException().Message, error: true);
            return;
        }

        Agregando = true;

        try
        {
            string sugerido = admitidas.Length == 1
                ? System.IO.Path.GetFileNameWithoutExtension(admitidas[0])
                : $"Material ({admitidas.Length} imagenes)";

            // El material nuevo entra en la materia que este filtrando la lista: si cayera
            // siempre en "Sin materia", desapareceria de la vista apenas se agrega y
            // parecerian dos bugs distintos (no se agrego / se agrego mal).
            string materia = string.IsNullOrWhiteSpace(MateriaElegida)
                ? BibliotecaService.SinMateria
                : MateriaElegida!;

            var libro = await _biblioteca.AgregarFuenteAsync(admitidas, sugerido, materia);

            PoblarLibrosDeLaMateria();
            Libro = libro;

            string extra = ignoradas > 0
                ? $" Se ignoraron {ignoradas} archivo(s) con un formato no admitido."
                : string.Empty;
            Avisar($"Se agrego \"{libro.Titulo}\" ({libro.MedidaTamanio}) a la biblioteca.{extra}");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Asistente/Soltar", ex);
            Avisar(ex.Message, error: true);
        }
        finally
        {
            Agregando = false;
        }
    }

    /// <summary>true si la extension del archivo la cubre algun extractor (arquitectura Inc-4 §4.1).</summary>
    private static bool EsFormatoAdmitido(string ruta)
        => FactoriaExtractores.Para(System.IO.Path.GetExtension(ruta)) is not null;

    [RelayCommand]
    private async Task ElegirArchivoAsync()
    {
        string[]? rutas = _dialogos.ElegirFuentes();
        if (rutas is not null)
        {
            await SoltarAsync(rutas);
        }
    }

    // ------------------------------------------------------------------
    // Alcance concreto
    // ------------------------------------------------------------------
    /// <summary>Traduce modulos marcados + rango + tema en rangos de paginas reales.</summary>
    private List<RangoPaginas> ConstruirRangos(out string descripcion) => ConstruirRangos(Libro, out descripcion);

    /// <summary>
    /// Rangos de un documento concreto. Al combinar varios (US-024) cada uno se acota con
    /// SUS propios modulos marcados: las paginas 40-80 de un apunte no son las 40-80 del
    /// otro, asi que un rango unico compartido recortaria material al azar.
    ///
    /// El rango manual (<see cref="PresetRango"/> / <see cref="Desde"/> / <see cref="Hasta"/>)
    /// solo se aplica al documento en foco, que es el unico sobre el que el alumno lo vio y
    /// lo eligio; los demas entran completos salvo que tengan modulos marcados.
    /// </summary>
    private List<RangoPaginas> ConstruirRangos(Libro? cual, out string descripcion)
    {
        var rangos = new List<RangoPaginas>();
        var partes = new List<string>();

        if (cual is not Libro libro)
        {
            descripcion = string.Empty;
            return rangos;
        }

        bool esElEnfocado = ReferenceEquals(libro, Libro);
        int total = Math.Max(1, libro.CantidadPaginas);

        var modulosDelLibro = esElEnfocado ? Modulos.AsEnumerable() : libro.Modulos;
        var marcados = modulosDelLibro.Where(m => m.Seleccionado).OrderBy(m => m.DesdePagina).ToList();
        foreach (var m in marcados)
        {
            rangos.Add(new RangoPaginas(m.DesdePagina, m.HastaPagina, m.Nombre));
        }

        if (marcados.Count > 0)
        {
            partes.Add(marcados.Count <= 3
                ? string.Join(" + ", marcados.Select(m => m.Nombre))
                : $"{marcados.Count} modulos");
        }

        if (PresetRango != 0 && esElEnfocado)
        {
            int desde = Math.Clamp(Desde, 1, total);
            int hasta = Math.Clamp(Hasta, desde, total);

            rangos.Add(new RangoPaginas(desde, hasta, "Rango elegido"));
            partes.Add($"pags. {desde}-{hasta}");
        }

        if (rangos.Count == 0)
        {
            rangos.Add(new RangoPaginas(1, total, "Libro completo"));
            partes.Add("libro completo");
        }

        string tema = Tema.Trim();
        if (tema.Length > 0)
        {
            partes.Add($"tema \"{tema}\"");
        }

        descripcion = string.Join(" · ", partes);
        return rangos;
    }

    // ------------------------------------------------------------------
    // Generar
    // ------------------------------------------------------------------
    private bool PuedeGenerar() => !Generando && HayConQueArmar;

    [RelayCommand(CanExecute = nameof(PuedeGenerar))]
    private async Task GenerarAsync()
    {
        Mensaje = string.Empty;

        // US-026: el repaso se arma local y al instante. Sale antes de tocar nada del
        // pipeline de IA —extractores, claves, cuota— porque no necesita nada de eso
        // (RN-27): las preguntas ya se generaron y se guardaron cuando se rindieron.
        if (ModoRepaso)
        {
            GenerarRepaso();
            return;
        }

        var elegidos = Seleccionados;

        if (elegidos.Count == 0)
        {
            return;
        }

        // El documento en foco manda en el paso Alcance; si no esta entre los tildados, el
        // primero tildado ocupa ese lugar para que capitulos y rango sigan teniendo duenio.
        Libro libro = elegidos.Contains(Libro) ? Libro! : elegidos[0];

        // RN-23. La lista filtrada por materia ya lo hace imposible desde la interfaz, pero
        // esto es lo que garantiza que un examen combinado nunca mezcle materias, aunque la
        // seleccion llegue por otro camino.
        var materias = elegidos
            .Select(l => l.Materia)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (materias.Count > 1)
        {
            Avisar(
                "Los documentos elegidos son de materias distintas (" + string.Join(", ", materias) + "). " +
                "Un examen combina material de una sola materia: elegi una y volve a marcar.", error: true);
            return;
        }

        var faltantes = elegidos.Where(l => !l.ArchivoDisponible).ToList();
        if (faltantes.Count > 0)
        {
            Avisar(faltantes.Count == elegidos.Count
                ? "No se encuentra la copia del material. Volve a agregarlo desde Libros."
                : "No se encuentra la copia interna de: " + string.Join(", ", faltantes.Select(l => $"\"{l.Titulo}\"")) +
                  ". Volve a agregarlos desde Libros, o destildalos para generar con el resto.", error: true);
            return;
        }

        if (!_sesion.HayApiKey)
        {
            Avisar("Falta la API Key de Gemini. Cargala en Ajustes.", error: true);
            _nav.IrA("ajustes");
            return;
        }

        if (HayExamenSinTerminar?.Invoke() == true &&
            !_dialogos.Confirmar("Hay un examen sin finalizar. Si generas uno nuevo se descarta el actual.\n\n¿Continuar?"))
        {
            return;
        }

        bool combinado = elegidos.Count > 1;
        bool esPdf = libro.Tipo == TipoFuente.Pdf;

        // Solo PDF tiene paginas/capitulos: para el resto de los formatos el recorte es
        // siempre "material completo" y el unico acotador es el eje tematico (US-009 AC-T45/46).
        string alcance;
        var rangos = new List<RangoPaginas>();
        if (esPdf && !combinado)
        {
            rangos = ConstruirRangos(out alcance);
        }
        else
        {
            alcance = ResumenAlcance;
        }

        int cantidad = Cantidad;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IProgress<string> progreso = new Progress<string>(texto =>
        {
            Progreso = texto;
            _nav.Estado(texto);
        });

        Generando = true;
        string examenId = Guid.NewGuid().ToString("N");

        try
        {
            // Paso 1: extraer el material segun su formato (contrato IExtractorContenido,
            // arquitectura Inc-4 §4.1). PDF lee por bloques de paginas; Office por parte;
            // el set de imagenes las prepara y convierte HEIC.
            var opciones = new OpcionesExtraccion
            {
                PaginasPorBloque = _sesion.Config.PaginasPorBloque,
                MaxCaracteres = _sesion.Config.MaxCaracteresContexto,
                ExtraerImagenes = IncluirImagenes && _sesion.Config.IncluirImagenes,
                MaxImagenes = _sesion.Config.MaxImagenesPorExamen,
                CarpetaImagenes = RutasApp.CarpetaImagenesExamen(examenId)
            };

            // Para un set de imagenes, el tope "por material" (NFR-43) lo lee el
            // ImagenExtractor de MaxPaginasEscaneadas.
            if (elegidos.Any(l => l.Tipo == TipoFuente.SetImagenes))
            {
                opciones.MaxPaginasEscaneadas = Math.Max(1, _sesion.Config.MaxImagenesPorMaterial);
            }

            if (combinado)
            {
                // RN-3: combinar cinco materiales multiplica por cinco lo que viaja. Se avisa
                // antes de empezar, no cuando ya paso el rato esperando.
                _nav.Estado(
                    $"Combinando {elegidos.Count} documentos de {libro.Materia}: se leen uno por uno " +
                    "y despues se genera un unico examen. Puede tardar mas y consumir mas cuota.");
            }

            // Paso 1: cada documento con SU extractor (US-024: un PDF con texto, un .docx
            // con fotos y un set de imagenes se procesan distinto), y despues se fusionan.
            var partes = new List<ParteDelMaterial>();
            var ilegibles = new List<string>();

            // Todo lo extraido, aporte o no. Sirve para dos cosas que se pierden si solo se
            // guarda lo que entro al examen: explicar POR QUE un alcance quedo vacio (hace
            // falta el conteo de paginas sin texto), y borrar del disco las paginas
            // escaneadas de un documento que despues quedo afuera.
            var todoLoLeido = new List<ExtraccionResultado>();

            for (int i = 0; i < elegidos.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var doc = elegidos[i];

                var extractor = FactoriaExtractores.Para(System.IO.Path.GetExtension(doc.RutaArchivo));
                if (extractor is null)
                {
                    ilegibles.Add($"\"{doc.Titulo}\" (formato que la app ya no puede procesar)");
                    continue;
                }

                if (combinado)
                {
                    progreso.Report($"Leyendo {i + 1} de {elegidos.Count}: {doc.Titulo}...");
                }

                // Cada documento escribe sus imagenes en su propia subcarpeta: los extractores
                // numeran los archivos desde uno en cada material, asi que una carpeta
                // compartida haria que el segundo documento pisara las figuras del primero.
                var opcionesDoc = ParaDocumento(opciones, combinado, examenId, i);

                var recorte = new RecorteFuente
                {
                    Paginas = doc.Tipo == TipoFuente.Pdf ? ConstruirRangos(doc, out _) : null,
                    TemaLibre = Tema.Trim()
                };

                try
                {
                    var parcial = await extractor.ExtraerAsync(doc.Archivos, recorte, opcionesDoc, progreso, ct);
                    todoLoLeido.Add(parcial);

                    if (parcial.TieneMaterial)
                    {
                        partes.Add(new ParteDelMaterial(doc.Titulo, parcial));
                    }
                    else
                    {
                        ilegibles.Add($"\"{doc.Titulo}\" (sin contenido aprovechable)");
                    }
                }
                catch (FuenteIlegibleException ex)
                {
                    // US-024/US-022: un documento ilegible no tira abajo el examen entero si
                    // hay otros que si se pudieron leer. Se descarta y se informa al final.
                    ilegibles.Add($"\"{doc.Titulo}\" ({ex.Message})");
                }
            }

            if (partes.Count == 0)
            {
                // Ningun documento aporto material: no se crea un examen vacio (RN-4/NFR-41).
                // Con una sola fuente PDF se conserva el diagnostico fino de siempre (rango
                // vacio vs. escaneo que no se pudo decodificar), que son dos problemas con
                // dos soluciones distintas para el usuario.
                Avisar(elegidos.Count == 1 && esPdf && todoLoLeido.Count == 1
                    ? DescribirAlcanceVacio(todoLoLeido[0])
                    : "No se encontro contenido para generar preguntas en " +
                      (elegidos.Count == 1 ? "este material." : "ninguno de los documentos elegidos.") +
                      (ilegibles.Count > 0 ? " Motivo: " + string.Join("; ", ilegibles) + "." : string.Empty),
                    error: true);

                BorrarPaginasEscaneadas(todoLoLeido);
                return;
            }

            var extraccion = CombinadorDeMateriales.Combinar(partes, opciones);

            // Los documentos que si entraron, en el orden en que se fusionaron: es la lista
            // cerrada contra la que se valida el "DocumentoOrigen" que devuelve el modelo.
            var documentos = partes.Select(p => p.Documento).ToList();

            if (!extraccion.TieneMaterial)
            {
                Avisar(esPdf
                    ? DescribirAlcanceVacio(extraccion)
                    : "No se encontro contenido para generar preguntas en este material.", error: true);
                return;
            }

            if (ilegibles.Count > 0)
            {
                _nav.Estado(
                    $"Se generan preguntas con {partes.Count} de los {elegidos.Count} documentos. " +
                    "Quedaron afuera: " + string.Join("; ", ilegibles) + ".");
            }

            if (!extraccion.TieneTexto)
            {
                // El material son imagenes y las lee Gemini. Pasa con un PDF escaneado y, desde
                // US-014, tambien con un Word o PowerPoint armado pegando fotos: por eso la
                // unidad se nombra segun la fuente, que en un .docx no son "paginas".
                string unidad = esPdf ? "paginas" : "imagenes";

                _nav.Estado(
                    $"El material no tiene texto extraible: se mandan {extraccion.PaginasEscaneadas.Count} " +
                    $"{unidad} para que Gemini les lea el contenido. Puede tardar mas y consumir mas cuota.");
            }

            if (extraccion.HuboMuestreo)
            {
                _nav.Estado($"Alcance extenso: se muestrearon {extraccion.PaginasLeidas} de {extraccion.PaginasSeleccionadas} paginas.");
            }

            // Paso 2: pedirle los lotes de preguntas a Gemini.
            var solicitud = new SolicitudGeneracion
            {
                ApiKey = _sesion.Config.ApiKey,

                // Todas las claves, no solo la primera: si la del dia se queda sin cuota,
                // el servicio rota a la siguiente sin cortarle la generacion al usuario.
                Claves = _sesion.Config.ClavesDisponibles.ToList(),

                Modelo = _sesion.Config.Modelo,

                // El PDF y el alcance viajan para que, cuando convenga, se suba el recorte
                // con la Files API en vez de mandar el texto extraido. Solo aplica a PDF:
                // para el resto va vacio y SubirPdfSiConviene corta por RutaPdf vacio.
                //
                // Con varios documentos combinados queda deshabilitado: subir un solo PDF
                // haria que el modelo generara el examen entero desde ese archivo e ignorara
                // el material ya fusionado de los demas, que es justo lo contrario de lo que
                // el alumno pidio al marcarlos (US-024).
                RutaPdf = esPdf && !combinado ? libro.RutaArchivo : string.Empty,
                Rangos = rangos,
                UsarFilesApi = esPdf && !combinado && _sesion.Config.UsarFilesApi,

                TituloLibro = combinado ? TituloDelConjunto(documentos, libro.Materia) : libro.Titulo,
                Materia = libro.Materia,

                // RN-24: la lista de documentos activa el campo "DocumentoOrigen" del esquema
                // y es el conjunto cerrado contra el que se valida lo que devuelve el modelo.
                Documentos = documentos,
                TemaLibre = Tema.Trim(),
                AlcanceDescripcion = alcance,
                CantidadPreguntas = cantidad,
                PreguntasPorLote = _sesion.Config.PreguntasPorLote,
                IncluirImagenes = IncluirImagenes,
                Fragmentos = extraccion.Fragmentos,
                Imagenes = extraccion.Imagenes,
                PaginasEscaneadas = extraccion.PaginasEscaneadas
            };

            var preguntas = await _gemini.GenerarPreguntasAsync(solicitud, progreso, ct);

            // Las paginas escaneadas ya cumplieron: eran material para el prompt, no
            // ilustraciones del examen. Dejarlas en disco serian varios MB por intento.
            //
            // Se barre todo lo leido y no solo lo que entro al examen: al combinar, el tope
            // de RN-3 deja fuera paginas que igual se escribieron en disco, y esas quedarian
            // ocupando lugar hasta que se borre el examen del historial.
            BorrarPaginasEscaneadas(todoLoLeido);

            // Paso 3: armar el examen y entregarlo.
            var examen = new ExamenEnCurso
            {
                // Mismo id que nombra la carpeta de imagenes: al finalizar, registro.Id
                // hereda este valor y US-012 puede limpiar Imagenes\{id} (arquitectura Inc-4 §1.4).
                Id = examenId,

                // Con varios documentos no hay un libro duenio del examen. Se guarda el que
                // estaba en foco para que el historial siga pudiendo reconstruir titulo y
                // materia (ShellViewModel.RecuperarHuerfanos lo usa), y el titulo visible
                // pasa a nombrar el conjunto.
                LibroId = libro.Id,
                LibroTitulo = combinado ? TituloDelConjunto(documentos, libro.Materia) : libro.Titulo,
                Materia = libro.Materia,
                AlcanceDescripcion = alcance,
                Inicio = DateTime.Now,
                Ronda = 0,

                // US-034 / RN-43: el cronometro es del formato, asi que aplica igual a este
                // examen que a un repaso o a uno importado.
                LimiteSegundos = LimiteEnSegundos
            };

            foreach (var p in preguntas.OrderBy(_ => _random.Next()))
            {
                examen.Preguntas.Add(p);
            }

            ExamenGenerado?.Invoke(examen);

            if (preguntas.Count < cantidad)
            {
                // El aviso viejo ("suele pasar cuando el material es corto") no permitia
                // hacer nada: no distinguia un alcance de tres paginas de un modelo que no
                // colabora. Estas dos causas se distinguen por el tamanio del alcance, asi
                // que se nombra la que corresponde.
                int paginas = esPdf ? PaginasDelAlcance : int.MaxValue;

                string causa = paginas < 15
                    ? $"El alcance elegido tiene {paginas} pagina(s): con tan poco material no salen " +
                      $"{cantidad} preguntas distintas. Ampliá el alcance o pedí menos preguntas."
                    : "El modelo devolvió menos de las pedidas y los lotes de relleno no alcanzaron. " +
                      "Probá de nuevo, o bajá \"Preguntas por peticion\" en Ajustes si se repite.";

                Avisar($"El examen quedó con {preguntas.Count} de las {cantidad} preguntas pedidas. {causa}");
            }
        }
        catch (OperationCanceledException)
        {
            _nav.Estado("Generacion cancelada.");
            Avisar("Se cancelo la generacion.");
        }
        catch (GeminiException ex)
        {
            RutasApp.RegistrarError("GenerarExamen/Gemini", ex);
            Avisar(ex.Message, error: true);
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("GenerarExamen", ex);
            Avisar(ex.Message, error: true);
        }
        finally
        {
            Generando = false;
            Progreso = string.Empty;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Arma el examen de repaso combinando los examenes tildados (US-026).
    ///
    /// No hay await, ni token de cancelacion, ni indicador de progreso, y eso es el punto:
    /// RN-27 dice que un repaso no depende de la IA ni de la conexion, asi que sale armado
    /// antes de que el alumno alcance a ver un spinner.
    /// </summary>
    private void GenerarRepaso()
    {
        // Los tres modos locales entran por aca, pero solo el combinado necesita este cuerpo.
        // Los otros dos arman su propio ExamenEnCurso y salen.
        if (ModoFalladas)
        {
            GenerarRepasoInteligente();
            return;
        }

        if (ModoImportado)
        {
            GenerarDesdeImportado();
            return;
        }

        var elegidos = ExamenesElegidos;

        if (elegidos.Count < 2)
        {
            Avisar("Tilda al menos dos examenes: un repaso combina preguntas de varios.", error: true);
            return;
        }

        if (!ConfirmarDescarteDelIntentoAbierto())
        {
            return;
        }

        var armado = CombinadorDeExamenes.Armar(elegidos, Cantidad, _random);

        if (armado.Preguntas.Count == 0)
        {
            Avisar("Los examenes tildados no tienen preguntas guardadas, asi que no hay de donde armar el repaso.",
                error: true);
            return;
        }

        var titulos = elegidos.Select(e => e.TituloTexto).ToList();
        var materias = elegidos.Select(e => e.Materia).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var repaso = new ExamenEnCurso
        {
            LibroTitulo = CombinadorDeExamenes.TituloDelRepaso(titulos),

            // A diferencia de US-024 aca si se admite mezclar materias: no se genera desde
            // material con IA, son preguntas que el alumno ya rindio. Con varias se nombra el
            // conjunto, porque poner una sola seria mentir sobre las otras.
            Materia = materias.Count == 1 ? materias[0] : $"{materias.Count} materias",

            AlcanceDescripcion = armado.SeAjustoLaCantidad
                ? $"repaso de {elegidos.Count} examenes · {armado.Preguntas.Count} preguntas (habia {armado.Disponibles})"
                : $"repaso de {elegidos.Count} examenes · {armado.Preguntas.Count} preguntas",

            Inicio = DateTime.Now,
            Ronda = 0,
            EsRepaso = true,
            ExamenesDeOrigen = titulos,
            LimiteSegundos = LimiteEnSegundos
        };

        foreach (var pregunta in armado.Preguntas)
        {
            repaso.Preguntas.Add(pregunta);
        }

        ExamenGenerado?.Invoke(repaso);
        AvisarSiSeAjusto(armado, "entre los examenes elegidos");
    }

    /// <summary>Segundos de limite para el examen que se esta por generar. 0 = sin limite.</summary>
    private int LimiteEnSegundos => MinutosLimite * 60;

    private bool ConfirmarDescarteDelIntentoAbierto() =>
        HayExamenSinTerminar?.Invoke() != true ||
        _dialogos.Confirmar("Hay un examen sin finalizar. Si armas otro se descarta el actual.\n\n¿Continuar?");

    /// <summary>
    /// El aviso de "se ajusto la cantidad" es uno solo para los dos repasos locales: US-032
    /// pide explicitamente que avise "igual que en US-026". Con dos copias del texto, la
    /// segunda se desactualiza.
    /// </summary>
    private void AvisarSiSeAjusto(RepasoArmado armado, string deDonde)
    {
        if (!armado.SeAjustoLaCantidad)
        {
            return;
        }

        // Pediste 60 y recibiste 22. Sin este aviso parece que la app fallo.
        _dialogos.Aviso("Se ajusto la cantidad",
            $"Pediste {armado.Pedidas} preguntas, pero {deDonde} hay {armado.Disponibles} " +
            "y ninguna se repite. El examen salio con todas las que habia.");
    }

    /// <summary>
    /// US-032 — arma el examen con lo que el alumno viene fallando en la materia o el
    /// documento elegido. Igual que el combinado: local, instantaneo y sin cuota (RN-40).
    /// </summary>
    private void GenerarRepasoInteligente()
    {
        if (FocoElegido is not FocoDeRepaso foco)
        {
            Avisar("Elegi una materia o un documento para repasar lo que fallaste ahi.", error: true);
            return;
        }

        if (!ConfirmarDescarteDelIntentoAbierto())
        {
            return;
        }

        var armado = RepasoInteligente.Armar(
            _sesion.Historial, Cantidad, foco.Clave, foco.EsMateria, _random);

        if (armado.Preguntas.Count == 0)
        {
            // Puede pasar entre que se dibujo la lista y se toco generar: si el alumno rindio
            // un repaso en el medio y acerto todo, el pozo quedo vacio. Es una buena noticia,
            // y decirlo asi evita que parezca una falla.
            Avisar($"Ya no te quedan preguntas falladas en {foco.Nombre}. Las acertaste todas.", error: false);
            PoblarFocosDeRepaso();
            return;
        }

        var repaso = new ExamenEnCurso
        {
            LibroTitulo = RepasoInteligente.Titulo(foco.Nombre),
            Materia = foco.EsMateria ? foco.Nombre : string.Empty,
            AlcanceDescripcion = armado.SeAjustoLaCantidad
                ? $"lo que falle · {armado.Preguntas.Count} preguntas (habia {armado.Disponibles})"
                : $"lo que falle · {armado.Preguntas.Count} preguntas",
            Inicio = DateTime.Now,
            Ronda = 0,

            // Cuenta como repaso para el historial: no es un intento nuevo del examen
            // original, y no puede alimentar un repaso combinado (misma regla que US-026).
            EsRepaso = true,
            ExamenesDeOrigen = new List<string> { foco.Nombre },
            LimiteSegundos = LimiteEnSegundos
        };

        foreach (var pregunta in armado.Preguntas)
        {
            repaso.Preguntas.Add(pregunta);
        }

        ExamenGenerado?.Invoke(repaso);
        AvisarSiSeAjusto(armado, $"en {foco.Nombre} solo tenes {armado.Disponibles} falladas —");
    }

    /// <summary>
    /// US-037 — rinde un examen que compartio un compañero. Las preguntas ya vienen armadas
    /// en el archivo, asi que esto tampoco toca la IA.
    /// </summary>
    private void GenerarDesdeImportado()
    {
        if (ImportadoElegido is not ExamenImportado importado)
        {
            Avisar("Elegi cual de los examenes importados queres rendir.", error: true);
            return;
        }

        if (!ConfirmarDescarteDelIntentoAbierto())
        {
            return;
        }

        string examenId = Guid.NewGuid().ToString("N");

        var preguntas = CompartirExamenService.Desempaquetar(
            importado.Paquete, RutasApp.CarpetaImagenesExamen(examenId));

        if (preguntas.Count == 0)
        {
            Avisar("Ese archivo no tiene preguntas para rendir.", error: true);
            return;
        }

        var examen = new ExamenEnCurso
        {
            Id = examenId,
            LibroTitulo = importado.Titulo,
            Materia = importado.Paquete.Materia,
            AlcanceDescripcion = "examen compartido por un compañero",
            Inicio = DateTime.Now,
            Ronda = 0,
            LimiteSegundos = LimiteEnSegundos
        };

        foreach (var pregunta in preguntas)
        {
            // Se remezclan las opciones igual que en un repaso: si dos compañeros comparan el
            // examen, que la correcta no sea "la C" para los dos.
            pregunta.MezclarOpciones(_random);
            examen.Preguntas.Add(pregunta);
        }

        ExamenGenerado?.Invoke(examen);
    }

    /// <summary>
    /// Nombre del examen cuando combina varios documentos (US-024). Con dos o tres los
    /// nombra: es lo que el alumno reconoce en el historial. Con mas, la lista completa no
    /// entraria en una fila, asi que se resume por materia.
    /// </summary>
    private static string TituloDelConjunto(IReadOnlyList<string> documentos, string materia) =>
        documentos.Count <= 3
            ? string.Join(" + ", documentos)
            : $"{materia} ({documentos.Count} documentos)";

    /// <summary>
    /// Opciones de extraccion de un documento puntual dentro de una seleccion combinada
    /// (US-024).
    ///
    /// El unico cambio respecto de las opciones globales es la carpeta de imagenes: cada
    /// extractor numera sus archivos desde uno ("fig_01.png"), asi que con una carpeta
    /// compartida el segundo documento pisaria en disco las figuras del primero. La
    /// subcarpeta cuelga de la carpeta del examen, asi que el borrado del historial (US-012),
    /// que la elimina recursivamente, las sigue limpiando todas.
    ///
    /// Los topes de imagenes y de paginas escaneadas se dejan enteros a proposito: son un
    /// limite de lo que se manda, no de lo que se lee, y quien lo aplica sobre el conjunto
    /// es <see cref="CombinadorDeMateriales"/>, que ademas reparte el cupo por rondas entre
    /// documentos. Recortarlo aca le daria a cada documento una cuota fija y desperdiciaria
    /// la de los que tienen pocas figuras.
    /// </summary>
    private static OpcionesExtraccion ParaDocumento(
        OpcionesExtraccion globales, bool combinado, string examenId, int indice)
    {
        if (!combinado)
        {
            return globales;
        }

        // PdfExtractorService escribe directo en la carpeta sin crearla (los otros dos
        // extractores si la crean): sin esto, un PDF combinado fallaria al guardar la
        // primera figura.
        string carpeta = System.IO.Path.Combine(RutasApp.CarpetaImagenesExamen(examenId), $"d{indice + 1}");
        System.IO.Directory.CreateDirectory(carpeta);

        return new OpcionesExtraccion
        {
            PaginasPorBloque = globales.PaginasPorBloque,
            MaxPaginasLeidas = globales.MaxPaginasLeidas,
            MaxCaracteres = globales.MaxCaracteres,
            ExtraerImagenes = globales.ExtraerImagenes,
            MaxImagenes = globales.MaxImagenes,
            MinAnchoImagen = globales.MinAnchoImagen,
            MinAltoImagen = globales.MinAltoImagen,
            MaxProporcionPaginaParaFigura = globales.MaxProporcionPaginaParaFigura,
            MinCaracteresPagina = globales.MinCaracteresPagina,
            MaxPaginasEscaneadas = globales.MaxPaginasEscaneadas,
            MaxPaginasEscaneadasPorBloque = globales.MaxPaginasEscaneadasPorBloque,
            LadoMaximoPaginaEscaneada = globales.LadoMaximoPaginaEscaneada,
            MinLadoPaginaEscaneada = globales.MinLadoPaginaEscaneada,
            CarpetaImagenes = carpeta
        };
    }

    /// <summary>
    /// Por que no hay con que armar el examen. Distingue el rango vacio del escaneo que
    /// no se pudo decodificar, porque lo que tiene que hacer el usuario no es lo mismo.
    /// </summary>
    private static string DescribirAlcanceVacio(ExtraccionResultado extraccion)
    {
        if (extraccion.PaginasSinTexto == 0)
        {
            return "El alcance elegido esta vacio: no se leyo ninguna pagina. " +
                   "Revisa el rango de paginas o el modulo seleccionado.";
        }

        return
            $"Las {extraccion.PaginasSinTexto} paginas del alcance no tienen texto extraible y " +
            "tampoco se pudo rescatar su imagen para que la lea Gemini. Suele pasar con escaneos " +
            "comprimidos en JBIG2 o JPEG 2000. Proba con otro rango, o pasa el PDF por un OCR " +
            "(por ejemplo el de Acrobat o el de Google Drive) y volve a agregarlo.";
    }

    private static void BorrarPaginasEscaneadas(IEnumerable<ExtraccionResultado> extracciones)
    {
        foreach (var pagina in extracciones.SelectMany(e => e.PaginasEscaneadas))
        {
            try
            {
                if (File.Exists(pagina.Ruta))
                {
                    File.Delete(pagina.Ruta);
                }
            }
            catch (Exception ex)
            {
                RutasApp.RegistrarError($"BorrarPaginaEscaneada({pagina.Ruta})", ex);
            }
        }
    }

    private bool PuedeCancelar() => Generando;

    [RelayCommand(CanExecute = nameof(PuedeCancelar))]
    private void Cancelar()
    {
        _cts?.Cancel();
        Progreso = "Cancelando...";
    }

    public void CancelarSiCorre() => _cts?.Cancel();

    /// <summary>Al entrar, arranca con un libro elegido si hay uno solo o si ya habia seleccion.</summary>
    public override void AlEntrar()
    {
        // Si la materia elegida desaparecio (la borraron desde Libros), se vuelve a "todas"
        // en vez de dejar la lista de documentos vacia sin explicar por que.
        if (HayMateriaElegida && !_biblioteca.ExisteMateria(MateriaElegida!))
        {
            MateriaElegida = null;
        }

        PoblarLibrosDeLaMateria();

        if (Libro is null || !LibrosDeLaMateria.Contains(Libro))
        {
            Libro = LibrosDeLaMateria.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HayLibros));
        OnPropertyChanged(nameof(Materias));

        // US-026: la lista de examenes se rearma al entrar, porque puede haber uno mas desde
        // la ultima visita (se acaba de rendir) o uno menos (se borro del historial).
        PoblarExamenesParaRepaso();

        Recalcular();
    }

    private void Avisar(string texto, bool error = false)
    {
        Mensaje = texto;
        Severidad = error ? 3 : 2;
    }
}
