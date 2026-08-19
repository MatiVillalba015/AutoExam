using System.Collections.ObjectModel;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>Clave, modelo y las perillas finas de la generacion.</summary>
public partial class AjustesViewModel : PaginaViewModel
{
    private readonly SesionUsuarioService _sesion;
    private readonly GeminiApiService _gemini;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _nav;

    public AjustesViewModel(
        SesionUsuarioService sesion, GeminiApiService gemini, IDialogos dialogos, INavegacion nav)
        : base("ajustes", "Ajustes", "Settings24")
    {
        _sesion = sesion;
        _gemini = gemini;
        _dialogos = dialogos;
        _nav = nav;
    }

    public ObservableCollection<string> Modelos { get; } = new();

    public string CarpetaDatos => RutasApp.Raiz;

    /// <summary>Version instalada, para saber contra que se compara el manifiesto de GitHub.</summary>
    public string VersionActual => ActualizacionService.VersionActual;

    /// <summary>
    /// Comprobacion a pedido. La automatica del arranque se calla si no hay nada nuevo;
    /// esta contesta siempre, porque el usuario apreto un boton y espera una respuesta.
    /// </summary>
    [RelayCommand]
    private void BuscarActualizacion() => ActualizacionService.ComprobarAhora();

    /// <summary>
    /// Las claves tal como las escribe el usuario: una por linea, o separadas por comas.
    /// Es un solo campo de texto a proposito; una grilla para tres claves seria mas UI que
    /// la que el problema justifica.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenClaves))]
    private string _apiKey = string.Empty;

    /// <summary>Cuenta las claves cargadas para que se vea que la rotacion tiene con que trabajar.</summary>
    public string ResumenClaves
    {
        get
        {
            int cuantas = AppConfig.SepararClaves(ApiKey).Count;

            return cuantas switch
            {
                0 => "Sin claves cargadas.",
                1 => "1 clave. Con una sola, al agotarse la cuota diaria hay que esperar al otro dia.",
                _ => $"{cuantas} claves. Si una agota su cuota, AutoExam sigue con la siguiente sin cortar el examen."
            };
        }
    }

    [ObservableProperty]
    private string _modelo = AppConfig.ModeloPorDefecto;

    [ObservableProperty]
    private int _preguntasPorLote = 15;

    [ObservableProperty]
    private int _paginasPorBloque = 15;

    [ObservableProperty]
    private int _maxCaracteres = 90_000;

    [ObservableProperty]
    private int _maxImagenes = 12;

    [ObservableProperty]
    private bool _incluirImagenes = true;

    [ObservableProperty]
    private bool _temaOscuro = true;

    [ObservableProperty]
    private bool _ocupado;

    [ObservableProperty]
    private string _mensajeTitulo = string.Empty;

    [ObservableProperty]
    private string _mensaje = string.Empty;

    /// <summary>0 = sin mensaje · 1 = ok · 2 = aviso · 3 = error.</summary>
    [ObservableProperty]
    private int _severidad;

    public void CargarDesdeConfig()
    {
        var c = _sesion.Config;

        ApiKey = c.ClavesComoTexto;
        PreguntasPorLote = c.PreguntasPorLote;
        PaginasPorBloque = c.PaginasPorBloque;
        MaxCaracteres = c.MaxCaracteresContexto;
        MaxImagenes = c.MaxImagenesPorExamen;
        IncluirImagenes = c.IncluirImagenes;
        TemaOscuro = c.TemaOscuro;

        PoblarModelos(AppConfig.ModelosSugeridos, c.Modelo);
    }

    public void PoblarModelos(IEnumerable<string> modelos, string elegido)
    {
        var lista = modelos.ToList();

        if (!string.IsNullOrWhiteSpace(elegido) && !lista.Contains(elegido, StringComparer.OrdinalIgnoreCase))
        {
            lista.Insert(0, elegido);
        }

        Modelos.Clear();
        foreach (var m in lista)
        {
            Modelos.Add(m);
        }

        Modelo = elegido;
    }

    partial void OnTemaOscuroChanged(bool value)
    {
        TemaService.Aplicar(value);

        // El tema se guarda solo: nadie espera tener que apretar "Guardar"
        // despues de cambiar de claro a oscuro.
        _sesion.Config.TemaOscuro = value;
        _sesion.GuardarConfig();
    }

    [RelayCommand]
    private void Guardar()
    {
        var c = _sesion.Config;

        c.EstablecerClaves(ApiKey);
        c.Modelo = string.IsNullOrWhiteSpace(Modelo) ? AppConfig.ModeloPorDefecto : Modelo.Trim();
        c.PreguntasPorLote = Math.Clamp(PreguntasPorLote, 5, 15);
        c.PaginasPorBloque = Math.Clamp(PaginasPorBloque, 5, 40);
        c.MaxCaracteresContexto = Math.Clamp(MaxCaracteres, 10_000, 300_000);
        c.MaxImagenesPorExamen = Math.Clamp(MaxImagenes, 0, 30);
        c.IncluirImagenes = IncluirImagenes;
        c.TemaOscuro = TemaOscuro;

        _sesion.GuardarConfig();
        _nav.RefrescarEstadoApi();

        Avisar(1, "Ajustes guardados", RutasApp.ArchivoConfig);
        _nav.Estado("Ajustes guardados.");
    }

    /// <summary>
    /// Trae de Google la lista real de modelos habilitados para esta clave. Es la
    /// salida cuando Google retira una generacion y la app deja de generar examenes.
    /// </summary>
    [RelayCommand]
    private async Task DetectarAsync()
    {
        string clave = PrimeraClave();

        if (string.IsNullOrWhiteSpace(clave))
        {
            Avisar(2, "Falta la API Key", "Pega primero tu clave de Gemini.");
            return;
        }

        Ocupado = true;
        Avisar(0, "Consultando a Google...", "Pidiendo los modelos habilitados para tu clave.");

        try
        {
            var modelos = await _gemini.ListarModelosAsync(clave);

            if (modelos.Count == 0)
            {
                Avisar(2, "Sin modelos de texto",
                    "La clave es valida pero no expone ningun modelo con generateContent. " +
                    "Revisa en Google AI Studio que el proyecto tenga la API habilitada.");
                return;
            }

            string actual = Modelo.Trim();
            bool sigueVivo = modelos.Contains(actual, StringComparer.OrdinalIgnoreCase);
            string elegido = sigueVivo ? actual : ElegirRecomendado(modelos);

            PoblarModelos(modelos, elegido);

            _sesion.Config.Modelo = elegido;
            _sesion.GuardarConfig();
            _nav.RefrescarEstadoApi();

            Avisar(1, $"{modelos.Count} modelos disponibles",
                sigueVivo
                    ? $"Lista actualizada. Modelo en uso: {elegido}."
                    : $"El modelo anterior ya no existe. Se selecciono y guardo: {elegido}.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("DetectarModelos", ex);
            Avisar(3, "No se pudieron listar los modelos", ex.Message);
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// La primera clave del campo. "Detectar modelos" y "Probar conexion" hablan de una
    /// clave concreta, no del juego entero: consultarlas todas gastaria cuota de cada una
    /// para responder la misma pregunta.
    /// </summary>
    private string PrimeraClave() => AppConfig.SepararClaves(ApiKey).FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// Prefiere un flash estable. Delega en el servicio para que la eleccion de modelo sea
    /// una sola regla: la de Ajustes y la del rescate automatico ante un 404 tienen que
    /// terminar en el mismo modelo, o el usuario veria uno en pantalla y otro generando.
    /// </summary>
    public static string ElegirRecomendado(List<string> modelos) => GeminiApiService.ElegirFlash(modelos);

    [RelayCommand]
    private async Task ProbarAsync()
    {
        string clave = PrimeraClave();
        string modelo = string.IsNullOrWhiteSpace(Modelo) ? AppConfig.ModeloPorDefecto : Modelo.Trim();

        Ocupado = true;
        Avisar(0, "Probando...", $"Consultando {modelo}.");

        try
        {
            var (ok, mensaje) = await _gemini.ProbarConexionAsync(clave, modelo);
            Avisar(ok ? 1 : 3, ok ? "Conexion correcta" : "No se pudo conectar", mensaje);
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void AbrirCarpeta()
    {
        try
        {
            _dialogos.AbrirCarpeta(RutasApp.Raiz);
        }
        catch (Exception ex)
        {
            Avisar(3, "No se pudo abrir la carpeta", ex.Message);
        }
    }

    private void Avisar(int severidad, string titulo, string mensaje)
    {
        Severidad = severidad;
        MensajeTitulo = titulo;
        Mensaje = mensaje;
    }
}
