using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AutoExam;

namespace AutoExam.Tests.TestSupport;

/// <summary>
/// Infraestructura minima de WPF para probar Views/ExamenView.xaml sin levantar ninguna
/// ventana visible y sin depender de un framework de test STA de terceros.
///
/// Tres problemas resueltos aca, una sola vez para toda la suite:
///
/// 1) Todo objeto WPF (<see cref="DependencyObject"/>, y por lo tanto <see cref="Application"/>,
///    <see cref="ResourceDictionary"/>, <see cref="Style"/>, etc.) tiene afinidad de hilo: solo
///    se puede tocar desde el hilo STA que lo creo. <c>Application.Current</c> ademas es un
///    singleton de todo el proceso (no se puede crear una segunda vez). La unica combinacion que
///    funciona con ambas restricciones a la vez es UN SOLO hilo STA, compartido por todos los
///    tests de esta suite, creado una vez y reusado — no un hilo nuevo por test (que es lo que
///    hacen los "[WpfFact]" de paquetes como Xunit.StaFact, y por lo que se descartaron aca).
///
/// 2) <c>InitializeComponent()</c> de ExamenView resuelve varios <c>StaticResource</c> (estilos
///    de Theme/Estilos.xaml, conversores) al momento de construirse, aunque nunca se haga
///    <c>Show()</c> de nada. Si no hay una <see cref="Application"/> con esos recursos
///    mergeados, tira <c>XamlParseException</c> en el constructor.
///
/// 3) La resolucion de <c>InputBindings</c> de WPF pasa por el foco de teclado real
///    (<c>Keyboard.FocusedElement</c>), que a su vez exige un <see cref="PresentationSource"/>
///    real (no alcanza con pasarle uno descartable a <see cref="KeyEventArgs"/>). Cada llamada a
///    <see cref="RaiseKeyDown"/> crea un <see cref="HwndSource"/> propio (nunca mostrado en
///    pantalla, <c>Width=0 Height=0</c>) con la vista como <c>RootVisual</c> y le pide foco antes
///    de levantar la tecla — reproduce lo que hace <c>ExamenView.xaml.cs</c> en la app real
///    (<c>Focus()</c> al hacerse visible).
/// </summary>
public static class WpfHost
{
    private static readonly object Candado = new();
    private static Dispatcher? _dispatcher;
    private static bool _recursosListos;

    /// <summary>Ejecuta <paramref name="accion"/> en el unico hilo STA compartido de la suite
    /// y relanza cualquier excepcion (asert fallido incluido) en el hilo del test que llama.</summary>
    public static void Invocar(Action accion) => ObtenerDispatcher().Invoke(accion);

    /// <summary>Igual que <see cref="Invocar(Action)"/> pero devolviendo un resultado.</summary>
    public static T Invocar<T>(Func<T> funcion) => ObtenerDispatcher().Invoke(funcion);

    private static Dispatcher ObtenerDispatcher()
    {
        lock (Candado)
        {
            if (_dispatcher is not null)
            {
                return _dispatcher;
            }

            using var listo = new ManualResetEventSlim(false);
            Dispatcher? capturado = null;

            var hiloSta = new Thread(() =>
            {
                capturado = Dispatcher.CurrentDispatcher;
                listo.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "AutoExam.Tests.WpfHost",
            };
            hiloSta.SetApartmentState(ApartmentState.STA);
            hiloSta.Start();

            listo.Wait();
            _dispatcher = capturado;
            return _dispatcher!;
        }
    }

    /// <summary>Crea <see cref="Application.Current"/> si hace falta y le mergea
    /// Theme/Estilos.xaml + los conversores que ExamenView.xaml resuelve por StaticResource.
    /// Idempotente. Debe llamarse SIEMPRE desde dentro de <see cref="Invocar(Action)"/> (ya en
    /// el hilo STA compartido) — nunca directamente desde el hilo del test.</summary>
    public static void AsegurarRecursos()
    {
        if (_recursosListos)
        {
            return;
        }

        if (Application.Current is null)
        {
            _ = new Application();
        }

        var estilos = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/AutoExam;component/Theme/Estilos.xaml", UriKind.Absolute)
        };
        Application.Current!.Resources.MergedDictionaries.Add(estilos);

        // Plantillas compartidas: desde US-025 la tarjeta de correccion de una pregunta vive
        // en su propio diccionario, porque la usan tanto ExamenView como el detalle del
        // historial. Sin mergearlo aca, ExamenView no resuelve "TarjetaCorreccion" y ni
        // siquiera llega a construirse.
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/AutoExam;component/Theme/Plantillas.xaml", UriKind.Absolute)
        });

        // Mismas claves que registra AutoExam/App.xaml.cs (Application.Resources), solo las
        // que ExamenView.xaml realmente resuelve por StaticResource.
        Application.Current.Resources["BoolToVis"] = new BooleanToVisibilityConverter();
        Application.Current.Resources["EstadoAPincel"] = new EstadoAPincelConverter();
        Application.Current.Resources["AprobadoAPincel"] = new AprobadoAPincelConverter();
        Application.Current.Resources["RutaAImagen"] = new RutaAImagenConverter();
        Application.Current.Resources["TextoAVisibilidad"] = new TextoAVisibilidadConverter();

        // US-027: desde que el examen lleva el color de su materia como acento (la pastilla
        // del encabezado y la barra de progreso), ExamenView tambien resuelve este.
        Application.Current.Resources["ColorMateria"] = new ColorMateriaAPincelConverter();

        _recursosListos = true;
    }

    /// <summary>
    /// Simula una tecla llegando al elemento indicado, con foco de teclado real puesto en
    /// <paramref name="raiz"/> (o, si no es Focusable — como ExamenView, ver el comentario en
    /// su XAML —, en el primer descendiente Focusable, que es donde WPF lo redirige solo).
    ///
    /// Requiere un <see cref="PresentationSource"/> de verdad, no uno descartable: la
    /// resolucion real de foco de teclado (<c>UIElement.Focus()</c> / <c>Keyboard.FocusedElement</c>)
    /// solo funciona para elementos conectados a un origen — <c>CommandManager</c> resuelve los
    /// <c>InputBindings</c> de cada ancestro en el camino de burbujeo a partir de ahi, igual que
    /// con una tecla real. Se crea un <see cref="HwndSource"/> nuevo y descartable por llamada
    /// (nunca mostrado en pantalla), con <paramref name="raiz"/> como <c>RootVisual</c>.
    ///
    /// Llamar solo desde dentro de <see cref="Invocar(Action)"/>.
    /// </summary>
    public static bool RaiseKeyDown(FrameworkElement raiz, Key tecla)
    {
        AsegurarRecursos();

        using var origen = new HwndSource(new HwndSourceParameters("AutoExam.Tests.KeyBindings")
        {
            Width = 0,
            Height = 0,
        })
        {
            RootVisual = raiz,
        };

        raiz.Focus();
        var destino = (Keyboard.FocusedElement as UIElement) ?? raiz;

        // Se levanta primero el evento de tunel y despues el de burbujeo, que es el orden real
        // de WPF. Importa desde US-036: los atajos del examen pasaron de KeyBinding a un
        // manejador de PreviewKeyDown —para poder dejar pasar la tecla cuando hay un campo de
        // texto con foco—, y con solo el evento de burbujeo esta suite no los veria disparar.
        var tunel = new KeyEventArgs(Keyboard.PrimaryDevice, origen, 0, tecla)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };

        destino.RaiseEvent(tunel);

        if (tunel.Handled)
        {
            return true;
        }

        var args = new KeyEventArgs(Keyboard.PrimaryDevice, origen, 0, tecla)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
        };

        destino.RaiseEvent(args);
        return args.Handled;
    }
}
