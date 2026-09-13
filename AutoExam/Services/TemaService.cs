using System.Windows;
using System.Windows.Media;
using AutoExam.Models;
using Wpf.Ui.Appearance;

namespace AutoExam.Services;

/// <summary>
/// Cambia el tema en dos frentes: los controles de WPF-UI y los tokens propios
/// de AutoExam. Los diccionarios de tokens definen las mismas claves, asi que
/// intercambiar uno por el otro repinta toda la app sin tocar un solo estilo.
///
/// Desde US-049 la eleccion no es solo claro/oscuro sino un tema de
/// <see cref="PaletaDeApp"/>: encima del juego de tokens se pisan las claves del acento
/// (marca, marca fuerte, marca suave, sobre-marca y el degradado del icono). Se pisan sobre
/// el diccionario recien creado, ANTES de mergearlo, y no con un diccionario de acento
/// aparte: dos diccionarios con las mismas claves dejan el resultado dependiendo del orden
/// de busqueda de MergedDictionaries, que es justo la clase de detalle que se rompe callado.
/// </summary>
public static class TemaService
{
    // Pack URI absoluto y no ruta relativa: una relativa se resuelve contra el
    // ensamblado de entrada, asi que se rompe apenas AutoExam deja de serlo
    // (por ejemplo al cargarlo desde un arnes de pruebas).
    private const string RutaOscuro = "pack://application:,,,/AutoExam;component/Theme/Tokens.Oscuro.xaml";
    private const string RutaClaro = "pack://application:,,,/AutoExam;component/Theme/Tokens.Claro.xaml";

    public static bool EsOscuro { get; private set; } = true;

    /// <summary>Tema de color activo (US-049). Es la clave, no el objeto, porque es lo que se guarda.</summary>
    public static string TemaActual { get; private set; } = PaletaDeApp.PorDefecto;

    /// <summary>Compatibilidad: aplicar solo claro/oscuro equivale a elegir el tema violeta o el claro.</summary>
    public static void Aplicar(bool oscuro) => Aplicar(PaletaDeApp.DesdeTemaOscuro(oscuro));

    public static void Aplicar(string claveDeTema)
    {
        var tema = PaletaDeApp.Resolver(claveDeTema);

        EsOscuro = tema.Oscuro;
        TemaActual = tema.Clave;

        var recursos = Application.Current?.Resources;

        if (recursos is null)
        {
            return;
        }

        var nuevo = new ResourceDictionary
        {
            Source = new Uri(tema.Oscuro ? RutaOscuro : RutaClaro, UriKind.Absolute)
        };

        PintarAcento(nuevo, tema);

        ApplicationThemeManager.Apply(tema.Oscuro ? ApplicationTheme.Dark : ApplicationTheme.Light);
        IntercambiarTokens(recursos, nuevo);

        // Despues de aplicar el tema, nunca antes: ApplicationThemeManager recarga el
        // diccionario de WPF-UI, y ese diccionario trae sus propios acentos. Lo que se escribe
        // aca va al diccionario propio de la Application, que se consulta antes que cualquier
        // diccionario mergeado, asi que gana sin depender del orden de la lista.
        PintarAcentoDeLosControles(recursos, nuevo, tema.Oscuro);
    }

    private static void IntercambiarTokens(ResourceDictionary recursos, ResourceDictionary nuevo)
    {
        // Se reemplaza el diccionario de tokens anterior, nunca se acumulan:
        // dos diccionarios con las mismas claves harian ganar al ultimo agregado
        // y el tema quedaria dependiendo del orden de las llamadas.
        var viejos = recursos.MergedDictionaries
            .Where(d => d.Source is not null &&
                        d.Source.OriginalString.Contains("Tokens.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var viejo in viejos)
        {
            recursos.MergedDictionaries.Remove(viejo);
        }

        // Los tokens van antes que Estilos.xaml en el orden de busqueda, pero como
        // los estilos usan DynamicResource el orden de insercion no los afecta.
        recursos.MergedDictionaries.Insert(0, nuevo);
    }

    /// <summary>
    /// Pisa las claves del acento en el diccionario de tokens recien cargado. Un tema que se
    /// conforma con el acento original (los dos violetas) no toca nada: sus colores ya estan
    /// escritos en Tokens.Oscuro/Claro y duplicarlos aca los dejaria en dos lugares.
    /// </summary>
    private static void PintarAcento(ResourceDictionary tokens, TemaDeApp tema)
    {
        if (tema.UsaElAcentoDeLosTokens)
        {
            return;
        }

        tokens["PincelMarca"] = Pincel(tema.Marca);
        tokens["PincelMarcaFuerte"] = Pincel(tema.MarcaFuerte);
        tokens["PincelMarcaSuave"] = Pincel(tema.MarcaSuave);
        tokens["PincelSobreMarca"] = Pincel(tema.SobreMarca);

        // El cuadrado de icono del menu (US-041) es un degradado de dos paradas del mismo
        // acento corridas en luminancia. Si el tema cambia el acento y este pincel no, el
        // menu queda con cuatro cuadrados violetas dentro de una app azul.
        var degradado = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };

        degradado.GradientStops.Add(new GradientStop(Desde(tema.IconoDesde), 0));
        degradado.GradientStops.Add(new GradientStop(Desde(tema.IconoHasta), 1));
        degradado.Freeze();

        tokens["PincelIconoAccion"] = degradado;
    }

    /// <summary>
    /// Lleva el acento tambien a los controles de WPF-UI: el boton primario y los interruptores
    /// salen de su propia paleta, no de los tokens de AutoExam. Sin esto el criterio de US-049
    /// queda a medias — la app cambia de color pero "Guardar" y los toggles siguen violetas, que
    /// es justo lo que el criterio nombra ("botones principales, elementos destacados").
    ///
    /// Se pisan los pinceles concretos y no <c>SystemAccentColor*</c>: el diccionario de WPF-UI
    /// deriva sus pinceles de esos colores con StaticResource al cargarse, resolviendolos contra
    /// SU propia copia, asi que escribir los colores no cambia nada. Los pinceles, en cambio,
    /// las plantillas los consumen con DynamicResource, y el diccionario propio de la
    /// Application se consulta antes que cualquier mergeado.
    ///
    /// Los tres tonos derivados son el mismo acento corrido en luminancia, en la direccion que
    /// corresponde a cada fondo: sobre oscuro, mas claro; sobre claro, mas oscuro.
    /// </summary>
    private static void PintarAcentoDeLosControles(
        ResourceDictionary recursos, ResourceDictionary tokens, bool oscuro)
    {
        if (tokens["PincelMarca"] is not SolidColorBrush marca)
        {
            return;
        }

        // El acento sale del token ya resuelto y no de una constante aparte: asi el violeta
        // sigue estando escrito en un solo lugar (Tokens.Oscuro/Claro.xaml) y un tema con
        // acento propio no necesita repetir sus colores para los controles.
        Color baseColor = marca.Color;
        double sentido = oscuro ? 1 : -1;

        var acento = Pincel(baseColor);
        var encima = Pincel(Correr(baseColor, 0.12 * sentido));
        var apretado = Pincel(Correr(baseColor, 0.24 * sentido));

        // El texto que va ENCIMA del acento pleno. Sale del token y no de blanco fijo: sobre el
        // violeta claro del tema Claro, un texto blanco no se lee.
        var sobreAcento = tokens["PincelSobreMarca"] as SolidColorBrush ?? Pincel(Colors.White);

        // Los colores sueltos igual se publican: algunas plantillas los usan para armar sus
        // propios pinceles de estado.
        recursos["SystemAccentColor"] = baseColor;
        recursos["SystemAccentColorPrimary"] = encima.Color;
        recursos["SystemAccentColorSecondary"] = apretado.Color;
        recursos["SystemAccentColorTertiary"] = Correr(baseColor, 0.36 * sentido);

        foreach (var (clave, pincel) in new (string, SolidColorBrush)[]
        {
            ("SystemAccentColorBrush", acento),
            ("SystemAccentColorPrimaryBrush", encima),
            ("SystemAccentColorSecondaryBrush", apretado),
            ("SystemAccentColorTertiaryBrush", apretado),

            // Fondos de acento genericos: barra de progreso, seleccion, resaltados.
            ("AccentFillColorDefaultBrush", acento),
            ("AccentFillColorSecondaryBrush", encima),
            ("AccentFillColorTertiaryBrush", apretado),
            ("AccentTextFillColorPrimaryBrush", acento),
            ("AccentTextFillColorSecondaryBrush", encima),
            ("AccentTextFillColorTertiaryBrush", apretado),
            ("ControlFillColorAccentBrush", acento),

            // Boton primario (ui:Button Appearance="Primary"): "Guardar", "Verificar y empezar".
            ("AccentButtonBackground", acento),
            ("AccentButtonBackgroundPointerOver", encima),
            ("AccentButtonBackgroundPressed", apretado),
            ("AccentButtonBorderBrushPressed", apretado),
            ("AccentButtonForeground", sobreAcento),
            ("AccentButtonForegroundPointerOver", sobreAcento),
            ("AccentButtonForegroundPressed", sobreAcento),

            // Interruptores encendidos: notificaciones, reducir movimiento.
            ("ToggleSwitchFillOn", acento),
            ("ToggleSwitchFillOnPointerOver", encima),
            ("ToggleSwitchFillOnPressed", apretado),
            ("ToggleSwitchStrokeOn", acento),
            ("ToggleSwitchStrokeOnPointerOver", encima),
            ("ToggleSwitchStrokeOnPressed", apretado),
            ("ToggleSwitchKnobFillOn", sobreAcento),
            ("ToggleSwitchKnobFillOnPointerOver", sobreAcento),
            ("ToggleSwitchKnobFillOnPressed", sobreAcento),
        })
        {
            recursos[clave] = pincel;
        }
    }

    private static SolidColorBrush Pincel(Color color)
    {
        var pincel = new SolidColorBrush(color);
        pincel.Freeze();
        return pincel;
    }

    /// <summary>
    /// Mueve un color hacia el blanco (cantidad positiva) o hacia el negro (negativa). Mezcla
    /// lineal y no un ajuste de HSL: sobre un acento ya saturado la mezcla conserva el matiz,
    /// que es lo unico que importa para que los tres tonos se lean como el mismo color.
    /// </summary>
    private static Color Correr(Color color, double cantidad)
    {
        byte Mezclar(byte canal)
        {
            double destino = cantidad >= 0 ? 255 : 0;
            double mezclado = canal + (destino - canal) * Math.Abs(cantidad);

            return (byte)Math.Clamp(Math.Round(mezclado), 0, 255);
        }

        return Color.FromRgb(Mezclar(color.R), Mezclar(color.G), Mezclar(color.B));
    }

    /// <summary>
    /// Congelado a proposito: un pincel congelado se puede compartir entre hilos y no vuelve a
    /// notificar cambios, que es lo que se quiere de un token de tema — el repintado sale de
    /// reemplazar el diccionario entero, no de mutar el pincel.
    /// </summary>
    private static SolidColorBrush Pincel(string hex)
    {
        var pincel = new SolidColorBrush(Desde(hex));
        pincel.Freeze();
        return pincel;
    }

    private static Color Desde(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
