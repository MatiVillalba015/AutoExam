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
    {
        bool hayTexto = !string.IsNullOrWhiteSpace(value as string);

        // "invertir" muestra el elemento cuando el texto esta VACIO: es como se dibuja el
        // hueco de un dato que todavia no se eligio (el panel "Tu examen" de US-058). Mismo
        // parametro que ya usa BoolAVisibilidadConverter, para no tener dos convenciones. Sin
        // parametro se comporta igual que siempre.
        bool invertir = string.Equals(parameter as string, "invertir", StringComparison.OrdinalIgnoreCase);

        return hayTexto != invertir ? Visibility.Visible : Visibility.Collapsed;
    }

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
        string modo = parameter as string ?? string.Empty;

        bool borde = string.Equals(modo, "borde", StringComparison.OrdinalIgnoreCase);

        // "rotulo" (US-057): el color con el que se ESCRIBE el nombre del estado, y con el que
        // se tiñe su tarjeta. Es igual a "borde" salvo en pendiente/salteada, donde los dos
        // tonos estan invertidos respecto de acierto y error: en los dos temas, el legible
        // sobre la superficie es "PincelPendienteSuave" y no "PincelPendiente" (ver el comentario
        // de la seccion semantica en Tokens.Oscuro.xaml y Tokens.Claro.xaml).
        //
        // Sin esta distincion, "Salteada / Pendiente" se escribia en #3E2C02 sobre una tarjeta
        // oscura y quedaba practicamente invisible — lo que ya pasaba antes de US-057, cuando el
        // mismo color pintaba tambien la franja lateral.
        bool rotulo = string.Equals(modo, "rotulo", StringComparison.OrdinalIgnoreCase);

        bool fuerte = borde || rotulo;

        string clave = value switch
        {
            EstadoPreguntaEnum.Respondida => fuerte ? "PincelMarca" : "PincelMarcaSuave",
            EstadoPreguntaEnum.Salteada => rotulo || !fuerte ? "PincelPendienteSuave" : "PincelPendiente",
            ResultadoPreguntaEnum.Correcta => fuerte ? "PincelAcierto" : "PincelAciertoSuave",
            ResultadoPreguntaEnum.Incorrecta => fuerte ? "PincelError" : "PincelErrorSuave",
            ResultadoPreguntaEnum.Salteada => rotulo || !fuerte ? "PincelPendienteSuave" : "PincelPendiente",
            _ => fuerte ? "PincelBorde" : "PincelSuperficie"
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

/// <summary>
/// Una fraccion 0..1 llevada al trazo visible de un anillo de progreso (US-044).
///
/// <b>Por que un guion sobre la MISMA elipse y no un Path con un ArcSegment:</b> la version
/// anterior dibujaba el arco como una geometria aparte y lo metia en el mismo Grid que el
/// circulo de riel, centrado. Un <c>Path</c> se mide por el rectangulo que ocupa su geometria,
/// no por el circulo del que esa geometria es un pedazo: con un arco corto ese rectangulo es
/// chico y queda en un cuadrante, asi que centrarlo lo corre del borde hacia adentro. El
/// sintoma era un anillo que se montaba sobre el numero del medio y que solo caia en su lugar
/// al 100%, cuando el rectangulo del arco vuelve a ser el circulo entero.
///
/// Dibujando el arco como un guion del trazo de la misma elipse que hace de riel, el radio y
/// el centro son los del riel por construccion: no hay dos circulos que puedan desalinearse.
///
/// El trazo arranca a las 3 en punto —es donde WPF empieza a recorrer una elipse—, asi que la
/// vista lo rota -90 grados para que el progreso salga desde arriba.
/// </summary>
public class FraccionAAnilloConverter : IValueConverter
{
    /// <summary>
    /// Devuelve el <c>StrokeDashArray</c> que deja visible exactamente <paramref name="value"/>
    /// de la vuelta: un trazo del largo del arco y un hueco de una circunferencia entera, que
    /// garantiza que el patron no se repita.
    /// </summary>
    /// <param name="parameter">"radio,grosor" del anillo, por ejemplo "44,9".</param>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double fraccion = value switch
        {
            double d => d,
            int i => i,
            _ => 0d
        };

        fraccion = double.IsNaN(fraccion) ? 0 : Math.Clamp(fraccion, 0, 1);

        var (radio, grosor) = LeerMedidas(parameter);

        // Las unidades de StrokeDashArray son multiplos del grosor del trazo, no pixeles: por
        // eso la circunferencia se divide por el grosor. Sin esa division, el trazo mide
        // "grosor veces" lo que deberia y el anillo se llena con cualquier fraccion.
        double vuelta = 2 * Math.PI * radio / grosor;

        var patron = new DoubleCollection { fraccion * vuelta, vuelta };
        patron.Freeze();

        return patron;
    }

    private static (double Radio, double Grosor) LeerMedidas(object? parameter)
    {
        // Defaults = los del anillo del Historial, que es el primero que existio.
        double radio = 44;
        double grosor = 9;

        if (parameter is not string texto)
        {
            return (radio, grosor);
        }

        // Se parte antes de parsear: con "44,9" en una cultura que usa la coma como separador
        // decimal, parsear el texto entero daria 449.
        var partes = texto.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (partes.Length > 0 &&
            double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double r) && r > 0)
        {
            radio = r;
        }

        if (partes.Length > 1 &&
            double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double g) && g > 0)
        {
            grosor = g;
        }

        return (radio, grosor);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Un indice 0..N llevado a su numero de orden 1..N+1, para etiquetar los modulos de un libro
/// como "M1", "M2"... (US-045).
///
/// La etiqueta sale de la posicion en la lista y no de un campo del modulo: agregar o quitar
/// uno tiene que renumerar el resto solo, y un campo guardado obligaria a mantenerlo
/// sincronizado con el orden en cada edicion.
/// </summary>
public class MasUnoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int indice ? (indice + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Una fraccion 0..1 llevada a un ancho proporcional de columna, para la barra segmentada de
/// "Datos y almacenamiento" (US-046).
///
/// Devuelve estrellas y no pixeles a proposito: la barra ocupa el ancho de su tarjeta, que
/// depende de la ventana y del zoom (US-048). Con anchos fijos habria que recalcularlos en
/// cada cambio de tamanio; con estrellas, el Grid reparte solo y siempre suma el total.
///
/// Una fraccion de cero devuelve cero estrellas, que es una columna sin ancho: el segmento
/// desaparece en vez de dejar una raya de un pixel que se lee como "algo hay".
/// </summary>
public class FraccionAEstrellaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double fraccion = value is double d && !double.IsNaN(d) ? Math.Clamp(d, 0, 1) : 0;

        return new GridLength(fraccion, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Un color en <c>#RRGGBB</c> llevado a pincel, para la muestra de cada tema de la app
/// (US-049). Es el mismo trabajo que hace <see cref="ColorMateriaAPincelConverter"/> con el
/// color de una materia, pero sobre la paleta general: se mantienen separados porque son dos
/// ajustes independientes (RN-57) y mezclarlos invitaria a resolver uno con el otro.
/// </summary>
public class ColorDeTemaAPincelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string hex = value as string ?? string.Empty;

        try
        {
            var pincel = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            pincel.Freeze();
            return pincel;
        }
        catch
        {
            // Un hex invalido no puede romper el dibujado de la pantalla de Ajustes.
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
