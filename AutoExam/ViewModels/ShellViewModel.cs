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
        Libros = new BibliotecaViewModel(biblioteca, pdf, dialogos, this);
        Asistente = new AsistenteViewModel(biblioteca, pdf, gemini, sesion, dialogos, this);
        Examen = new ExamenViewModel(sesion, dialogos, this);
        Historial = new HistorialViewModel(sesion, dialogos, this);
        Ajustes = new AjustesViewModel(sesion, gemini, dialogos, this);

        Paginas = new ObservableCollection<PaginaViewModel> { Libros, Asistente, Examen, Historial, Ajustes };

        // Cableado entre paginas. Cada una ignora que las otras existen.
        Onboarding.Entrar += AlEntrar;
        Onboarding.ModelosDetectados += (modelos, elegido) => Ajustes.PoblarModelos(modelos, elegido);

        Asistente.ExamenGenerado += Examen.Iniciar;
        Asistente.HayExamenSinTerminar = () => Examen.HayIntentoAbierto;
        Examen.HistorialCambio += Historial.Refrescar;
    }

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

    [ObservableProperty]
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

        value?.AlEntrar();
    }

    // ------------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------------
    public async Task IniciarAsync()
    {
        _biblioteca.Cargar();
        _sesion.Cargar();

        await RecuperarHuerfanosAsync();

        Ajustes.CargarDesdeConfig();
        Examen.CargarDesdeConfig();
        TemaService.Aplicar(_sesion.Config.TemaOscuro);
        Historial.Refrescar();
        RefrescarEstadoApi();

        Libros.LibroSeleccionado = _biblioteca.Libros.FirstOrDefault();
        Pagina = Libros;

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

    private void AlEntrar(string mensaje)
    {
        MostrarBienvenida = false;
        Pagina = _biblioteca.Libros.Count == 0 ? Libros : Asistente;
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
