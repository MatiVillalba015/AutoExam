using System.Collections.ObjectModel;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>
/// Arma la aplicacion: crea las paginas, las conecta entre si por eventos (nunca
/// por referencias directas) y expone la navegacion. La ventana solo enlaza.
/// </summary>
public partial class ShellViewModel : ObservableObject, INavegacion
{
    private readonly BibliotecaService _biblioteca;
    private readonly SesionUsuarioService _sesion;
    private readonly IDialogos _dialogos;

    public ShellViewModel(
        BibliotecaService biblioteca,
        SesionUsuarioService sesion,
        PdfExtractorService pdf,
        GeminiApiService gemini,
        IDialogos dialogos)
    {
        _biblioteca = biblioteca;
        _sesion = sesion;
        _dialogos = dialogos;

        Onboarding = new OnboardingViewModel(sesion, gemini);
        Libros = new BibliotecaViewModel(biblioteca, pdf, gemini, sesion, dialogos, this);
        Asistente = new AsistenteViewModel(biblioteca, pdf, gemini, sesion, dialogos, this);
        Examen = new ExamenViewModel(sesion, dialogos, this);
        Historial = new HistorialViewModel(sesion, dialogos, this);
        Ajustes = new AjustesViewModel(sesion, gemini, dialogos, this);

        Paginas = new ObservableCollection<PaginaViewModel> { Libros, Asistente, Examen, Historial, Ajustes };

        // US-030 / US-031: la navegacion deja de ser una barra pegada al borde y pasa a una
        // grilla de cuatro tarjetas grandes, que desde US-031 son ACCIONES y no secciones.
        //
        // El spec pedia los cuatro botones de navegacion mas cuatro accesos directos, pero
        // "generar examen", "ver examenes anteriores" y "ajustes" llevan exactamente a la
        // misma pantalla que el boton de navegacion homonimo: al pie de la letra quedaban
        // ocho controles con tres pares identicos, justo lo que el ultimo criterio de US-031
        // pide evitar. Resuelto con el usuario a favor de las acciones. Las secciones siguen
        // alcanzables por Ctrl+1..5 (US-004) y por la barra de arriba, y de hecho tres de las
        // cuatro acciones aterrizan igual en su seccion; la unica que hace algo mas es subir
        // material, que es la que el spec describe como distinta ("sin pasar primero por
        // Biblioteca").
        //
        // "Examen" no tiene tarjeta: no es un destino que uno elija, es donde la app te lleva
        // cuando hay un examen para rendir.
        Inicio = new InicioViewModel(new[]
        {
            new AccesoDeInicio(Asistente, "Generar examen", "Wand24",
                "Preguntas nuevas con IA sobre tu material, o un repaso combinando exámenes que ya rendiste.",
                AtajoGenerarExamenCommand),

            new AccesoDeInicio(Libros, "Subir material", "DocumentAdd24",
                "PDF, Word, PowerPoint, Excel o fotos de tus apuntes. Se abre el selector de archivos directo.",
                AtajoSubirMaterialCommand),

            new AccesoDeInicio(Historial, "Exámenes anteriores", "History24",
                "Repasá pregunta por pregunta lo que ya rendiste y mirá tu evolución.",
                AtajoVerHistorialCommand),

            new AccesoDeInicio(Ajustes, "Ajustes", "Settings24",
                "Clave de Gemini, modelo, tema y tamaño de letra del examen.",
                AtajoAjustesCommand)
        });

        // Cableado entre paginas. Cada una ignora que las otras existen.
        Onboarding.Entrar += AlEntrar;
        Onboarding.ModelosDetectados += (modelos, elegido) => Ajustes.PoblarModelos(modelos, elegido);

        Asistente.ExamenGenerado += Examen.Iniciar;
        Asistente.HayExamenSinTerminar = () => Examen.HayIntentoAbierto;
        Examen.HistorialCambio += Historial.Refrescar;

        // US-012: borrar el examen original desde Historial descarta la revancha en curso.
        Historial.HayRevanchaEnCursoDe = id => Examen.HayIntentoAbierto && Examen.RegistroActualId == id;
        Historial.ExamenBorrado += Examen.AlBorrarseExamen;

        // US-026 / RN-29: el Historial es un atajo al mismo flujo, no una pantalla distinta.
        // Tildar ahi y tocar "Armar repaso" lleva al asistente ya en modo repaso, con lo
        // tildado puesto: el armado vive en un solo lugar.
        Historial.RepasoPedido += () =>
        {
            Asistente.EntrarEnModoRepaso();
            IrA(Asistente.Clave);
        };
    }

    public InicioViewModel Inicio { get; }

    public OnboardingViewModel Onboarding { get; }
    public BibliotecaViewModel Libros { get; }
    public AsistenteViewModel Asistente { get; }
    public ExamenViewModel Examen { get; }
    public HistorialViewModel Historial { get; }
    public AjustesViewModel Ajustes { get; }

    public ObservableCollection<PaginaViewModel> Paginas { get; }

    /// <summary>
    /// Config actual, expuesta solo para que MainWindow lea/escriba la geometria de la
    /// ventana (US-003): es la unica forma de que el code-behind llegue a
    /// SesionUsuarioService sin duplicar el acceso a la config en dos lugares.
    /// </summary>
    public AppConfig Config => _sesion.Config;

    // Sin estos dos avisos la barra de seccion queda congelada en lo que hubiera al
    // enlazarse: EnUnaSeccion y TituloSeccion se calculan a partir de Pagina, y una
    // propiedad calculada no notifica sola. El sintoma seria la barra "Inicio / Libros"
    // visible incluso estando en el inicio, sin ningun error en ningun lado.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnUnaSeccion))]
    [NotifyPropertyChangedFor(nameof(TituloSeccion))]
    private PaginaViewModel? _pagina;

    [ObservableProperty]
    private bool _mostrarBienvenida = true;

    [ObservableProperty]
    private string _estadoTexto = string.Empty;

    [ObservableProperty]
    private string _estadoApi = string.Empty;

    partial void OnPaginaChanged(PaginaViewModel? value)
    {
        foreach (var p in Paginas)
        {
            p.EsActual = ReferenceEquals(p, value);
        }

        if (ReferenceEquals(value, Inicio))
        {
            // Se recalcula al entrar y no al arrancar: el menu es lo primero que se ve
            // despues de rendir un examen o de subir material, y tiene que reflejarlo.
            Inicio.Actualizar(_biblioteca.Libros.Count, _sesion.Perfil.Historial);
        }

        value?.AlEntrar();
    }

    /// <summary>true salvo cuando lo que se esta mostrando es la propia pantalla de inicio.</summary>
    public bool EnUnaSeccion => Pagina is not null && !ReferenceEquals(Pagina, Inicio);

    /// <summary>Titulo de la seccion abierta, para la barra de arriba.</summary>
    public string TituloSeccion => EnUnaSeccion ? Pagina!.Titulo : string.Empty;

    /// <summary>
    /// Vuelve a la grilla de inicio. Es lo que hace que el inicio no sea un callejon sin
    /// salida: la eleccion de US-030 cambia la navegacion de "siempre visible al costado" a
    /// "un click de ida y uno de vuelta", asi que la vuelta tiene que estar siempre a mano.
    /// </summary>
    [RelayCommand]
    public void IrAInicio() => Pagina = Inicio;

    // ------------------------------------------------------------------
    // Atajos del menu principal (US-031)
    //
    // RN-36: son atajos a flujos que ya existen. Ninguno de estos cuatro metodos decide
    // nada; todos delegan en la pagina que ya sabe hacer esa tarea. Por eso viven en el
    // shell y no en InicioViewModel: el shell es el unico que conoce a las cuatro paginas,
    // y asi el menu no necesita una referencia a ninguna.
    // ------------------------------------------------------------------

    [RelayCommand]
    private void AtajoGenerarExamen()
    {
        // El asistente conserva el estado entre visitas: sin esto, quien lo dejo en el paso
        // Formato volveria ahi, y el criterio pide el paso Material.
        Asistente.EmpezarDesdeCero();
        IrA(Asistente.Clave);
    }

    /// <summary>
    /// "Subir material nuevo... sin pasos intermedios extra". Es el unico de los cuatro
    /// atajos que hace algo mas que navegar, y es el que justifica que el menu tenga
    /// acciones: el resto de la app no ofrece ninguna otra forma de llegar al selector de
    /// archivos sin entrar antes a Biblioteca o al paso Material del asistente.
    ///
    /// Navega primero y abre el dialogo despues, en ese orden: el archivo que se elija
    /// aparece en la lista de Biblioteca, y si la pantalla siguiera siendo el menu, el alta
    /// ocurriria sin que se vea nada.
    /// </summary>
    [RelayCommand]
    private async Task AtajoSubirMaterialAsync()
    {
        IrA(Libros.Clave);
        await Libros.ElegirArchivoCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void AtajoVerHistorial() => IrA(Historial.Clave);

    [RelayCommand]
    private void AtajoAjustes() => IrA(Ajustes.Clave);

    // ------------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------------
    /// <summary>
    /// Carga biblioteca y config, y arranca la app. <paramref name="trasCargarConfig"/> se
    /// invoca justo despues de <c>_sesion.Cargar()</c> y antes de seguir con el resto
    /// (que incluye pasos async como <see cref="RecuperarHuerfanosAsync"/>): existe para que
    /// <c>MainWindow.Ventana_Loaded</c> pueda restaurar la geometria de la ventana (US-003)
    /// leyendo <see cref="Config"/> ya poblado desde disco, y hacerlo antes de que el resto
    /// de la inicializacion (que puede tardar) demore que la ventana se vea en su posicion
    /// final. Si se llama sin este parametro, el comportamiento es igual al de siempre.
    /// </summary>
    public async Task IniciarAsync(Action? trasCargarConfig = null)
    {
        _biblioteca.Cargar();
        _sesion.Cargar();

        trasCargarConfig?.Invoke();

        // Recien aca se sabe que examenes siguen en el historial, y por eso la limpieza va
        // aca y no en App.OnStartup: sus imagenes tienen que sobrevivir para que el detalle
        // del historial (US-025) y las preguntas con figura (US-018) se sigan viendo
        // completos meses despues.
        RutasApp.LimpiarImagenesAntiguas(_sesion.Perfil.Historial.Select(e => e.Id));

        await RecuperarHuerfanosAsync();

        Ajustes.CargarDesdeConfig();
        Examen.CargarDesdeConfig();
        TemaService.Aplicar(_sesion.Config.TemaOscuro);
        Historial.Refrescar();
        RefrescarEstadoApi();

        Libros.LibroSeleccionado = _biblioteca.Libros.FirstOrDefault();
        Pagina = Inicio;

        Onboarding.Preparar();
        await Onboarding.VerificarGuardadaAsync();
    }

    /// <summary>
    /// Si quedo un PDF en la carpeta interna que ya no figura en el indice, se
    /// vuelve a registrar reconstruyendo titulo y materia desde el historial.
    /// </summary>
    private async Task RecuperarHuerfanosAsync()
    {
        try
        {
            var conocidos = new Dictionary<string, (string Titulo, string Materia)>(StringComparer.OrdinalIgnoreCase);

            foreach (var examen in _sesion.Perfil.Historial.OrderByDescending(h => h.Fecha))
            {
                if (!string.IsNullOrWhiteSpace(examen.LibroId) && !conocidos.ContainsKey(examen.LibroId))
                {
                    conocidos[examen.LibroId] = (examen.LibroTitulo, examen.Materia);
                }
            }

            int recuperados = await _biblioteca.RecuperarHuerfanosAsync(conocidos);

            if (recuperados > 0)
            {
                Estado($"Se recuperaron {recuperados} libro(s) que estaban en la carpeta pero no en el indice.");
            }
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("RecuperarHuerfanos", ex);
        }
    }

    /// <summary>
    /// Se pasa la bienvenida y se entra a la app.
    ///
    /// US-031: se aterriza en el menu principal y no en una seccion. Antes esto empujaba a
    /// Libros o al asistente segun hubiera material o no, porque no habia ningun lugar que
    /// dijera que hacer primero. Ahora si lo hay: el menu muestra las cuatro acciones y, el
    /// primer dia, la invitacion a subir el primer material. Empujar a una seccion ademas
    /// dejaba al menu sin forma de verse al arrancar, que es justo donde los criterios de
    /// US-031 lo describen ("cuando entro al menu principal por primera vez").
    /// </summary>
    private void AlEntrar(string mensaje)
    {
        MostrarBienvenida = false;
        Pagina = Inicio;
        Estado(mensaje);
    }

    /// <summary>Devuelve false si el usuario decide no cerrar.</summary>
    public bool PuedeCerrar()
    {
        if (Examen.HayIntentoAbierto && Examen.Examen?.Preguntas.Count > 0)
        {
            return _dialogos.Confirmar(
                "Hay un examen sin finalizar. Si cerras ahora se pierde el intento.\n\n¿Cerrar de todas formas?");
        }

        return true;
    }

    public void Cerrar()
    {
        Asistente.CancelarSiCorre();
        Examen.Cerrar();
        Libros.GuardarPendiente();

        try
        {
            _biblioteca.Guardar();
            _sesion.GuardarConfig();
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Shell.Cerrar", ex);
        }
    }

    // ------------------------------------------------------------------
    // INavegacion
    // ------------------------------------------------------------------
    /// <summary>
    /// Guardia de <see cref="IrACommand"/> (US-004, defensa en profundidad para NFR-10):
    /// no se ejecuta si el foco esta en un control editable. No afecta llamadas directas
    /// a <see cref="IrA"/> desde otras paginas (por ejemplo Examen/Asistente al terminar),
    /// que son programaticas y no pasan por el comando.
    /// </summary>
    private static bool PuedeIrA(string? clave)
        => System.Windows.Input.Keyboard.FocusedElement is not (
            System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.PasswordBox);

    [RelayCommand(CanExecute = nameof(PuedeIrA))]
    public void IrA(string clave)
    {
        var destino = Paginas.FirstOrDefault(p => p.Clave == clave);
        if (destino is not null)
        {
            Pagina = destino;
        }
    }

    public void Estado(string texto) => EstadoTexto = texto;

    public void RefrescarEstadoApi()
        => EstadoApi = _sesion.HayApiKey ? $"Gemini · {_sesion.Config.Modelo}" : "Gemini · sin API Key";

    /// <summary>
    /// Version instalada, en la barra de estado. Es la unica forma de saber, a simple vista,
    /// si una actualizacion se aplico de verdad: el numero del release y el del binario
    /// pueden no coincidir, y cuando eso pasa la app pide actualizar sin parar.
    /// </summary>
    public string Version => $"v{ActualizacionService.VersionActual}";
}
