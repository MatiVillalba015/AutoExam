using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AutoExam.Models;
using AutoExam.Services;

namespace AutoExam;

/// <summary>Enlaza un RadioButton a un indice de opcion (0..3) en modo bidireccional.</summary>
public class IndiceOpcionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int indice && parameter is not null && int.TryParse(parameter.ToString(), out int esperado))
        {
            return indice == esperado;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool marcado && marcado && parameter is not null && int.TryParse(parameter.ToString(), out int esperado))
        {
            return esperado;
        }

        return Binding.DoNothing;
    }
}

/// <summary>Visible cuando el texto no esta vacio.</summary>
public class TextoAVisibilidadConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Visible cuando el bool es true (o false, si se pasa "invertir" como parametro).</summary>
public class BoolAVisibilidadConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool valor = value is bool b && b;
        if (string.Equals(parameter as string, "invertir", StringComparison.OrdinalIgnoreCase))
        {
            valor = !valor;
        }

        return valor ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolInvertidoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Traduce el estado de una pregunta al pincel del tema activo. Existe para que
/// ningun color quede escrito en un modelo: la paleta vive solo en Theme/Tokens.
/// El parametro elige que rol se busca: "fondo" o "borde".
/// </summary>
public class EstadoAPincelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool borde = string.Equals(parameter as string, "borde", StringComparison.OrdinalIgnoreCase);

        string clave = value switch
        {
            EstadoPreguntaEnum.Respondida => borde ? "PincelMarca" : "PincelMarcaSuave",
            EstadoPreguntaEnum.Salteada => borde ? "PincelPendiente" : "PincelPendienteSuave",
            ResultadoPreguntaEnum.Correcta => borde ? "PincelAcierto" : "PincelAciertoSuave",
            ResultadoPreguntaEnum.Incorrecta => borde ? "PincelError" : "PincelErrorSuave",
            ResultadoPreguntaEnum.Salteada => borde ? "PincelPendiente" : "PincelPendienteSuave",
            _ => borde ? "PincelBorde" : "PincelSuperficie"
        };

        return Buscar(clave);
    }

    internal static Brush Buscar(string clave)
        => Application.Current?.TryFindResource(clave) as Brush ?? Brushes.Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Pincel de acierto/error para banderas booleanas (nota aprobada, linea correcta).</summary>
public class AprobadoAPincelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool aprobado = value is bool b && b;
        bool suave = string.Equals(parameter as string, "suave", StringComparison.OrdinalIgnoreCase);

        return EstadoAPincelConverter.Buscar(
            aprobado
                ? (suave ? "PincelAciertoSuave" : "PincelAcierto")
                : (suave ? "PincelErrorSuave" : "PincelError"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Codigo de severidad del ViewModel (0..3) al enum de InfoBar de WPF-UI.</summary>
public class SeveridadConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            1 => Wpf.Ui.Controls.InfoBarSeverity.Success,
            2 => Wpf.Ui.Controls.InfoBarSeverity.Warning,
            3 => Wpf.Ui.Controls.InfoBarSeverity.Error,
            _ => Wpf.Ui.Controls.InfoBarSeverity.Informational
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Devuelve true si el valor entero es mayor que cero. Para habilitar acciones.</summary>
public class MayorQueCeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Carga la imagen del disco sin bloquear el archivo.</summary>
public class RutaAImagenConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ImagenUtil.CargarDesdeArchivo(value as string ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
