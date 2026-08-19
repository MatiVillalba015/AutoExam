using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>
/// Primera pantalla: comprobar que la clave sirve antes de dejar entrar. Verificar
/// aca evita que el primer error aparezca recien cuando el usuario ya cargo un
/// libro y espero dos minutos por un examen.
/// </summary>
public partial class OnboardingViewModel : ObservableObject
{
    private readonly SesionUsuarioService _sesion;
    private readonly GeminiApiService _gemini;

    /// <summary>Se dispara cuando hay que abrir la app: lleva el mensaje para la barra de estado.</summary>
    public event Action<string>? Entrar;

    /// <summary>Modelos que resultaron habilitados, para que Ajustes no vuelva a preguntar.</summary>
    public event Action<List<string>, string>? ModelosDetectados;

    public OnboardingViewModel(SesionUsuarioService sesion, GeminiApiService gemini)
    {
        _sesion = sesion;
        _gemini = gemini;
    }

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerificarCommand))]
    private bool _verificando;

    [ObservableProperty]
    private string _paso = string.Empty;

    [ObservableProperty]
    private string _mensajeTitulo = string.Empty;

    [ObservableProperty]
    private string _mensaje = string.Empty;

    [ObservableProperty]
    private bool _hayMensaje;

    public void Preparar()
    {
        ApiKey = _sesion.Config.ClavesComoTexto;

        if (_sesion.ModeloMigradoDesde is string viejo)
        {
            Avisar("Se actualizo el modelo", $"\"{viejo}\" ya fue retirado por Google. Se va a usar uno vigente.");
        }
    }

    /// <summary>Con una clave ya guardada la verificacion arranca sola y entra derecho.</summary>
    public async Task VerificarGuardadaAsync()
    {
        if (_sesion.HayApiKey)
        {
            await VerificarAsync();
        }
    }

    private bool PuedeVerificar() => !Verificando;

    [RelayCommand(CanExecute = nameof(PuedeVerificar))]
    private async Task VerificarAsync()
    {
        string clave = PrimeraClave();

        if (string.IsNullOrWhiteSpace(clave))
        {
            Avisar("Falta la API Key", "Pegala arriba para poder generar examenes.");
            return;
        }

        Verificando = true;
        HayMensaje = false;

        try
        {
            Paso = "Buscando los modelos de tu clave...";
            var modelos = await _gemini.ListarModelosAsync(clave);

            if (modelos.Count == 0)
            {
                throw new GeminiException(
                    "La clave es valida pero no expone ningun modelo de texto. Revisa en Google AI " +
                    "Studio que el proyecto tenga habilitada la Generative Language API.");
            }

            // Si Google devolvio la lista, la clave ya quedo probada: se guarda aca y
            // no despues, para no perderla si la prueba del modelo llega a fallar.
            _sesion.Config.EstablecerClaves(ApiKey);
            _sesion.GuardarConfig();

            string modelo = modelos.Contains(_sesion.Config.Modelo, StringComparer.OrdinalIgnoreCase)
                ? _sesion.Config.Modelo
                : AjustesViewModel.ElegirRecomendado(modelos);

            Paso = $"Probando {modelo}...";
            var (ok, mensaje) = await _gemini.ProbarConexionAsync(clave, modelo);

            if (!ok)
            {
                throw new GeminiException(mensaje);
            }

            _sesion.Config.Modelo = modelo;
            _sesion.GuardarConfig();

            ModelosDetectados?.Invoke(modelos, modelo);
            Entrar?.Invoke($"Conectado con {modelo}.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("VerificarYEntrar", ex);
            Avisar(_sesion.HayApiKey ? "La clave guardada ya no funciona" : "No se pudo verificar la clave", ex.Message);
        }
        finally
        {
            Verificando = false;
            Paso = string.Empty;
        }
    }

    /// <summary>Valvula de escape: nadie tiene que quedar trabado en la primera pantalla.</summary>
    [RelayCommand]
    private void Omitir()
    {
        string clave = PrimeraClave();

        if (!string.IsNullOrWhiteSpace(clave))
        {
            _sesion.Config.EstablecerClaves(ApiKey);
            _sesion.GuardarConfig();
        }

        Entrar?.Invoke("Entraste sin verificar la clave.");
    }

    /// <summary>
    /// La primera clave del campo, ya limpia. Se admite pegar varias de una: se verifica
    /// esta y se guardan todas, que es lo que despues permite rotar ante un 429.
    /// </summary>
    private string PrimeraClave() => GeminiApiService.NormalizarApiKey(
        AppConfig.SepararClaves(ApiKey).FirstOrDefault() ?? string.Empty);

    private void Avisar(string titulo, string mensaje)
    {
        MensajeTitulo = titulo;
        Mensaje = mensaje;
        HayMensaje = true;
    }
}
