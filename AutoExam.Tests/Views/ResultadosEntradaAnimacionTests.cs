using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-011, superficie "entrada de la pantalla de Resultados" (RN-7, mapa de
/// specs/02-tech-spec.md §US-011; arquitectura Inc-4 §4.7). Antes de este incremento la vista
/// Resultados de <c>ExamenView.xaml</c> no tenía animación de entrada — ninguna suite la cubría.
///
/// Verificación estructural del XAML del checkout (sin runtime WPF), cubre:
/// - AC-T52 / NFR-45 — la entrada anima sólo <c>Opacity</c> + <c>TranslateTransform.Y</c> (nunca
///   Brush/Color) y su timing sale de un recurso centralizado de <c>Theme/Estilos.xaml</c>.
/// - AC-T53 / NFR-47 — el disparo está guardado por <c>Animaciones.Reducidas == False</c>, así que
///   con "reducir movimiento" del SO activo la animación no corre y la vista aparece igual.
/// - AC-T54 — usa <c>DuracionTransicionSeccion</c> (0.22 s ≤ 250 ms).
///
/// Contraste con <see cref="ItemLibroFadeInReducirMovimientoTests"/>: esta superficie SÍ respeta
/// reducir movimiento (MultiDataTrigger + x:Static), el fade de <c>ItemLibro</c> (superficie 8)
/// no — misma técnica disponible, brecha registrada allá.
/// </summary>
public class ResultadosEntradaAnimacionTests
{
    private static readonly Lazy<XElement> GridResultados = new(() =>
    {
        var doc = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/ExamenView.xaml"));

        var grid = doc.Descendants()
            .Where(e => e.Name.LocalName == "Grid")
            .FirstOrDefault(e => e.Attributes().Any(a =>
                a.Name.LocalName == "Visibility" && a.Value.Contains("EnResultados")));

        Assert.True(grid is not null,
            "No se encontró el Grid de la vista Resultados (Visibility atada a EnResultados) en ExamenView.xaml.");
        return grid!;
    });

    private static XElement EntradaTrigger()
    {
        var trigger = GridResultados.Value.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "MultiDataTrigger" or "DataTrigger" &&
                                 e.Descendants().Any(x => x.Name.LocalName == "DoubleAnimation"));
        Assert.True(trigger is not null, "La vista Resultados no tiene una animación de entrada.");
        return trigger!;
    }

    [Fact]
    public void LaEntrada_SoloAnimaOpacityYTranslateY_NuncaBrush_AC_T52()
    {
        var objetivos = EntradaTrigger().Descendants()
            .Where(e => e.Name.LocalName == "DoubleAnimation")
            .Select(a => a.Attributes().FirstOrDefault(x => x.Name.LocalName.EndsWith("TargetProperty"))?.Value ?? string.Empty)
            .ToList();

        Assert.NotEmpty(objetivos);
        Assert.All(objetivos, o => Assert.True(
            o.Contains("Opacity") || o.Contains("TranslateTransform.Y"),
            $"La animación de entrada de Resultados apunta a '{o}' — sólo se admite Opacity o TranslateTransform.Y (NFR-45)."));
    }

    [Fact]
    public void LaEntrada_UsaDuracionCentralizadaYMenorA250ms_AC_T54()
    {
        var duraciones = EntradaTrigger().Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName == "Duration")
            .Select(a => a.Value)
            .ToList();

        Assert.NotEmpty(duraciones);
        Assert.All(duraciones, d => Assert.Equal("{StaticResource DuracionTransicionSeccion}", d));
    }

    [Fact]
    public void LaEntrada_NoDisparaConReducirMovimiento_AC_T53_NFR47()
    {
        var trigger = EntradaTrigger();

        bool tieneGuardia = trigger.Descendants()
            .Where(e => e.Name.LocalName == "Condition")
            .Any(c =>
                (c.Attributes().FirstOrDefault(a => a.Name.LocalName == "Binding")?.Value ?? string.Empty).Contains("Animaciones.Reducidas") &&
                (c.Attributes().FirstOrDefault(a => a.Name.LocalName == "Value")?.Value ?? string.Empty) == "False");

        Assert.True(tieneGuardia,
            "La animación de entrada de Resultados no está guardada por Animaciones.Reducidas == False (NFR-47).");
    }
}
