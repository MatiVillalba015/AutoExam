using System.Windows;
using System.Windows.Media;

namespace AutoExam.Behaviors;

/// <summary>
/// Compuerta unica de "reducir movimiento" para las animaciones no esenciales de la app
/// (US-011, NFR-47, US-054). Dos seniales, combinadas con OR (RN-62):
/// <list type="bullet">
///   <item><see cref="PorElSistema"/> — "Mostrar animaciones en Windows"
///   (<see cref="SystemParameters.ClientAreaAnimation"/>) y el tier de render: en Tier 0
///   (sin aceleracion por hardware) las animaciones cuestan mas de lo que aportan.</item>
///   <item><see cref="PorLaApp"/> — el toggle de Ajustes (US-054), que existe para poder
///   apagarlas solo aca sin cambiar la configuracion de todo Windows.</item>
/// </list>
/// El OR es lo que evita dos configuraciones contradictorias: con Windows pidiendo movimiento
/// reducido, apagar el toggle de la app no vuelve a encender las animaciones.
///
/// Dos formas de consumirla, misma fuente de verdad:
/// <list type="bullet">
///   <item><see cref="Reducidas"/> (bool estatico) desde codigo — lo usa
///   <see cref="TransicionContenido"/> para aplicar el estado final sin Storyboard.</item>
///   <item><see cref="MovimientoReducidoProperty"/> (propiedad adjunta) desde XAML — los
///   <c>ControlTemplate</c> de <c>Theme/Estilos.xaml</c> la usan como
///   <c>&lt;Condition Property="comportamientos:Animaciones.MovimientoReducido" Value="False"/&gt;</c>
///   extra en el <c>MultiTrigger</c> de hover/press. Se necesita una propiedad (no un
///   <c>x:Static</c> con <c>Binding</c>) porque un <c>MultiTrigger</c> — a diferencia de un
///   <c>MultiDataTrigger</c> — solo admite condiciones por <c>Property</c>.</item>
/// </list>
/// La propiedad adjunta hereda por el arbol: su valor por defecto es la preferencia del
/// sistema, y <see cref="AplicarEn"/> la setea en la ventana para que el toggle de Ajustes
/// llegue a cada plantilla sin reiniciar. Sin herencia habria que tocar elemento por elemento,
/// y las plantillas de los estilos ni siquiera existen hasta que se instancia el control.
/// </summary>
public static class Animaciones
{
    /// <summary>
    /// true cuando el sistema operativo o el equipo ya piden movimiento reducido: la
    /// preferencia de Windows esta apagada, o no hay aceleracion de render (Tier 0).
    /// Se resuelve una vez, al inicializar el tipo; un cambio de la preferencia del SO se
    /// toma en el proximo arranque, igual que en la mayoria de las apps de escritorio.
    /// </summary>
    public static bool PorElSistema { get; } =
        !SystemParameters.ClientAreaAnimation
        || (RenderCapability.Tier >> 16) == 0;

    /// <summary>El toggle de Ajustes (US-054). Lo escribe <see cref="AplicarEn"/>.</summary>
    public static bool PorLaApp { get; private set; }

    /// <summary>
    /// true cuando las animaciones no esenciales deben acortarse o desactivarse, por
    /// cualquiera de las dos vias.
    /// </summary>
    public static bool Reducidas => PorElSistema || PorLaApp;

    /// <summary>
    /// Espejo de <see cref="Reducidas"/> como propiedad adjunta, para usarla como condicion
    /// de un <c>MultiTrigger</c> en XAML. Hereda por el arbol visual: su default es
    /// <see cref="PorElSistema"/> y la ventana la pisa con el valor combinado.
    /// </summary>
    public static readonly DependencyProperty MovimientoReducidoProperty =
        DependencyProperty.RegisterAttached(
            "MovimientoReducido", typeof(bool), typeof(Animaciones),
            new FrameworkPropertyMetadata(PorElSistema, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetMovimientoReducido(DependencyObject d) => (bool)d.GetValue(MovimientoReducidoProperty);

    public static void SetMovimientoReducido(DependencyObject d, bool value) => d.SetValue(MovimientoReducidoProperty, value);

    /// <summary>
    /// Toma la preferencia de la app y la propaga desde <paramref name="raiz"/> (la ventana)
    /// hacia abajo. Se llama al arrancar y cada vez que se toca el toggle, asi el cambio se
    /// ve al instante en vez de esperar al proximo arranque.
    /// </summary>
    public static void AplicarEn(DependencyObject? raiz, bool reducirPorLaApp)
    {
        PorLaApp = reducirPorLaApp;

        if (raiz is not null)
        {
            SetMovimientoReducido(raiz, Reducidas);
        }
    }

    /// <summary>
    /// Igual que <see cref="AplicarEn"/> pero resolviendo la ventana sola, para que un
    /// ViewModel pueda llamarla sin tener una referencia a la ventana. Es el mismo trato que
    /// <c>TemaService.Aplicar</c>: una preferencia de apariencia que vale para toda la app se
    /// aplica en un solo lugar.
    ///
    /// Si no hay ventana alcanzable desde este hilo —todavia no arranco, o la llamada viene de
    /// un hilo de fondo— igual se guarda la preferencia y no se toca nada mas. Leer
    /// <c>Application.Current.MainWindow</c> desde otro hilo tira, y una preferencia de
    /// apariencia no puede ser el motivo por el que se cae una operacion; el arranque la vuelve
    /// a aplicar sobre la ventana en <c>ShellViewModel.AplicarApariencia</c>.
    /// </summary>
    public static void Aplicar(bool reducirPorLaApp)
    {
        var app = Application.Current;

        bool alcanzable = app is not null && app.Dispatcher.CheckAccess();

        AplicarEn(alcanzable ? app!.MainWindow : null, reducirPorLaApp);
    }
}
