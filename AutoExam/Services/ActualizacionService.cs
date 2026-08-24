using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using AutoExam.Models;
using AutoUpdaterDotNET;

namespace AutoExam.Services;

/// <summary>
/// Actualizaciones automaticas contra GitHub Releases, via AutoUpdater.NET.
///
/// La app publica un unico .exe portable, asi que el paquete de actualizacion es un ZIP con
/// ese .exe adentro y AutoUpdater lo descomprime sobre la carpeta de instalacion. Un .exe
/// suelto NO sirve: AutoUpdater lo trataria como instalador y terminaria ejecutando una
/// segunda copia de AutoExam en vez de reemplazar la primera.
/// </summary>
public static class ActualizacionService
{
    /// <summary>
    /// Manifiesto de versiones. Vive en la rama main del repo, no en el Release: asi se puede
    /// corregir un enlace roto con un commit, sin rehacer la publicacion.
    /// </summary>
    public const string UrlManifiesto =
        "https://raw.githubusercontent.com/MatiVillalba015/AutoExam/main/update.xml";

    /// <summary>Nombre del ejecutable dentro del ZIP, relativo a la carpeta de instalacion.</summary>
    private const string Ejecutable = "AutoExam.exe";

    /// <summary>
    /// Cliente para la comprobacion previa del paquete. Timeout corto: esto corre al arrancar
    /// y su unico trabajo es contestar "¿el ZIP esta ahi?"; si tarda demasiado, la respuesta
    /// util es seguir sin molestar al usuario.
    /// </summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    /// <summary>
    /// Intentos que se le conceden a una misma version antes de darla por rota.
    ///
    /// Dos y no uno, porque el primer intento puede haberse quedado a mitad de camino por
    /// algo inocente: el usuario cancelo la descarga, o se corto internet. Al segundo
    /// intento fallido ya no es mala suerte.
    /// </summary>
    private const int MaxIntentosPorVersion = 2;

    private static bool _configurado;

    /// <summary>True si la comprobacion en curso la pidio el usuario desde Ajustes.</summary>
    private static bool _aPedido;

    /// <summary>
    /// Version que se le ofrecio al usuario en la ventana. Se guarda porque el evento de
    /// salida no recibe argumentos y para entonces hay que saber que version se esta
    /// instalando.
    /// </summary>
    private static string _versionOfrecida = string.Empty;

    /// <summary>
    /// Config donde se anota que version se intento instalar. Se inyecta desde el arranque;
    /// sin esto el servicio no puede recordar nada entre reinicios, que es justo lo que hace
    /// falta para cortar un bucle de actualizacion.
    /// </summary>
    private static SesionUsuarioService? _sesion;

    public static void UsarSesion(SesionUsuarioService sesion) => _sesion = sesion;

    /// <summary>Version instalada, para mostrarla en Ajustes.</summary>
    public static string VersionActual
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version;
            return v is null ? "desconocida" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>
    /// Comprobacion silenciosa al arrancar. Si no hay actualizacion, o no hay internet, o el
    /// manifiesto todavia no existe, no se muestra absolutamente nada: quien reciba la app no
    /// tiene por que ver un cartel de error tecnico cada vez que la abre sin conexion.
    /// </summary>
    public static void ComprobarAlIniciar()
    {
        Configurar();

        _aPedido = false;
        AutoUpdater.ReportErrors = false;

        Iniciar();
    }

    /// <summary>
    /// Comprobacion pedida desde Ajustes. Aca si se contesta siempre, incluso "ya estas al
    /// dia" o el error de red: el usuario apreto un boton y merece una respuesta.
    /// </summary>
    public static void ComprobarAhora()
    {
        Configurar();

        _aPedido = true;
        AutoUpdater.ReportErrors = true;

        Iniciar();
    }

    private static void Iniciar()
    {
        try
        {
            AutoUpdater.Start(UrlManifiesto);
        }
        catch (Exception ex)
        {
            // Nunca puede tumbar el arranque: sin actualizaciones la app funciona igual.
            RutasApp.RegistrarError("AutoUpdater.Start", ex);
        }
    }

    private static void Configurar()
    {
        if (_configurado)
        {
            return;
        }

        _configurado = true;

        AutoUpdater.AppTitle = "AutoExam";

        // La version sale del ensamblado (<Version> del .csproj). Se compara contra el
        // <version> del manifiesto, asi que subir uno sin subir el otro no hace nada.
        AutoUpdater.InstalledVersion = Assembly.GetEntryAssembly()?.GetName().Version;

        // Es una app portable en la carpeta del usuario: pedir UAC para reemplazar un .exe
        // que ya se puede escribir solo agrega una pantalla de permisos que asusta.
        AutoUpdater.RunUpdateAsAdmin = false;

        // Ruta del .exe dentro del ZIP, para que sepa que relanzar despues de descomprimir.
        AutoUpdater.ExecutablePath = Ejecutable;

        // No se borra la carpeta antes de extraer: si alguien dejo un PDF al lado del .exe,
        // no es asunto del actualizador hacerlo desaparecer.
        AutoUpdater.ClearAppDirectory = false;

        // Descarga al Temp del usuario en vez de al directorio de la app, que puede estar
        // en Archivos de programa y sin permiso de escritura.
        AutoUpdater.DownloadPath = Path.Combine(Path.GetTempPath(), "AutoExam");

        AutoUpdater.Mandatory = false;
        AutoUpdater.ShowSkipButton = true;
        AutoUpdater.ShowRemindLaterButton = true;
        AutoUpdater.LetUserSelectRemindLater = false;
        AutoUpdater.RemindLaterAt = 1;
        AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;

        // GitHub rechaza peticiones sin User-Agent identificable.
        AutoUpdater.HttpUserAgent = "AutoExam-Updater";

        AutoUpdater.TopMost = true;

        AutoUpdater.CheckForUpdateEvent += AlComprobar;

        // Con este handler enganchado, cerrar la app para aplicar la actualizacion pasa a ser
        // responsabilidad nuestra. Se acepta ese trabajo extra a cambio de la unica senial
        // fiable de que la actualizacion se aplico de verdad.
        AutoUpdater.ApplicationExitEvent += AlSalirParaActualizar;
    }

    /// <summary>
    /// Corre cuando el paquete ya se descargo y AutoUpdater esta por reemplazar el .exe.
    ///
    /// Es el momento correcto para anotar el intento. Antes se anotaba al mostrar la ventana,
    /// y eso contaba como intento cada "Mas tarde" y cada "Omitir": con dos postergaciones, la
    /// deteccion de bucle daba por rota una version que nunca se habia llegado a instalar y
    /// apagaba el aviso automatico, ademas de afirmar en pantalla algo falso.
    /// </summary>
    private static void AlSalirParaActualizar()
    {
        AnotarIntento(_versionOfrecida);

        // El proceso se reinicia enseguida: si no se guarda ahora, no hay otra ocasion.
        Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }

    /// <summary>
    /// Se ejecuta con el resultado de cada comprobacion. Con este handler enganchado,
    /// AutoUpdater ya no muestra su ventana solo: hay que pedirsela, y eso es justo lo que
    /// permite quedarse callado al arrancar y contestar cuando el usuario pregunta.
    /// </summary>
    private static void AlComprobar(UpdateInfoEventArgs args)
    {
        if (args is null || args.Error is not null)
        {
            if (args?.Error is not null)
            {
                RutasApp.RegistrarError("AutoUpdater / comprobar", args.Error);
            }

            if (_aPedido)
            {
                Avisar(
                    "No se pudo comprobar",
                    "No se pudo consultar si hay una version nueva. Revisá tu conexion a internet.",
                    MessageBoxImage.Warning);
            }

            return;
        }

        if (args.IsUpdateAvailable)
        {
            // Decidir si se ofrece implica preguntarle a GitHub si el paquete existe, y este
            // handler puede estar corriendo en el hilo de UI. Hacerlo aca dejaria la ventana
            // congelada hasta que GitHub conteste, que es justo el momento en que el usuario
            // acaba de abrir la app. Se resuelve afuera y se vuelve para mostrar la ventana.
            bool aPedido = _aPedido;

            Task.Run(() =>
            {
                bool ofrecer = PuedeOfrecer(args, out string motivo);

                if (!ofrecer)
                {
                    RutasApp.RegistrarError("AutoUpdater / actualizacion descartada",
                        new InvalidOperationException(motivo));

                    if (aPedido)
                    {
                        Avisar("No se pudo actualizar", motivo, MessageBoxImage.Warning);
                    }

                    return;
                }

                // El intento NO se anota aca. Mostrar la ventana no es intentar actualizar: el
                // usuario todavia puede elegir "Mas tarde" u "Omitir". Se anota en
                // AlSalirParaActualizar, que corre cuando el paquete ya se bajo y la app esta
                // por ser reemplazada, que es lo unico que despues justifica decir "esto ya se
                // instalo y no sirvio".
                _versionOfrecida = args.CurrentVersion;

                // La ventana de AutoUpdater es de WinForms, y como cualquier ventana tiene que
                // abrirse en el hilo de UI. A cambio trae la descarga con barra de progreso,
                // el reinicio y el "recordar mas tarde".
                var ui = Application.Current?.Dispatcher;

                if (ui is null)
                {
                    AutoUpdater.ShowUpdateForm(args);
                }
                else
                {
                    ui.Invoke(() => AutoUpdater.ShowUpdateForm(args));
                }
            });

            return;
        }

        // Se llego a la version anunciada: se limpia el contador para que la proxima
        // actualizacion arranque con sus dos intentos completos.
        OlvidarIntentos();

        if (_aPedido)
        {
            Avisar(
                "AutoExam esta al dia",
                $"Ya tenes la ultima version ({args.InstalledVersion}).",
                MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Corta el bucle de actualizacion.
    ///
    /// AutoUpdater compara el manifiesto contra la version del ENSAMBLADO. Si el ZIP
    /// publicado se compilo antes de subir el numero de version, el binario nuevo sigue
    /// reportando el viejo: la app se actualiza, reinicia, se vuelve a ver desactualizada y
    /// pide actualizar otra vez. Con &lt;mandatory&gt;true&lt;/mandatory&gt; ni siquiera hay
    /// boton para salir.
    ///
    /// Aca se detecta esa situacion por lo unico que la delata sin ambiguedad: ya se intento
    /// instalar esta misma version, y despues de reiniciar la app sigue sin serlo.
    /// </summary>
    private static bool PuedeOfrecer(UpdateInfoEventArgs args, out string motivo)
    {
        motivo = string.Empty;

        // Lo primero, y vale tambien para la comprobacion a pedido: el paquete tiene que
        // existir antes de ofrecerlo. Sin esto, un manifiesto que apunta a un Release que
        // nunca se subio —o que se borro— produce del otro lado lo peor posible: aparece la
        // ventana, el usuario acepta, y la descarga muere con un error tecnico que no le dice
        // nada y que no esta en su mano resolver. Es exactamente lo que le pasa a alguien que
        // recibe la app y no tiene el repositorio delante.
        if (!PaqueteDisponible(args.DownloadURL, out string problemaDelPaquete))
        {
            motivo =
                $"Hay una version {args.CurrentVersion} anunciada, pero su paquete todavia no " +
                $"se puede descargar ({problemaDelPaquete}).\n\n" +
                "Normalmente significa que la publicacion en GitHub quedo a medias. AutoExam " +
                "sigue funcionando con la version que ya tenes.";

            return false;
        }

        // Una comprobacion a pedido siempre se ofrece: si el desarrollador ya corrigio el
        // paquete publicado, este boton tiene que poder traerlo igual.
        if (_aPedido)
        {
            return true;
        }

        var config = _sesion?.Config;
        if (config is null)
        {
            return true;
        }

        if (!EsBucleDeActualizacion(config, args.CurrentVersion))
        {
            return true;
        }

        motivo =
            $"Ya se instalo la actualizacion {args.CurrentVersion} " +
            $"{config.IntentosDeActualizacion} veces y la aplicacion sigue en la version " +
            $"{VersionActual}. El paquete publicado no contiene la version que anuncia, asi que " +
            "instalarlo de nuevo daria lo mismo.\n\n" +
            "No se va a volver a avisar solo. Cuando el paquete este corregido, usá " +
            "\"Buscar actualizaciones\" en Ajustes.";

        return false;
    }

    /// <summary>
    /// Comprueba que el ZIP anunciado se pueda bajar, sin bajarlo.
    ///
    /// Se usa HEAD y, si el servidor no lo admite, un GET pidiendo el primer byte: GitHub
    /// responde a los dos, pero un CDN intermedio puede rechazar HEAD y seria un falso
    /// negativo que dejaria sin actualizaciones a alguien que si podia actualizar.
    ///
    /// Ante un fallo de RED (sin internet, timeout) devuelve true a proposito: el trabajo de
    /// esta funcion es detectar un paquete que no esta publicado, no hacer de test de
    /// conectividad. Si no hay red, la descarga fallara despues con su propio mensaje, que es
    /// el comportamiento de siempre; bloquear la actualizacion por una desconexion pasajera
    /// seria peor que no comprobar nada.
    /// </summary>
    /// <remarks>Publica para poder probarla sin levantar ventanas.</remarks>
    public static bool PaqueteDisponible(string? url, out string motivo)
    {
        motivo = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            motivo = "el manifiesto no trae la direccion del paquete";
            return false;
        }

        try
        {
            var respuesta = Pedir(HttpMethod.Head, url);

            if (respuesta is null || respuesta.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                respuesta = Pedir(HttpMethod.Get, url, primerByteNomas: true);
            }

            if (respuesta is null)
            {
                return true;
            }

            using (respuesta)
            {
                if (respuesta.IsSuccessStatusCode)
                {
                    return true;
                }

                motivo = respuesta.StatusCode == HttpStatusCode.NotFound
                    ? "el archivo no existe en GitHub (404)"
                    : $"GitHub respondio {(int)respuesta.StatusCode}";

                return false;
            }
        }
        catch (Exception ex)
        {
            // Sin red no se puede afirmar que el paquete falte: se deja pasar.
            RutasApp.RegistrarError("AutoUpdater / comprobar paquete", ex);
            return true;
        }
    }

    /// <summary>Pide la URL y devuelve null si el fallo fue de red, no del servidor.</summary>
    private static HttpResponseMessage? Pedir(HttpMethod metodo, string url, bool primerByteNomas = false)
    {
        try
        {
            using var pedido = new HttpRequestMessage(metodo, url);
            pedido.Headers.UserAgent.ParseAdd("AutoExam-Updater");

            if (primerByteNomas)
            {
                // Con Range no se descargan los 60 MB solo para saber si el archivo esta.
                pedido.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            }

            return Http.Send(pedido, HttpCompletionOption.ResponseHeadersRead);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decide si ofrecer <paramref name="versionOfrecida"/> seria repetir un intento que ya
    /// fallo. Separada del resto para poder probarla sin levantar ventanas ni red: es la
    /// regla de la que depende que la app no quede pidiendo la misma actualizacion sin fin.
    /// </summary>
    public static bool EsBucleDeActualizacion(AppConfig config, string? versionOfrecida)
    {
        if (config is null || string.IsNullOrWhiteSpace(versionOfrecida))
        {
            return false;
        }

        bool mismaVersion = string.Equals(
            config.UltimaVersionIntentada, versionOfrecida, StringComparison.Ordinal);

        return mismaVersion && config.IntentosDeActualizacion >= MaxIntentosPorVersion;
    }

    /// <summary>
    /// Anota un intento de instalar <paramref name="version"/>. Publica para poder probar el
    /// conteo, que es la mitad de la deteccion del bucle.
    /// </summary>
    public static void AnotarIntento(AppConfig config, string version)
    {
        if (config is null || string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        config.IntentosDeActualizacion = string.Equals(
            config.UltimaVersionIntentada, version, StringComparison.Ordinal)
            ? config.IntentosDeActualizacion + 1
            : 1;

        config.UltimaVersionIntentada = version;
    }

    private static void AnotarIntento(string version)
    {
        var config = _sesion?.Config;
        if (config is null)
        {
            return;
        }

        // Se anota ANTES de lanzar la actualizacion: el proceso se reinicia en el medio, asi
        // que despues ya no habria ocasion de guardar nada.
        AnotarIntento(config, version);

        Guardar();
    }

    private static void OlvidarIntentos()
    {
        var config = _sesion?.Config;

        if (config is null || (config.UltimaVersionIntentada.Length == 0 && config.IntentosDeActualizacion == 0))
        {
            return;
        }

        config.UltimaVersionIntentada = string.Empty;
        config.IntentosDeActualizacion = 0;

        Guardar();
    }

    private static void Guardar()
    {
        try
        {
            _sesion?.GuardarConfig();
        }
        catch (Exception ex)
        {
            // No poder anotar el intento no puede romper la app; como mucho se pierde el
            // corte del bucle, que es exactamente donde estabamos antes.
            RutasApp.RegistrarError("AutoUpdater / guardar intento", ex);
        }
    }

    private static void Avisar(string titulo, string mensaje, MessageBoxImage icono)
    {
        // La comprobacion corre fuera del hilo de UI: sin este salto, el MessageBox
        // aparece sin dueño y puede quedar detras de la ventana principal.
        Application.Current?.Dispatcher.Invoke(() =>
            MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, icono));
    }
}
