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

    // ------------------------------------------------------------------
    // US-040 — notas de version
    // ------------------------------------------------------------------

    /// <summary>
    /// Todas las versiones con notas, de la mas nueva a la mas vieja. Salen del CHANGELOG.md
    /// embebido en el ejecutable (RN-51): estan disponibles sin conexion, que es justo cuando
    /// se las quiere leer — despues de que la app se actualizo sola.
    /// </summary>
    public IReadOnlyList<NotasDeUnaVersion> Versiones => NotasDeVersion.Todas;

    [ObservableProperty]
    private bool _mostrarNotas;

    /// <summary>
    /// True cuando la version instalada no tiene entrada en el archivo. El criterio pide
    /// decirlo con claridad en vez de dejar la seccion vacia: pasa en un build de prueba
    /// hecho entre dos releases, y una pantalla en blanco ahi se lee como una falla.
    /// </summary>
    public bool FaltanNotasDeEstaVersion => NotasDeVersion.FaltanLasDeLaInstalada;

    public string AvisoSinNotas => NotasDeVersion.AvisoSinNotas;

    /// <summary>Si hay al menos una version con notas para mostrar.</summary>
    public bool HayNotas => Versiones.Count > 0;

    [RelayCommand]
    private void AlternarNotas() => MostrarNotas = !MostrarNotas;

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

    /// <summary>
    /// Numero de la prueba de conexion en curso. Existe para US-019: si el usuario toca
    /// "Probar conexion" dos veces seguidas, la primera respuesta puede llegar despues de la
    /// segunda y dejar en pantalla un resultado que ya no corresponde al ultimo intento. Cada
    /// prueba se lleva su numero y solo escribe si sigue siendo la ultima.
    /// </summary>
    private int _pruebaEnCurso;

    [RelayCommand]
    private async Task ProbarAsync()
    {
        string clave = PrimeraClave();
        string modelo = string.IsNullOrWhiteSpace(Modelo) ? AppConfig.ModeloPorDefecto : Modelo.Trim();

        int miPrueba = ++_pruebaEnCurso;

        Ocupado = true;
        Avisar(0, "Probando la conexion...", $"Consultando {modelo}. Esto puede tardar unos segundos.");

        try
        {
            var (ok, mensaje) = await _gemini.ProbarConexionAsync(clave, modelo);

            if (miPrueba != _pruebaEnCurso)
            {
                // Llego tarde: ya hay otra prueba mas nueva mandando en pantalla.
                return;
            }

            if (ok)
            {
                Avisar(1, "Conexion exitosa", mensaje);
            }
            else
            {
                // El mensaje del servicio es exacto pero tecnico. El titulo dice en dos palabras
                // que paso —que es lo que el usuario necesita para saber si la clave sirve— y el
                // detalle queda abajo para cuando haga falta.
                var (titulo, motivo) = ClasificarFalla(mensaje);
                Avisar(3, titulo, motivo);
            }
        }
        finally
        {
            if (miPrueba == _pruebaEnCurso)
            {
                Ocupado = false;
            }
        }
    }

    /// <summary>
    /// Traduce el mensaje tecnico de una prueba fallida a un titular en lenguaje simple
    /// (US-019 / RN-16). Publica y estatica para poder probar la clasificacion sin levantar la
    /// pantalla de Ajustes ni tocar la red.
    /// </summary>
    /// <returns>El titulo corto y el detalle que se muestra debajo.</returns>
    public static (string Titulo, string Motivo) ClasificarFalla(string? mensaje)
    {
        string m = mensaje ?? string.Empty;

        bool Dice(params string[] fragmentos) =>
            fragmentos.Any(f => m.Contains(f, StringComparison.OrdinalIgnoreCase));

        // El orden importa: se va de la causa mas concreta a la mas general, porque varios de
        // estos mensajes comparten palabras.
        if (Dice("Falta la API Key", "No hay API Key"))
        {
            return ("Falta la clave", "Pega tu clave de Google Gemini en el campo de arriba y volve a probar.");
        }

        if (Dice("API Key rechazada", "API key not valid", "(401)", "(403)"))
        {
            return ("Clave invalida",
                "Google no acepto esta clave. Revisa que este completa y que el proyecto de " +
                "Google AI Studio tenga habilitada la Generative Language API.\n\n" + m);
        }

        if (Dice("cuota DIARIA"))
        {
            return ("Cuota agotada",
                "Se termino la cuota gratuita del dia para esta clave. Se renueva maniana, o " +
                "podes agregar una segunda clave y AutoExam va a rotar sola.\n\n" + m);
        }

        if (Dice("cuota por minuto", "(429)"))
        {
            return ("Demasiados pedidos seguidos",
                "La clave funciona, pero se paso del limite por minuto. Espera un momento y " +
                "volve a probar.\n\n" + m);
        }

        if (Dice("El modelo no existe", "(404)"))
        {
            return ("Modelo no disponible",
                "La clave anda, pero ese modelo no esta habilitado para ella. Toca \"Detectar\" " +
                "para traer la lista real de modelos que podes usar.\n\n" + m);
        }

        if (Dice("No se pudo contactar", "timeout", "no respondio a tiempo"))
        {
            return ("Sin conexion a internet",
                "No se pudo llegar a los servidores de Google. Revisa tu conexion y volve a probar.\n\n" + m);
        }

        if (Dice("filtros de contenido", "bloqueo"))
        {
            return ("Pedido bloqueado",
                "Google bloqueo hasta un pedido de prueba trivial con esta clave.\n\n" + m);
        }

        return ("No se pudo conectar", m);
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
