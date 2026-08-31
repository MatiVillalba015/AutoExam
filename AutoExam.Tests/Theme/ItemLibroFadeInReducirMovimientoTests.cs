using System.Xml.Linq;
using AutoExam.Behaviors;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// US-011, superficie 8 de RN-7 ("alta/baja de ítems en Historial y Libros") — fundido de
/// entrada del estilo <c>ItemLibro</c> en <c>AutoExam/Theme/Estilos.xaml</c>.
///
/// <b>Historia:</b> la primera versión disparaba el fade con
/// <c>EventTrigger RoutedEvent="FrameworkElement.Loaded"</c>. Un <c>EventTrigger</c> no admite
/// <c>&lt;Condition&gt;</c>, así que el fundido de 140 ms corría también con "reducir movimiento"
/// del SO activo — brecha contra NFR-47 / AC-T53 ("las animaciones no esenciales de las 8
/// superficies se acortan o desactivan" con <c>SystemParameters.ClientAreaAnimation == false</c>).
///
/// <b>Comportamiento actual (lo que fijan estos tests):</b> el fade se dispara con un
/// <c>MultiDataTrigger</c> — mismo patrón que la entrada de Resultados en <c>ExamenView.xaml</c> —
/// cuyas condiciones son (a) el contenedor pasó a visible y (b)
/// <c>Animaciones.Reducidas == False</c>. Con reducir-movimiento activo la condición (b) nunca se
/// cumple, no hay <c>EnterActions</c>, y el ítem aparece directo en <c>Opacity=1</c> sin animar.
/// El estado final (ítem visible) es idéntico con y sin animación, así que la paridad funcional
/// de NFR-48 se mantiene.
///
/// Parseo estructural del XAML del checkout, sin runtime WPF — mismo criterio que
/// <see cref="EstilosXamlAnimacionesHoverPresionTests"/>.
/// </summary>
public class ItemLibroFadeInReducirMovimientoTests
{
    private static readonly Lazy<XElement> ItemLibro = new(() =>
    {
        var doc = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Theme/Estilos.xaml"));
        var estilo = doc.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "Style" &&
            e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == "ItemLibro"));
        Assert.True(estilo is not null, "No se encontró <Style x:Key=\"ItemLibro\"> en AutoExam/Theme/Estilos.xaml.");
        return estilo!;
    });

    /// <summary>
    /// El <c>MultiDataTrigger</c> que dispara el fundido de entrada: se identifica por tener una
    /// condición ligada a la compuerta de reducir movimiento y un <c>DoubleAnimation</c> de
    /// <c>Opacity</c> en sus <c>EnterActions</c>.
    /// </summary>
    private static XElement TriggerDelFundidoDeEntrada()
    {
        var trigger = ItemLibro.Value.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "MultiDataTrigger" &&
                                 TieneCondicionDeReducirMovimiento(e) &&
                                 e.Descendants().Any(d => d.Name.LocalName == "DoubleAnimation" &&
                                                          Valor(d, "Storyboard.TargetProperty") == "Opacity"));

        Assert.True(trigger is not null,
            "ItemLibro no tiene un MultiDataTrigger que dispare el fundido de entrada de Opacity " +
            "con la guardia de 'reducir movimiento' — se rompió el cierre de la brecha NFR-47 " +
            "(superficie 8 de US-011).");
        return trigger!;
    }

    // ------------------------------------------------------------------
    // El fundido de entrada existe y usa timing centralizado
    // ------------------------------------------------------------------

    [Fact]
    public void ItemLibro_TieneFundidoDeEntrada_OpacityCeroAUno()
    {
        var animaciones = TriggerDelFundidoDeEntrada().Descendants()
            .Where(e => e.Name.LocalName == "DoubleAnimation")
            .ToList();

        Assert.Contains(animaciones, a =>
            Valor(a, "Storyboard.TargetProperty") == "Opacity" &&
            Valor(a, "From") == "0" &&
            Valor(a, "To") == "1");
    }

    [Fact]
    public void ElFundidoDeEntrada_UsaLaDuracionCentralizada_NoUnValorHardcodeado_NFR45()
    {
        var duraciones = TriggerDelFundidoDeEntrada().Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName == "Duration")
            .Select(a => a.Value)
            .ToList();

        Assert.NotEmpty(duraciones);
        Assert.All(duraciones, d => Assert.StartsWith("{StaticResource Duracion", d));
    }

    [Fact]
    public void ElFundidoDeEntrada_NoSeDisparaConEventTriggerDeLoaded()
    {
        // Un EventTrigger de Loaded no admite <Condition>: si volviera, la guardia de reducir
        // movimiento se perdería otra vez.
        var eventTriggersDeLoaded = ItemLibro.Value.Descendants()
            .Where(e => e.Name.LocalName == "EventTrigger" &&
                        e.Attributes().Any(a => a.Name.LocalName == "RoutedEvent" && a.Value.Contains("Loaded")))
            .ToList();

        Assert.Empty(eventTriggersDeLoaded);
    }

    // ------------------------------------------------------------------
    // NFR-47 / AC-T53 — el fundido de entrada respeta "reducir movimiento"
    // ------------------------------------------------------------------

    [Fact]
    public void ElFundidoDeEntrada_SoloCorreConReducirMovimientoApagado_NFR47()
    {
        var trigger = TriggerDelFundidoDeEntrada();

        // La condición que exige Animaciones.Reducidas == False.
        Assert.Contains(trigger.Descendants(), e =>
            e.Name.LocalName == "Condition" &&
            e.Attributes().Any(a => a.Name.LocalName == "Binding" && a.Value.Contains("Animaciones.Reducidas")) &&
            e.Attributes().Any(a => a.Name.LocalName == "Value" && a.Value == "False"));

        // El fade vive en EnterActions (no en ExitActions): con la guardia en False, EnterActions
        // nunca se ejecuta y el ítem queda en su Opacity por defecto (1).
        Assert.Contains(trigger.Elements(), e => e.Name.LocalName == "MultiDataTrigger.EnterActions");
    }

    [Fact]
    public void ElHoverDelMismoEstilo_SI_TieneGuardiaDeReducirMovimiento_NFR47()
    {
        var hover = ItemLibro.Value.Descendants()
            .Where(e => e.Name.LocalName == "MultiTrigger")
            .FirstOrDefault(mt => TieneCondicion(mt, "IsMouseOver", "True"));

        Assert.True(hover is not null, "ItemLibro perdió su MultiTrigger de hover.");
        Assert.True(
            hover!.Descendants().Any(e => e.Name.LocalName == "Condition" &&
                e.Attributes().Any(a => a.Name.LocalName == "Property" && a.Value.Contains("MovimientoReducido")) &&
                e.Attributes().Any(a => a.Name.LocalName == "Value" && a.Value == "False")),
            "El MultiTrigger de hover de ItemLibro ya no lleva la guardia Animaciones.MovimientoReducido=False (NFR-47).");
    }

    // ------------------------------------------------------------------
    // La compuerta de "reducir movimiento" es una sola fuente de verdad
    // ------------------------------------------------------------------

    [Fact]
    public void LaCompuertaDeReducirMovimiento_ExisteYEsUnaSolaFuenteDeVerdad()
    {
        var tipo = typeof(Animaciones);

        Assert.NotNull(tipo.GetProperty("Reducidas"));
        Assert.NotNull(tipo.GetField("MovimientoReducidoProperty"));
        Assert.NotNull(tipo.GetMethod("GetMovimientoReducido"));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static bool TieneCondicionDeReducirMovimiento(XElement trigger) =>
        trigger.Descendants().Any(e => e.Name.LocalName == "Condition" &&
            e.Attributes().Any(a => a.Name.LocalName == "Binding" && a.Value.Contains("Animaciones.Reducidas")));

    private static string Valor(XElement e, string atributoLocalName) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName == atributoLocalName)?.Value ?? string.Empty;

    private static bool TieneCondicion(XElement multiTrigger, string propiedad, string valor) =>
        multiTrigger.Descendants().Any(e => e.Name.LocalName == "Condition" &&
            e.Attributes().Any(a => a.Name.LocalName == "Property" && a.Value == propiedad) &&
            e.Attributes().Any(a => a.Name.LocalName == "Value" && a.Value == valor));
}
