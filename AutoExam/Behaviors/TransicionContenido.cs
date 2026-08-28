using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AutoExam.Behaviors;

/// <summary>
/// Anima con un fundido + deslizamiento sutil cada vez que cambia el Content
/// de un ContentControl. Se usa en el shell para que pasar de una pagina a
/// otra no sea un corte seco.
///
/// No se implementa como un EventTrigger de WPF porque ContentControl no
/// expone un evento de "contenido cambiado": hay que escuchar el
/// DependencyProperty directamente con un DependencyPropertyDescriptor.
/// </summary>
public static class TransicionContenido
{
    public static readonly DependencyProperty ActivaProperty =
        DependencyProperty.RegisterAttached(
            "Activa", typeof(bool), typeof(TransicionContenido), new PropertyMetadata(false, AlCambiarActiva));

    public static bool GetActiva(DependencyObject d) => (bool)d.GetValue(ActivaProperty);
    public static void SetActiva(DependencyObject d, bool valor) => d.SetValue(ActivaProperty, valor);

    private static readonly DependencyPropertyDescriptor DescriptorContenido =
        DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));

    private static void AlCambiarActiva(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ContentControl control)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            control.RenderTransform = new TranslateTransform();
            DescriptorContenido.AddValueChanged(control, AlCambiarContenido);
        }
        else
        {
            DescriptorContenido.RemoveValueChanged(control, AlCambiarContenido);
        }
    }

    private static void AlCambiarContenido(object? sender, EventArgs e)
    {
        if (sender is not ContentControl control || control.Content is null)
        {
            return;
        }

        if (control.RenderTransform is not TranslateTransform desplazamiento)
        {
            desplazamiento = new TranslateTransform();
            control.RenderTransform = desplazamiento;
        }

        var duracion = ObtenerDuracion();
        var suavizado = ObtenerSuavizado();

        control.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duracion) { EasingFunction = suavizado });

        desplazamiento.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, duracion) { EasingFunction = suavizado });
    }

    // Duracion/easing de la transicion viven en Theme/Estilos.xaml (DuracionTransicionSeccion,
    // SuavizadoSalida) para que todo timing de animacion de la app tenga una sola fuente de
    // verdad. Si el recurso no aparece (tema aun no cargado, o test fuera de contexto de
    // Application), se cae a los mismos valores que traia el intento original hardcodeado.
    private static readonly Duration DuracionFallback = new(TimeSpan.FromMilliseconds(220));

    private static Duration ObtenerDuracion()
    {
        if (Application.Current?.TryFindResource("DuracionTransicionSeccion") is Duration duracion)
        {
            return duracion;
        }

        return DuracionFallback;
    }

    private static IEasingFunction ObtenerSuavizado()
    {
        if (Application.Current?.TryFindResource("SuavizadoSalida") is IEasingFunction suavizado)
        {
            return suavizado;
        }

        return new QuadraticEase { EasingMode = EasingMode.EaseOut };
    }
}
