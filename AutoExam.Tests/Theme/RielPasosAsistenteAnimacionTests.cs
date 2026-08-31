using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// US-011, superficie 3 de RN-7 ("riel de pasos del asistente — línea de avance") — pulido de
/// la línea de avance entre pasos en <c>AutoExam/Views/AsistenteView.xaml</c>
/// (hallazgo D-2 de QA).
///
/// <b>Antes:</b> el <c>Border x:Name="Linea"</c> cambiaba <c>Background</c>
/// <c>PincelBorde</c> → <c>PincelMarca</c> de forma instantánea vía <c>Setter</c>, sin storyboard
/// ni los parámetros centralizados de duración/suavizado — incumplía el criterio "pasa" del
/// checklist de US-011 (usa los parámetros centralizados).
///
/// <b>Ahora:</b> base gris <c>Linea</c> siempre visible + overlay <c>LineaAvance</c> (color marca)
/// que "crece" 0→1 con un <c>ScaleTransform.X</c> (<c>x:Name="EscalaAvance"</c>). No se anima
/// <c>Brush</c>/<c>Color</c> (restricción del tech-spec), solo la escala. La transición usa
/// <c>DuracionTransicionSeccion</c> + <c>SuavizadoSalida</c> de <c>Theme/Estilos.xaml</c> y solo
/// corre con "reducir movimiento" del SO apagado (NFR-47 / AC-T53); con reducir-movimiento activo
/// la línea salta a <c>ScaleX=1</c> sin animar. Estado final idéntico en ambos casos.
///
/// Parseo estructural del XAML del checkout, sin runtime WPF — mismo criterio que
/// <see cref="EstilosXamlAnimacionesHoverPresionTests"/> y
/// <see cref="ItemLibroFadeInReducirMovimientoTests"/>.
/// </summary>
public class RielPasosAsistenteAnimacionTests
{
    private static readonly Lazy<XElement> RielTemplate = new(() =>
    {
        var doc = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/AsistenteView.xaml"));

        // El ControlTemplate del riel es el que contiene el Border x:Name="LineaAvance".
        var template = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ControlTemplate" &&
                                 e.Descendants().Any(d => d.Name.LocalName == "Border" &&
                                                          Attr(d, "Name") == "LineaAvance"));

        Assert.True(template is not null,
            "No se encontró el ControlTemplate del riel de pasos (con <Border x:Name=\"LineaAvance\">) " +
            "en AutoExam/Views/AsistenteView.xaml.");
        return template!;
    });

    // ------------------------------------------------------------------
    // Estructura: base gris + overlay marca con ScaleTransform
    // ------------------------------------------------------------------

    [Fact]
    public void LaBaseGrisDeLaLinea_SeConserva_PincelBorde()
    {
        var linea = RielTemplate.Value.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" && Attr(e, "Name") == "Linea");

        Assert.True(linea is not null, "Desapareció el Border x:Name=\"Linea\" (base gris del riel).");
        Assert.Equal("{DynamicResource PincelBorde}", Attr(linea!, "Background"));
    }

    [Fact]
    public void LaLineaDeAvance_EsUnOverlayMarca_ConScaleTransformEnCeroPorDefecto()
    {
        var avance = RielTemplate.Value.Descendants()
            .First(e => e.Name.LocalName == "Border" && Attr(e, "Name") == "LineaAvance");

        Assert.Equal("{DynamicResource PincelMarca}", Attr(avance, "Background"));

        var escala = avance.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "ScaleTransform" && Attr(e, "Name") == "EscalaAvance");

        Assert.True(escala is not null, "LineaAvance no tiene un <ScaleTransform x:Name=\"EscalaAvance\">.");
        Assert.Equal("0", Attr(escala!, "ScaleX"));   // arranca colapsada: la muestra la base gris
        Assert.Equal("0,0.5", Attr(avance, "RenderTransformOrigin"));   // crece desde el borde izquierdo
    }

    [Fact]
    public void YaNoQuedaElSwapInstantaneoDeBackgroundDeLaLinea()
    {
        // El mecanismo viejo: <Setter TargetName="Linea" Property="Background" Value="{DynamicResource PincelMarca}" />
        var swaps = RielTemplate.Value.Descendants()
            .Where(e => e.Name.LocalName == "Setter" &&
                        Attr(e, "TargetName") == "Linea" &&
                        Attr(e, "Property") == "Background")
            .ToList();

        Assert.Empty(swaps);
    }

    // ------------------------------------------------------------------
    // Transición: crece 0→1 con timing centralizado (NFR-45)
    // ------------------------------------------------------------------

    [Fact]
    public void ElAvance_Anima_ScaleX_DeCeroAUno()
    {
        var anim = AnimacionesDeEscalaAvance()
            .FirstOrDefault(a => Attr(a, "From") == "0" && Attr(a, "To") == "1");

        Assert.True(anim is not null,
            "No hay un DoubleAnimation From=0 To=1 sobre EscalaAvance.ScaleX (la línea no 'crece').");
    }

    [Fact]
    public void LaTransicionDelAvance_UsaDuracionYSuavizadoCentralizados_NoHardcodeados_NFR45()
    {
        var animada = AnimacionesDeEscalaAvance()
            .First(a => Attr(a, "From") == "0" && Attr(a, "To") == "1");

        Assert.Equal("{StaticResource DuracionTransicionSeccion}", Attr(animada, "Duration"));
        Assert.Equal("{StaticResource SuavizadoSalida}", Attr(animada, "EasingFunction"));
    }

    // ------------------------------------------------------------------
    // NFR-47 / AC-T53: la transición respeta "reducir movimiento"
    // ------------------------------------------------------------------

    [Fact]
    public void LaTransicionAnimada_SoloCorreConReducirMovimientoApagado_NFR47()
    {
        var trigger = RielTemplate.Value.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "MultiDataTrigger" &&
                                 TieneCondicionReducidas(e, "False") &&
                                 e.Descendants().Any(EsAnimacionAnimadaDeAvance));

        Assert.True(trigger is not null,
            "La animación 0→1 de la línea de avance no está detrás de una condición " +
            "Animaciones.Reducidas=False (NFR-47).");

        // Debe tener condición EsActual=True también (se dispara al pasar a ser el paso actual).
        Assert.Contains(trigger!.Descendants(), c =>
            c.Name.LocalName == "Condition" &&
            Attr(c, "Binding").Contains("EsActual") && Attr(c, "Value") == "True");
    }

    [Fact]
    public void ConReducirMovimientoActivo_LaLineaLlegaAScaleXUno_SinAnimar_NFR47_NFR48()
    {
        // Camino reduce-motion: DoubleAnimation To=1 Duration=0 (salto instantáneo), tanto para
        // el paso actual (MultiDataTrigger con Reducidas=True) como para el completado.
        var saltos = AnimacionesDeEscalaAvance()
            .Where(a => Attr(a, "To") == "1" && EsDuracionCero(Attr(a, "Duration")))
            .ToList();

        Assert.NotEmpty(saltos);

        var triggerReduceMotion = RielTemplate.Value.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "MultiDataTrigger" && TieneCondicionReducidas(e, "True"));

        Assert.True(triggerReduceMotion is not null,
            "Falta el MultiDataTrigger que fija ScaleX=1 sin animar cuando Animaciones.Reducidas=True.");
    }

    // ------------------------------------------------------------------
    // Restricción del tech-spec: no se anima Brush/Color
    // ------------------------------------------------------------------

    [Fact]
    public void ElRiel_NoAnima_NingunBrushNiColor()
    {
        var prohibidas = RielTemplate.Value.Descendants()
            .Where(e => (e.Name.LocalName.Contains("Color") || e.Name.LocalName.Contains("Brush")) &&
                        e.Name.LocalName.Contains("Animation"))
            .Select(e => e.Name.LocalName)
            .ToList();

        Assert.Empty(prohibidas);

        // Y ninguna animación apunta a Background/Foreground/BorderBrush.
        foreach (var anim in RielTemplate.Value.Descendants()
                     .Where(e => e.Name.LocalName.EndsWith("Animation") || e.Name.LocalName.EndsWith("KeyFrames")))
        {
            var target = Attr(anim, "Storyboard.TargetProperty");
            Assert.DoesNotContain("Background", target);
            Assert.DoesNotContain("Foreground", target);
            Assert.DoesNotContain("Brush", target);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IEnumerable<XElement> AnimacionesDeEscalaAvance() =>
        RielTemplate.Value.Descendants().Where(e =>
            e.Name.LocalName == "DoubleAnimation" &&
            Attr(e, "Storyboard.TargetName") == "EscalaAvance" &&
            Attr(e, "Storyboard.TargetProperty") == "ScaleX");

    private static bool EsAnimacionAnimadaDeAvance(XElement e) =>
        e.Name.LocalName == "DoubleAnimation" &&
        Attr(e, "Storyboard.TargetName") == "EscalaAvance" &&
        Attr(e, "From") == "0" && Attr(e, "To") == "1";

    private static bool TieneCondicionReducidas(XElement trigger, string valor) =>
        trigger.Descendants().Any(c => c.Name.LocalName == "Condition" &&
            Attr(c, "Binding").Contains("Animaciones.Reducidas") && Attr(c, "Value") == valor);

    private static bool EsDuracionCero(string d) => d is "0" or "0:0:0" or "00:00:00" or "0:0:0.0";

    private static string Attr(XElement e, string local) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName == local)?.Value ?? string.Empty;
}
