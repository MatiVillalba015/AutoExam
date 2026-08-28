using System.Windows;
using System.Windows.Input;

namespace AutoExam.Behaviors;

/// <summary>
/// Suple la ausencia de ButtonBase.IsPressed en controles que no derivan de ButtonBase.
/// Lo necesita puntualmente el estilo ItemLibro (Theme/Estilos.xaml), aplicado sobre
/// ListBoxItem: ese tipo no tiene una nocion nativa de "presionado" utilizable en un
/// MultiTrigger, a diferencia de los otros 6 estilos de US-008 (ToggleButton/Button/
/// RadioButton, todos ButtonBase). Expone EstaPresionado como propiedad adjunta de solo
/// lectura, actualizada con los eventos de mouse ya nativos de WPF, para que el
/// ControlTemplate la consuma en un MultiTrigger igual que IsPressed en los demas.
/// </summary>
public static class Presionable
{
    public static readonly DependencyProperty RastrearProperty =
        DependencyProperty.RegisterAttached(
            "Rastrear", typeof(bool), typeof(Presionable), new PropertyMetadata(false, AlCambiarRastrear));

    private static readonly DependencyPropertyKey EstaPresionadoPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "EstaPresionado", typeof(bool), typeof(Presionable), new PropertyMetadata(false));

    public static readonly DependencyProperty EstaPresionadoProperty = EstaPresionadoPropertyKey.DependencyProperty;

    public static bool GetRastrear(DependencyObject d) => (bool)d.GetValue(RastrearProperty);
    public static void SetRastrear(DependencyObject d, bool valor) => d.SetValue(RastrearProperty, valor);

    public static bool GetEstaPresionado(DependencyObject d) => (bool)d.GetValue(EstaPresionadoProperty);
    private static void SetEstaPresionado(DependencyObject d, bool valor) => d.SetValue(EstaPresionadoPropertyKey, valor);

    private static void AlCambiarRastrear(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement elemento)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            elemento.PreviewMouseLeftButtonDown += AlPresionar;
            elemento.PreviewMouseLeftButtonUp += AlSoltar;
            elemento.MouseLeave += AlSoltar;
            elemento.LostMouseCapture += AlSoltar;
        }
        else
        {
            elemento.PreviewMouseLeftButtonDown -= AlPresionar;
            elemento.PreviewMouseLeftButtonUp -= AlSoltar;
            elemento.MouseLeave -= AlSoltar;
            elemento.LostMouseCapture -= AlSoltar;
        }
    }

    private static void AlPresionar(object sender, MouseButtonEventArgs e)
    {
        // WPF fuerza IsHitTestVisible=False cuando IsEnabled=False, asi que este evento no
        // deberia llegar nunca con el control deshabilitado; el chequeo es una guardia
        // barata adicional, no la unica linea de defensa (esa es el MultiTrigger consumidor).
        if (sender is UIElement { IsEnabled: true } elemento)
        {
            SetEstaPresionado(elemento, true);
        }
    }

    private static void AlSoltar(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement elemento)
        {
            SetEstaPresionado(elemento, false);
        }
    }
}
