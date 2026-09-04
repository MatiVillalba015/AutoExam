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

/// <summary>
/// true si los dos valores enlazados son el mismo texto (ignorando mayusculas).
///
/// Existe para marcar el chip de la materia elegida (US-023): la comparacion es contra una
/// propiedad del ViewModel, no contra una constante, y ConverterParameter no admite un
/// Binding — de ahi que sea un multi-converter y no uno simple.
/// </summary>
public class SonIgualesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not { Length: 2 })
        {
            return false;
        }

        return string.Equals(values[0] as string, values[1] as string, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte el color de una materia (texto "#RRGGBB", US-027) en un pincel.
///
/// Los pinceles se cachean porque el mismo color se pide una vez por cada tarjeta del
/// historial y por cada libro de la biblioteca: crear un SolidColorBrush nuevo en cada
/// binding llenaria de objetos el arbol visual sin ninguna ganancia.
///
/// Con ConverterParameter="suave" devuelve el mismo tono translucido, para fondos de
/// encabezado de grupo donde el color pleno taparia el texto.
/// </summary>
public class ColorMateriaAPincelConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string texto = value as string ?? string.Empty;

        if (string.IsNullOrWhiteSpace(texto))
        {
            texto = PaletaMaterias.Neutro;
        }

        bool suave = string.Equals(parameter as string, "suave", StringComparison.OrdinalIgnoreCase);
        string clave = suave ? texto + "|suave" : texto;

        if (Cache.TryGetValue(clave, out var cacheado))
        {
            return cacheado;
        }

        SolidColorBrush pincel;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(texto);

            if (suave)
            {
                // Alfa bajo en vez de mezclar contra un fondo fijo: asi el mismo pincel
                // funciona en tema claro y en oscuro sin calcular dos variantes.
                color.A = 38;
            }

            pincel = new SolidColorBrush(color);
        }
        catch (FormatException)
        {
            // Un color escrito a mano en materias.json que no parsea no puede tumbar el
            // dibujado de la lista entera.
            pincel = new SolidColorBrush(Colors.Gray);
        }

        pincel.Freeze();
        Cache[clave] = pincel;

        return pincel;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Pincel del color de una materia a partir de su NOMBRE (US-027 / RN-30).
///
/// Existe aparte de <see cref="ColorMateriaAPincelConverter"/> porque hay un lugar donde no
/// se tiene el color a mano: los encabezados de grupo de la biblioteca, cuyo DataContext es
/// un <c>CollectionViewGroup</c> y lo unico que expone es el nombre por el que se agrupo.
/// Resolver el nombre contra la paleta es exactamente lo que pide RN-30: el color se busca
/// al dibujar, no se copia en cada item.
/// </summary>
public class NombreDeMateriaAPincelConverter : IValueConverter
{
    private static readonly ColorMateriaAPincelConverter Interno = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Interno.Convert(PaletaMaterias.ColorDe(value as string), targetType, parameter, culture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// La serie de una materia convertida en la polilinea del grafico de evolucion (US-033).
///
/// Toma la evolucion mas el ancho y el alto REALES del area de dibujo. Escalar con el tamanio
/// verdadero y no con un Viewbox es lo que evita que el grafico se deforme: un Viewbox que
/// estira un lienzo cuadrado a un rectangulo ancho tambien estira el grosor de la linea y
/// convierte los circulos de cada intento en ovalos.
///
/// El eje Y va invertido a proposito: en pantalla el 0 esta arriba, y una nota mas alta tiene
/// que dibujarse mas arriba.
/// </summary>
public class EvolucionAPolilineaConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var puntos = new PointCollection();

        if (values.Length < 3 ||
            values[0] is not EvolucionMateria evolucion ||
            values[1] is not double ancho || values[2] is not double alto ||
            ancho <= 0 || alto <= 0)
        {
            return puntos;
        }

        foreach (var (x, y) in evolucion.Relativos())
        {
            puntos.Add(new Point(x * ancho, (1 - y) * alto));
        }

        return puntos;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Una fraccion 0..1 llevada a pixeles sobre un largo dado, para posicionar los marcadores de
/// cada intento sobre el grafico (US-033).
///
/// Con ConverterParameter="invertir" ademas da vuelta el eje (para el vertical, donde 1 es
/// arriba), y con un numero como parametro le resta ese tanto — es como se centra un circulo
/// sobre su punto en vez de colgarlo de la esquina.
/// </summary>
public class FraccionAPixelConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double fraccion || values[1] is not double largo || largo <= 0)
        {
            return 0d;
        }

        string opciones = parameter as string ?? string.Empty;
        bool invertir = opciones.Contains("invertir", StringComparison.OrdinalIgnoreCase);

        double centrado = 0;
        foreach (string parte in opciones.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(parte.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
            {
                centrado = n;
            }
        }

        double valor = invertir ? (1 - fraccion) * largo : fraccion * largo;

        return valor - centrado;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
