using System.Collections.ObjectModel;
using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>Historial de intentos y estadisticas acumuladas.</summary>
public partial class HistorialViewModel : PaginaViewModel
{
    private readonly SesionUsuarioService _sesion;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _nav;

    public HistorialViewModel(SesionUsuarioService sesion, IDialogos dialogos, INavegacion nav)
        : base("historial", "Historial", "History24")
    {
        _sesion = sesion;
        _dialogos = dialogos;
        _nav = nav;

        Escala = new ObservableCollection<string>(EvaluadorUBA.DescribirEscala());

        // RN-30: el color es de la materia, no del examen. Si el alumno le cambia el color a
        // "Fisiologia" desde Libros, los examenes de fisiologia que ya estan dibujados en el
        // historial tienen que repintarse — pero cada ExamenRendido resuelve su color solo al
        // preguntar, asi que hay que pedirle que lo vuelva a preguntar.
        PaletaMaterias.Cambio += RepintarMaterias;
    }

    private void RepintarMaterias()
    {
        foreach (var examen in _sesion.Historial)
        {
            examen.NotificarColorMateria();
        }
    }

    public ObservableCollection<ExamenRendido> Examenes => _sesion.Historial;

    public ObservableCollection<string> Escala { get; }

    // ------------------------------------------------------------------
    // US-035 — buscador
    // ------------------------------------------------------------------

    /// <summary>
    /// Lo que se muestra en la lista. Es una coleccion aparte y no la del historial porque
    /// filtrar no puede sacar examenes del perfil: lo que se esconde sigue existiendo, y las
    /// estadisticas de arriba se siguen calculando sobre todos.
    /// </summary>
    public ObservableCollection<ExamenRendido> ExamenesFiltrados { get; } = new();

    [ObservableProperty]
    private string _filtro = string.Empty;

    partial void OnFiltroChanged(string value) => AplicarFiltro();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvisoSinResultados))]
    private bool _sinResultados;

    public string AvisoSinResultados => $"No se encontró nada para \"{Filtro.Trim()}\".";

    /// <summary>
    /// Filtra por titulo, materia y alcance/tema, que es lo que pide el criterio y tambien
    /// lo unico por lo que uno se acuerda de un examen viejo.
    /// </summary>
    private void AplicarFiltro()
    {
        string texto = Filtro.Trim();

        ExamenesFiltrados.Clear();

        foreach (var examen in _sesion.Historial)
        {
            if (texto.Length == 0 || Coincide(examen, texto))
            {
                ExamenesFiltrados.Add(examen);
            }
        }

        // Con el buscador vacio nunca hay "sin resultados": no hay busqueda que fallar. Un
        // historial vacio ya tiene su propio estado vacio, que dice otra cosa.
        SinResultados = texto.Length > 0 && ExamenesFiltrados.Count == 0;
    }

    private static bool Coincide(ExamenRendido examen, string texto) =>
        examen.TituloTexto.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
        examen.LibroTitulo.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
        examen.Materia.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
        examen.AlcanceDescripcion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void LimpiarFiltro() => Filtro = string.Empty;

    // ------------------------------------------------------------------
    // US-033 — evolucion por materia
    // ------------------------------------------------------------------

    /// <summary>Materias que tienen al menos un examen rendido.</summary>
    public ObservableCollection<string> MateriasConExamenes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorDeLaMateria))]
    private string _materiaEnEvolucion = string.Empty;

    partial void OnMateriaEnEvolucionChanged(string value) => RecalcularEvolucion();

    /// <summary>La serie de la materia elegida, ya lista para dibujar.</summary>
    [ObservableProperty]
    private EvolucionMateria? _evolucion;

    public bool HayMateriasParaGraficar => MateriasConExamenes.Count > 0;

    /// <summary>US-033 pide el color de la materia como acento; sale de la paleta (RN-30/RN-34).</summary>
    public string ColorDeLaMateria => PaletaMaterias.ColorDe(MateriaEnEvolucion);

    [RelayCommand]
    private void VerEvolucionDe(string? materia)
    {
        if (!string.IsNullOrWhiteSpace(materia))
        {
            MateriaEnEvolucion = materia;
        }
    }

    private void RecalcularEvolucion()
    {
        Evolucion = string.IsNullOrWhiteSpace(MateriaEnEvolucion)
            ? null
            : EvolucionDeMateria.De(_sesion.Historial, MateriaEnEvolucion);
    }

    private void RefrescarMaterias()
    {
        string anterior = MateriaEnEvolucion;

        MateriasConExamenes.Clear();

        foreach (string materia in EvolucionDeMateria.MateriasConExamenes(_sesion.Historial))
        {
            MateriasConExamenes.Add(materia);
        }

        OnPropertyChanged(nameof(HayMateriasParaGraficar));

        // Se conserva la materia que estaba abierta si sigue teniendo examenes; si no, la
        // primera. Sin esto, borrar un examen dejaba el grafico apuntando a la nada.
        MateriaEnEvolucion = MateriasConExamenes.Contains(anterior)
            ? anterior
            : MateriasConExamenes.FirstOrDefault() ?? string.Empty;

        RecalcularEvolucion();
    }

    /// <summary>Preguntas del examen abierto en el detalle (US-025).</summary>
    public ObservableCollection<Pregunta> DetallePreguntas { get; } = new();


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayExamenes))]
    private int _total;

    [ObservableProperty]
    private string _resumen = "Todavia no rendiste ningun examen.";

    [ObservableProperty]
    private string _detalle = string.Empty;

    [ObservableProperty]
    private string _promedio = "-";

    [ObservableProperty]
    private string _aciertos = "-";

    [ObservableProperty]
    private string _mejorNota = "-";

    public bool HayExamenes => Total > 0;

    public void Refrescar()
    {
        var perfil = _sesion.Perfil;
        Total = perfil.TotalExamenes;
        Insignia = Total == 0 ? string.Empty : $"{Total} rendidos";

        // La lista de examenes se reconstruye entera al refrescar, asi que lo tildado y los
        // conteos del repaso hay que recalcularlos contra las instancias nuevas.
        OnPropertyChanged(nameof(ElegiblesParaRepaso));
        OnPropertyChanged(nameof(HayElegiblesParaRepaso));
        RecalcularRepaso();

        // US-035 y US-033 leen del mismo historial: al cambiar, los dos se rehacen.
        AplicarFiltro();
        RefrescarMaterias();

        if (Total == 0)
        {
            Resumen = "Todavia no rendiste ningun examen.";
            Detalle = "Cuando rindas el primero vas a ver aca tu promedio y tu evolucion.";
            Promedio = Aciertos = MejorNota = "-";
            return;
        }

        Promedio = perfil.PromedioNota.ToString("0.0");
        Aciertos = $"{perfil.PromedioAciertos:0}%";
        MejorNota = perfil.MejorNota.ToString();

        Resumen = $"{Total} examenes rendidos · {perfil.Aprobados} aprobados · {perfil.Aplazos} aplazos";

        Detalle = string.Join(Environment.NewLine, new[]
        {
            $"Preguntas: {perfil.TotalCorrectas} correctas de {perfil.TotalPreguntas}",
            $"Salteadas en total: {perfil.TotalSalteadas}"
        });
    }

    public override void AlEntrar() => Refrescar();

    // ------------------------------------------------------------------
    // US-025 — detalle de un examen del historial
    // ------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayDetalleAbierto))]
    [NotifyPropertyChangedFor(nameof(SePuedeCompartir))]
    [NotifyCanExecuteChangedFor(nameof(CompartirExamenCommand))]
    private ExamenRendido? _examenAbierto;

    public bool HayDetalleAbierto => ExamenAbierto is not null;

    /// <summary>
    /// US-037: solo se puede compartir un examen cuyo detalle se guardo (US-025). Uno de antes
    /// de esa version solo tiene el resumen numerico, y de ahi no sale un examen rendible.
    /// </summary>
    public bool SePuedeCompartir => ExamenAbierto?.TieneDetalle == true;

    /// <summary>
    /// Exporta el examen abierto para pasarselo a un compañero (US-037). Es el mismo servicio
    /// que usa la pantalla de examen: el archivo lleva las preguntas y nada del alumno que lo
    /// rindio — ni su nota, ni que contesto, ni su historial (RN-45).
    /// </summary>
    [RelayCommand(CanExecute = nameof(SePuedeCompartir))]
    private void CompartirExamen()
    {
        if (ExamenAbierto is not ExamenRendido examen)
        {
            return;
        }

        string? destino = _dialogos.ElegirDondeGuardarExamen(
            CompartirExamenService.NombreSugerido(examen.TituloTexto));

        if (string.IsNullOrWhiteSpace(destino))
        {
            return;
        }

        try
        {
            CompartirExamenService.Guardar(CompartirExamenService.Empaquetar(examen), destino);
            _nav.Estado($"Examen exportado: {Path.GetFileName(destino)}");

            _dialogos.Aviso("Examen exportado",
                "Pasale ese archivo a quien quieras y que lo importe desde Nuevo examen. " +
                "No incluye tu nota ni tus respuestas.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Historial.Compartir", ex);
            _dialogos.Error("No se pudo exportar", ex.Message);
        }
    }

    /// <summary>
    /// RN-26: texto que explica por que un examen no tiene detalle. Vacio cuando si lo tiene.
    /// Nunca se deja la lista vacia sin decir nada: una pantalla en blanco se lee como un
    /// error de la app, no como "este examen es viejo".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAvisoSinDetalle))]
    private string _avisoSinDetalle = string.Empty;

    public bool HayAvisoSinDetalle => !string.IsNullOrWhiteSpace(AvisoSinDetalle);

    [ObservableProperty]
    private string _tituloDetalle = string.Empty;

    [ObservableProperty]
    private string _resumenDetalle = string.Empty;

    [RelayCommand]
    private void VerDetalle(ExamenRendido? examen)
    {
        if (examen is null)
        {
            return;
        }

        ExamenAbierto = examen;
        TituloDetalle = examen.TituloTexto;
        ResumenDetalle = $"{examen.FechaTexto} · nota {examen.NotaTexto} · {examen.DetalleTexto}";

        DetallePreguntas.Clear();
        AvisoSinDetalle = string.Empty;

        if (!examen.TieneDetalle)
        {
            // RN-26. No se intenta reconstruir nada (RN-25): las preguntas de aquel intento
            // no se guardaron y no hay de donde sacarlas.
            AvisoSinDetalle =
                "Este examen se rindio con una version anterior de la app, que solo guardaba el " +
                "resultado y no las preguntas. Su detalle no se puede recuperar. Los examenes que " +
                "rindas de ahora en mas si lo van a tener.";
            return;
        }

        foreach (var pregunta in examen.Preguntas)
        {
            // Aca se revela todo, tambien en las falladas. Al corregir en el momento se
            // esconden para que el Modo Revancha sirva; entrar al detalle de un examen viejo
            // es exactamente lo contrario: se viene a ver en que te equivocaste.
            pregunta.RevelarAnalisis = true;
            DetallePreguntas.Add(pregunta);
        }
    }

    [RelayCommand]
    private void CerrarDetalle()
    {
        // Volver a dejar las preguntas como estaban: el examen no se toca al mirarlo
        // (US-025 es de solo lectura).
        foreach (var pregunta in DetallePreguntas)
        {
            pregunta.RevelarAnalisis = false;
        }

        DetallePreguntas.Clear();
        ExamenAbierto = null;
        AvisoSinDetalle = string.Empty;
        TituloDetalle = ResumenDetalle = string.Empty;
    }
    // ------------------------------------------------------------------
    // US-026 / RN-29 — atajo al repaso combinado
    //
    // El punto de entrada del repaso es el asistente de Nuevo examen, y RN-29 es explicita en
    // que lo del Historial "es un atajo al mismo flujo, no una pantalla distinta". Por eso
    // aca NO se arma ningun examen: se tildan los examenes —que es lo natural mientras se
    // navega el historial— y el boton lleva al asistente ya en modo repaso, con lo tildado
    // puesto.
    //
    // Tener el armado duplicado en las dos pantallas fue el primer intento, y era un error:
    // dos copias de la misma logica se separan al primer retoque, y el alumno terminaria
    // viendo dos formularios parecidos que se comportan distinto.
    // ------------------------------------------------------------------

    /// <summary>
    /// Lo levanta el shell para llevar al asistente en modo repaso. La seleccion no viaja en
    /// el evento porque no hace falta: esta marcada en los propios <see cref="ExamenRendido"/>
    /// del historial, que son las mismas instancias que el asistente lista.
    /// </summary>
    public event Action? RepasoPedido;

    /// <summary>
    /// Examenes que pueden aportar preguntas a un repaso: los que tienen detalle guardado y
    /// no son ellos mismos un repaso. Los de antes de US-025 no aparecen tildables porque no
    /// tienen preguntas que prestar, y encadenar repasos de repasos esta fuera de alcance.
    /// </summary>
    public IReadOnlyList<ExamenRendido> ElegiblesParaRepaso =>
        Examenes.Where(e => e.PuedeAlimentarRepaso).ToList();

    /// <summary>
    /// Con menos de dos elegibles no se puede armar nada: la tarjeta se esconde en vez de
    /// quedar visible pero inerte. Es el caso de quien recien empieza, y tambien el de quien
    /// tiene historial pero todo anterior a US-025.
    /// </summary>
    public bool HayElegiblesParaRepaso => ElegiblesParaRepaso.Count >= 2;

    public IReadOnlyList<ExamenRendido> SeleccionadosParaRepaso =>
        ElegiblesParaRepaso.Where(e => e.Seleccionado).ToList();

    public int PreguntasDisponibles => CombinadorDeExamenes.ContarDisponibles(SeleccionadosParaRepaso);

    /// <summary>true con dos o mas examenes tildados: el repaso es, por definicion, combinado.</summary>
    public bool PuedeArmarRepaso => SeleccionadosParaRepaso.Count >= 2 && PreguntasDisponibles > 0;

    public string ResumenRepaso
    {
        get
        {
            int elegidos = SeleccionadosParaRepaso.Count;

            if (elegidos == 0)
            {
                return "Tilda dos o mas examenes de la lista para armar un repaso mezclado.";
            }

            if (elegidos == 1)
            {
                return "Tilda al menos otro examen: el repaso mezcla preguntas de varios.";
            }

            return $"{elegidos} examenes tildados · {PreguntasDisponibles} preguntas para elegir.";
        }
    }

    /// <summary>La llama la vista al tildar o destildar un examen.</summary>
    public void RecalcularRepaso()
    {
        OnPropertyChanged(nameof(SeleccionadosParaRepaso));
        OnPropertyChanged(nameof(PreguntasDisponibles));
        OnPropertyChanged(nameof(PuedeArmarRepaso));
        OnPropertyChanged(nameof(ResumenRepaso));
        IrAlRepasoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Lleva al asistente en modo repaso con lo tildado. No arma el examen: de eso se encarga
    /// el asistente, que es donde vive el flujo (RN-29).
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeArmarRepaso))]
    private void IrAlRepaso()
    {
        CerrarDetalle();
        RepasoPedido?.Invoke();
    }

    [RelayCommand]
    private void DestildarExamenes()
    {
        foreach (var examen in Examenes)
        {
            examen.Seleccionado = false;
        }

        RecalcularRepaso();
    }

    [RelayCommand]
    private void Borrar()
    {
        if (!_dialogos.Confirmar("¿Borrar todo el historial de examenes?\n\nEsta accion no se puede deshacer."))
        {
            return;
        }

        CerrarDetalle();
        _sesion.BorrarHistorial();
        Refrescar();
        _nav.Estado("Historial borrado.");
    }

    // ------------------------------------------------------------------
    // Borrado individual (US-012)
    // ------------------------------------------------------------------

    /// <summary>
    /// Lo cablea el shell: responde true si hay una ronda de revancha en curso del examen
    /// <c>id</c>. Se consulta antes de pedir confirmacion para advertir que al borrar se
    /// descarta esa ronda (AC-T59 / NFR-51).
    /// </summary>
    public Func<string, bool>? HayRevanchaEnCursoDe { get; set; }

    /// <summary>
    /// Se dispara despues de borrar un examen. Lo escucha <c>ExamenViewModel</c> para
    /// descartar el intento/ronda en curso de ese examen sin registrarlo.
    /// </summary>
    public event Action<string>? ExamenBorrado;

    [RelayCommand]
    private async Task BorrarExamen(ExamenRendido? examen)
    {
        if (examen is null)
        {
            return;
        }

        bool revanchaEnCurso = HayRevanchaEnCursoDe?.Invoke(examen.Id) == true;

        string mensaje = revanchaEnCurso
            ? $"Estas rindiendo una revancha de \"{examen.TituloTexto}\".\n\n" +
              "Si borras este examen, esa revancha en curso se descarta sin registrarse.\n\n¿Borrar igual?"
            : $"¿Borrar el examen \"{examen.TituloTexto}\" del historial?\n\nEsta accion no se puede deshacer.";

        if (!_dialogos.Confirmar(mensaje))
        {
            return;
        }

        // RN-28: al borrar el registro se va tambien su detalle de preguntas, porque vive
        // dentro del propio ExamenRendido en perfil.json. Las imagenes las limpia
        // LimpiarImagenesAsync. Un repaso (US-026) ya generado a partir de este examen no se
        // ve afectado: se llevo su propia copia de las preguntas al armarse.
        if (ExamenAbierto?.Id == examen.Id)
        {
            // Si estaba abierto en el detalle, se cierra: dejarlo mostraria las preguntas de
            // un examen que ya no existe.
            CerrarDetalle();
        }

        _sesion.BorrarExamen(examen.Id);
        await LimpiarImagenesAsync(examen.Id);
        ExamenBorrado?.Invoke(examen.Id);
        Refrescar();
        _nav.Estado("Examen borrado del historial.");
    }

    /// <summary>
    /// Borra best-effort la carpeta de imagenes del examen (NFR-50). Un fallo de IO no
    /// puede cortar el borrado: queda anotado en errores.log.
    /// </summary>
    private static Task LimpiarImagenesAsync(string examenId) => Task.Run(() =>
    {
        try
        {
            string carpeta = Path.Combine(RutasApp.Imagenes, examenId);
            if (Directory.Exists(carpeta))
            {
                Directory.Delete(carpeta, recursive: true);
            }
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"Historial.LimpiarImagenes({examenId})", ex);
        }
    });
}
