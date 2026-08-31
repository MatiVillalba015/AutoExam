using System.Windows;
using System.Windows.Media;

namespace AutoExam.Behaviors;

/// <summary>
/// Compuerta unica de "reducir movimiento" para las animaciones no esenciales de la app
/// (US-011, NFR-47). No hay opcion en Ajustes a proposito: la senial es del sistema
/// operativo ("Mostrar animaciones en Windows", <see cref="SystemParameters.ClientAreaAnimation"/>).
/// Se le suma el tier de render: en Tier 0 (sin aceleracion por hardware) las animaciones
/// cuestan mas de lo que aportan.
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
/// El valor se resuelve una vez, al inicializar el tipo; un cambio de la preferencia del SO
/// se toma en el proximo arranque (igual que "reducir movimiento" en la mayoria de las apps
/// de escritorio).
/// </summary>
public static class Animaciones
{
    /// <summary>
    /// true cuando las animaciones no esenciales deben acortarse/desactivarse: la preferencia
    /// del SO esta apagada, o el equipo no tiene aceleracion de render (Tier 0).
    /// </summary>
    public static bool Reducidas { get; } =
        !SystemParameters.ClientAreaAnimation
        || (RenderCapability.Tier >> 16) == 0;

    /// <summary>
    /// Espejo de <see cref="Reducidas"/> como propiedad adjunta, para usarla como condicion
    /// de un <c>MultiTrigger</c> en XAML. Su valor por defecto (el unico que se usa: nadie la
    /// setea) es <see cref="Reducidas"/>.
    /// </summary>
    public static readonly DependencyProperty MovimientoReducidoProperty =
        DependencyProperty.RegisterAttached(
            "MovimientoReducido", typeof(bool), typeof(Animaciones),
            new PropertyMetadata(Reducidas));

    public static bool GetMovimientoReducido(DependencyObject d) => (bool)d.GetValue(MovimientoReducidoProperty);

    public static void SetMovimientoReducido(DependencyObject d, bool value) => d.SetValue(MovimientoReducidoProperty, value);
}
