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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayLibroElegido))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerarCommand))]
    private Libro? _libro;

    public bool HayLibroElegido => Libro is not null;

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
                return "Elegi un libro para empezar.";
            }

            ConstruirRangos(out string descripcion);
            return descripcion;
        }
    }

    // ------------------------------------------------------------------
    // Reacciones a los cambios
    // ------------------------------------------------------------------
    partial void OnLibroChanged(Libro? value)
    {
        Modulos.Clear();

        if (value is not null)
        {
            foreach (var m in value.Modulos.OrderBy(m => m.DesdePagina))
            {
                m.Seleccionado = false;
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
        Pasos[0].Resumen = Libro is null ? "Sin elegir" : $"{Libro.Titulo} · {Libro.CantidadPaginas} pag.";
        Pasos[1].Resumen = Libro is null ? "-" : $"{ResumenAlcance} · ~{PaginasDelAlcance} pag.";
        Pasos[2].Resumen = $"{Cantidad} preguntas{(IncluirImagenes ? " · con graficos" : string.Empty)}";

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
        1 => Libro is not null,
        2 => true,
        _ => false
    };

    [RelayCommand(CanExecute = nameof(PuedeAvanzar))]
    private void Siguiente() => Paso = Math.Min(UltimoPaso, Paso + 1);

    [RelayCommand(CanExecute = nameof(EsPrimerPasoNo))]
    private void Anterior() => Paso = Math.Max(PrimerPaso, Paso - 1);

    private bool EsPrimerPasoNo() => Paso > PrimerPaso;

    /// <summary>Solo deja volver a pasos ya visitados: saltear adelante rompe la dependencia entre pasos.</summary>
    [RelayCommand]
    private void IrAPaso(int numero)
    {
        if (numero >= PrimerPaso && numero <= Paso)
        {
            Paso = numero;
        }
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

    [RelayCommand]
    private async Task SoltarAsync(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        Agregando = true;

        try
        {
            string sugerido = System.IO.Path.GetFileNameWithoutExtension(ruta);
            var libro = await _biblioteca.AgregarLibroAsync(ruta, sugerido, "Sin materia");
            Libro = libro;
            Avisar($"Se agrego \"{libro.Titulo}\" ({libro.CantidadPaginas} paginas) a la biblioteca.");
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

    [RelayCommand]
    private async Task ElegirArchivoAsync()
    {
        string? ruta = _dialogos.ElegirPdf();
        if (ruta is not null)
        {
            await SoltarAsync(ruta);
        }
    }

    // ------------------------------------------------------------------
    // Alcance concreto
    // ------------------------------------------------------------------
    /// <summary>Traduce modulos marcados + rango + tema en rangos de paginas reales.</summary>
    private List<RangoPaginas> ConstruirRangos(out string descripcion)
    {
        var rangos = new List<RangoPaginas>();
        var partes = new List<string>();

        if (Libro is not Libro libro)
        {
            descripcion = string.Empty;
            return rangos;
        }

        int total = Math.Max(1, libro.CantidadPaginas);

        var marcados = Modulos.Where(m => m.Seleccionado).OrderBy(m => m.DesdePagina).ToList();
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

        if (PresetRango != 0)
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
    private bool PuedeGenerar() => Libro is not null && !Generando;

    [RelayCommand(CanExecute = nameof(PuedeGenerar))]
    private async Task GenerarAsync()
    {
        Mensaje = string.Empty;

        if (Libro is not Libro libro)
        {
            return;
        }

        if (!libro.ArchivoDisponible)
        {
            Avisar("No se encuentra la copia del PDF. Volve a agregarlo desde Libros.", error: true);
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

        var rangos = ConstruirRangos(out string alcance);
        int cantidad = Cantidad;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var progreso = new Progress<string>(texto =>
        {
            Progreso = texto;
            _nav.Estado(texto);
        });

        Generando = true;
        string examenId = Guid.NewGuid().ToString("N");

        try
        {
            // Paso 1: leer el PDF por bloques de paginas.
            var opciones = new OpcionesExtraccion
            {
                PaginasPorBloque = _sesion.Config.PaginasPorBloque,
                MaxCaracteres = _sesion.Config.MaxCaracteresContexto,
                ExtraerImagenes = IncluirImagenes && _sesion.Config.IncluirImagenes,
                MaxImagenes = _sesion.Config.MaxImagenesPorExamen,
                CarpetaImagenes = RutasApp.CarpetaImagenesExamen(examenId)
            };

            var extraccion = await _pdf.ExtraerAsync(libro.RutaArchivo, rangos, opciones, progreso, ct);

            if (!extraccion.TieneMaterial)
            {
                Avisar(DescribirAlcanceVacio(extraccion), error: true);
                return;
            }

            if (!extraccion.TieneTexto)
            {
                // PDF escaneado: el material son las paginas como imagen y las lee Gemini.
                _nav.Estado(
                    $"El alcance no tiene texto extraible: se mandan {extraccion.PaginasEscaneadas.Count} " +
                    "paginas como imagen para que Gemini las lea. Puede tardar mas y consumir mas cuota.");
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
                // con la Files API en vez de mandar el texto extraido.
                RutaPdf = libro.RutaArchivo,
                Rangos = rangos,
                UsarFilesApi = _sesion.Config.UsarFilesApi,

                TituloLibro = libro.Titulo,
                Materia = libro.Materia,
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
            BorrarPaginasEscaneadas(extraccion);

            // Paso 3: armar el examen y entregarlo.
            var examen = new ExamenEnCurso
            {
                LibroId = libro.Id,
                LibroTitulo = libro.Titulo,
                Materia = libro.Materia,
                AlcanceDescripcion = alcance,
                Inicio = DateTime.Now,
                Ronda = 0
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
                int paginas = PaginasDelAlcance;

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

    private static void BorrarPaginasEscaneadas(ExtraccionResultado extraccion)
    {
        foreach (var pagina in extraccion.PaginasEscaneadas)
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
        if (Libro is null || !_biblioteca.Libros.Contains(Libro))
        {
            Libro = _biblioteca.Libros.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HayLibros));
        Recalcular();
    }

    private void Avisar(string texto, bool error = false)
    {
        Mensaje = texto;
        Severidad = error ? 3 : 2;
    }
}
