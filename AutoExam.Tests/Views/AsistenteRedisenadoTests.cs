using System.Globalization;
using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-043 — pulido visual de los 3 pasos del asistente de Nuevo examen.
///
/// El último criterio de la historia es el que más pesa: "ninguna opción, campo, texto ni
/// comportamiento cambia". Por eso la mitad de estos tests no verifica lo que se agregó sino
/// lo que tiene que haber quedado igual — los cuatro orígenes de preguntas con sus mismos
/// comandos, y los campos de Alcance y Formato.
///
/// Lo que no cubren es si se ve bien. Eso hay que mirarlo.
/// </summary>
public class AsistenteRedisenadoTests
{
    private const string Vista = "AutoExam/Views/AsistenteView.xaml";

    private static XDocument Doc(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    private static XElement Estilo(string clave)
    {
        var estilo = Doc("AutoExam/Theme/Estilos.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                 e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave));

        Assert.True(estilo is not null, $"No se encontró el estilo {clave} en Estilos.xaml.");
        return estilo!;
    }

    private static XElement RielDePasos() => Doc(Vista).Descendants()
        .First(e => e.Name.LocalName == "ItemsControl" &&
                    (e.Attribute("ItemsSource")?.Value ?? string.Empty)
                        .Contains("Pasos", StringComparison.Ordinal));

    // ------------------------------------------------------------------
    // AC — el riel: tilde en lo hecho, número en lo actual, etiqueta del paso
    // ------------------------------------------------------------------

    [Fact]
    public void UnPasoCompletado_MuestraUnTildeEnLugarDeSuNumero_US043()
    {
        var riel = RielDePasos();

        var tilde = riel.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "SymbolIcon" &&
                                 (e.Attribute("Symbol")?.Value ?? string.Empty)
                                     .StartsWith("Checkmark", StringComparison.Ordinal));

        Assert.True(tilde is not null, "El riel no tiene tilde para los pasos ya hechos (US-043).");

        // Arranca oculto y lo enciende el paso completado, que además apaga el número: el
        // criterio pide el tilde EN LUGAR del número, no los dos juntos.
        Assert.Equal("Collapsed", tilde!.Attribute("Visibility")?.Value);

        var completado = riel.Descendants()
            .First(e => e.Name.LocalName == "DataTrigger" &&
                        (e.Attribute("Binding")?.Value ?? string.Empty)
                            .Contains("Completado", StringComparison.Ordinal));

        var cambios = completado.Elements()
            .Where(s => s.Name.LocalName == "Setter")
            .ToDictionary(
                s => $"{s.Attribute("TargetName")?.Value}.{s.Attribute("Property")?.Value}",
                s => s.Attribute("Value")?.Value ?? string.Empty);

        Assert.Equal("Visible", cambios["Tilde.Visibility"]);
        Assert.Equal("Collapsed", cambios["Numero.Visibility"]);
    }

    [Fact]
    public void ElPasoActual_SigueResaltadoEnVioletaConSuNumero_US043()
    {
        var actual = RielDePasos().Descendants()
            .First(e => e.Name.LocalName == "DataTrigger" &&
                        (e.Attribute("Binding")?.Value ?? string.Empty)
                            .Contains("EsActual", StringComparison.Ordinal));

        var cambios = actual.Elements()
            .Where(s => s.Name.LocalName == "Setter")
            .ToDictionary(
                s => $"{s.Attribute("TargetName")?.Value}.{s.Attribute("Property")?.Value}",
                s => s.Attribute("Value")?.Value ?? string.Empty);

        Assert.Contains("PincelMarca", cambios["Circulo.Background"], StringComparison.Ordinal);

        // El número del paso actual no se esconde: el tilde es sólo para lo que ya quedó atrás.
        Assert.DoesNotContain("Numero.Visibility", cambios.Keys);
    }

    [Fact]
    public void ArribaDelContenido_HayUnaEtiquetaConElNumeroYElNombreDelPaso_US043()
    {
        var doc = Doc(Vista);

        var numero = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBlock" &&
                                 (e.Attribute("Text")?.Value ?? string.Empty)
                                     .Contains("PasoNumerado", StringComparison.Ordinal));

        var nombre = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBlock" &&
                                 (e.Attribute("Text")?.Value ?? string.Empty)
                                     .Contains("PasoNombrado", StringComparison.Ordinal));

        Assert.True(numero is not null && nombre is not null,
            "Falta la etiqueta \"01 MATERIAL\" arriba de la tarjeta del paso (US-043).");

        // Va entre el riel y el contenido: pegada a lo que se completa, no perdida al pie.
        int filaDeLaEtiqueta = int.Parse(
            numero!.Ancestors().First(a => a.Attribute("Grid.Row") is not null)
                .Attribute("Grid.Row")!.Value,
            CultureInfo.InvariantCulture);

        int filaDelRiel = int.Parse(RielDePasos().Attribute("Grid.Row")!.Value, CultureInfo.InvariantCulture);

        int filaDelContenido = int.Parse(
            Doc(Vista).Descendants()
                .First(e => e.Name.LocalName == "ScrollViewer" &&
                            e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "AreaDelPaso"))
                .Attribute("Grid.Row")!.Value,
            CultureInfo.InvariantCulture);

        Assert.InRange(filaDeLaEtiqueta, filaDelRiel + 1, filaDelContenido - 1);
    }

    // ------------------------------------------------------------------
    // AC — hover y click de las 4 tarjetas de origen
    // ------------------------------------------------------------------

    [Fact]
    public void LasCuatroTarjetasDeOrigen_UsanElEstiloPropio_NoElDeLasFichasChicas_US043()
    {
        var tarjetas = Doc(Vista).Descendants()
            .Where(e => e.Name.LocalName == "RadioButton" &&
                        (e.Attribute("GroupName")?.Value ?? string.Empty) == "OrigenPreguntas")
            .ToList();

        Assert.Equal(4, tarjetas.Count);

        foreach (var tarjeta in tarjetas)
        {
            Assert.Contains("TarjetaDeOpcion", tarjeta.Attribute("Style")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ElHoverDeUnaTarjetaDeOrigen_HaceUnZoomLeveYEnciendeElAnilloVioleta_US043()
    {
        var hover = Estilo("TarjetaDeOpcion").Descendants()
            .First(e => e.Name.LocalName == "MultiTrigger" &&
                        e.Descendants().Any(c =>
                            c.Name.LocalName == "Condition" &&
                            (c.Attribute("Property")?.Value ?? string.Empty)
                                .EndsWith("IsMouseOver", StringComparison.Ordinal) &&
                            (c.Attribute("Value")?.Value ?? string.Empty) == "True") &&
                        e.Elements().Any(a => a.Name.LocalName.EndsWith("EnterActions", StringComparison.Ordinal)));

        var zoom = hover.Descendants()
            .Where(a => a.Name.LocalName == "DoubleAnimation" &&
                        (a.Attribute("Storyboard.TargetName")?.Value ?? string.Empty) == "Zoom")
            .ToList();

        Assert.True(zoom.Count >= 2, "El hover de la tarjeta no escala, o escala en un solo eje (US-043).");

        foreach (var animacion in zoom.Where(a => a.Attribute("To")?.Value != "1"))
        {
            // "Leve y sutil (aprox. 1,02x)": el criterio da el número, y US-029 pone el techo.
            double destino = double.Parse(animacion.Attribute("To")!.Value, CultureInfo.InvariantCulture);
            Assert.InRange(destino, 1.01, 1.03);

            Assert.Contains("StaticResource", animacion.Attribute("Duration")?.Value ?? string.Empty,
                StringComparison.Ordinal);
            Assert.Contains("StaticResource", animacion.Attribute("EasingFunction")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }

        var anillo = Estilo("TarjetaDeOpcion").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("BorderBrush")?.Value ?? string.Empty)
                                     .Contains("PincelMarca", StringComparison.Ordinal) &&
                                 (e.Attribute("Opacity")?.Value ?? string.Empty) == "0");

        Assert.True(anillo is not null, "La tarjeta no tiene borde de acento que el hover pueda encender.");

        Assert.Contains(hover.Descendants(),
            a => a.Name.LocalName == "DoubleAnimation" &&
                 (a.Attribute("Storyboard.TargetName")?.Value ?? string.Empty) == "AnilloHover" &&
                 a.Attribute("To")?.Value == "1");
    }

    [Fact]
    public void ElClickEnUnaTarjetaDeOrigen_LaHundeUnInstanteYLaDevuelve_US043()
    {
        var pulsado = Estilo("TarjetaDeOpcion").Descendants()
            .First(e => e.Name.LocalName == "MultiTrigger" &&
                        e.Descendants().Any(c =>
                            c.Name.LocalName == "Condition" &&
                            (c.Attribute("Property")?.Value ?? string.Empty)
                                .EndsWith("IsPressed", StringComparison.Ordinal) &&
                            (c.Attribute("Value")?.Value ?? string.Empty) == "True"));

        var hundimiento = pulsado.Descendants()
            .Where(a => a.Name.LocalName == "DoubleAnimation" &&
                        (a.Attribute("Storyboard.TargetName")?.Value ?? string.Empty) == "Presion")
            .Select(a => double.Parse(a.Attribute("To")!.Value, CultureInfo.InvariantCulture))
            .ToList();

        // Se achica (algo menor que 1) y después vuelve a 1: sin la vuelta quedaría hundida
        // para siempre.
        Assert.Contains(hundimiento, v => v < 1);
        Assert.Contains(hundimiento, v => v == 1);
    }

    [Fact]
    public void ElZoomDelHoverYElHundimiento_SonEscalasDistintas_ParaQueSoltarNoCanceleElHover_US043()
    {
        // Con una sola escala compartida, el ExitActions del pulsado devolvía la tarjeta a 1.0
        // aunque el mouse siguiera encima, y el hover se perdía hasta salir y volver a entrar.
        var escalas = Estilo("TarjetaDeOpcion").Descendants()
            .Where(e => e.Name.LocalName == "ScaleTransform")
            .Select(e => e.Attributes().First(a => a.Name.LocalName == "Name").Value)
            .ToList();

        Assert.Contains("Zoom", escalas);
        Assert.Contains("Presion", escalas);
    }

    [Fact]
    public void ConReducirMovimiento_LaTarjetaSeResaltaPeroNoCrece_RN33()
    {
        var conMovimientoReducido = Estilo("TarjetaDeOpcion").Descendants()
            .Where(e => e.Name.LocalName == "MultiTrigger")
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).Contains("MovimientoReducido") &&
                (c.Attribute("Value")?.Value ?? string.Empty) == "True"))
            .ToList();

        Assert.NotEmpty(conMovimientoReducido);

        foreach (var disparo in conMovimientoReducido)
        {
            Assert.DoesNotContain(disparo.Descendants(),
                s => (s.Attribute("Property")?.Value ?? string.Empty)
                    .Contains("Scale", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ------------------------------------------------------------------
    // AC — más profundidad en las tarjetas que se eligen
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("TarjetaSeleccionable")]
    [InlineData("TarjetaDeOpcion")]
    [InlineData("ItemLibro")]
    public void LasTarjetasQueSeEligen_TienenFondoConProfundidad_NoPlano_US043(string clave)
    {
        string xaml = Estilo(clave).ToString();

        Assert.Contains("PincelTarjetaProfunda", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AutoExam/Theme/Tokens.Oscuro.xaml")]
    [InlineData("AutoExam/Theme/Tokens.Claro.xaml")]
    public void LaProfundidad_EsUnTokenDeTemaYEsSutil_US043(string diccionario)
    {
        var pincel = Doc(diccionario).Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "LinearGradientBrush" &&
                                 e.Attributes().Any(a => a.Name.LocalName == "Key" &&
                                                         a.Value == "PincelTarjetaProfunda"));

        Assert.True(pincel is not null, $"Falta PincelTarjetaProfunda en {diccionario}.");

        var paradas = pincel!.Elements().Where(g => g.Name.LocalName == "GradientStop").ToList();

        Assert.Equal(2, paradas.Count);

        // "Sin que se sienta recargado": las dos paradas tienen que ser casi el mismo color.
        // Un degradado con mucho salto convierte la tarjeta en un banner.
        double salto = Math.Abs(
            Luminancia(paradas[0].Attribute("Color")!.Value) -
            Luminancia(paradas[1].Attribute("Color")!.Value));

        Assert.InRange(salto, 1, 40);
    }

    /// <summary>Luminancia percibida de un "#RRGGBB", en 0..255.</summary>
    private static double Luminancia(string hex)
    {
        string limpio = hex.TrimStart('#');

        int r = int.Parse(limpio.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int g = int.Parse(limpio.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int b = int.Parse(limpio.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return (0.299 * r) + (0.587 * g) + (0.114 * b);
    }

    // ------------------------------------------------------------------
    // AC — el contenido de los 3 pasos no cambió
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Material nuevo", "UsarMaterialCommand")]
    [InlineData("Examenes anteriores", "UsarExamenesAnterioresCommand")]
    [InlineData("Lo que falle", "UsarPreguntasFalladasCommand")]
    [InlineData("Examen compartido", "UsarExamenImportadoCommand")]
    public void LosCuatroOrigenes_SiguenSiendoLosMismosConSuMismoComando_US043(string titulo, string comando)
    {
        var tarjeta = Doc(Vista).Descendants()
            .Where(e => e.Name.LocalName == "RadioButton" &&
                        (e.Attribute("GroupName")?.Value ?? string.Empty) == "OrigenPreguntas")
            .FirstOrDefault(e => e.Descendants().Any(t =>
                t.Name.LocalName == "TextBlock" && t.Attribute("Text")?.Value == titulo));

        Assert.True(tarjeta is not null, $"Desapareció la opción \"{titulo}\": US-043 no cambia ninguna.");
        Assert.Contains(comando, tarjeta!.Attribute("Command")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("¿De donde salen las preguntas?")]
    [InlineData("De que material")]
    public void LasPreguntasDelPasoMaterial_SiguenDiciendoLoMismo_US043(string texto)
    {
        Assert.Contains(texto, Fuente(Vista), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Modulos")]
    [InlineData("DetectarCapitulosCommand")]
    [InlineData("PresetRango")]
    [InlineData("Tema")]
    [InlineData("PresetCantidad")]
    [InlineData("IncluirImagenes")]
    [InlineData("MinutosLimite")]
    public void LosCamposDeAlcanceYFormato_SiguenEnlazadosALoMismo_US043(string propiedad)
    {
        // El rediseño es de estilo: si un binding de Alcance o Formato desapareciera, algún
        // campo dejó de estar o cambió de nombre, que es justo lo que el criterio prohíbe.
        Assert.Contains(propiedad, Fuente(Vista), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>ResumenAlcance</c> estaba en esta lista porque la vista lo enlazaba directamente, en
    /// la tarjeta "Vas a generar" del pie del paso Formato. US-058 reemplazó esa tarjeta por el
    /// panel "Tu examen", que muestra el resumen del paso Alcance — y ese resumen lo arma
    /// <c>RefrescarPasos</c> justamente a partir de <c>ResumenAlcance</c>. El dato no se perdió:
    /// pasó de leerse en la vista a leerse en el ViewModel, que es lo que RN-68 pide.
    /// </summary>
    [Fact]
    public void ElResumenDelAlcance_SigueLlegandoALaPantalla_AunqueYaNoSeEnlaceDirecto_US058()
    {
        string vm = Fuente("AutoExam/ViewModels/AsistenteViewModel.cs");

        Assert.Contains("ResumenAlcance", vm, StringComparison.Ordinal);

        // Y el panel lo muestra a través del resumen del paso, no de una copia propia.
        Assert.Contains("Pasos[1]", Fuente(Vista), StringComparison.Ordinal);
    }
}
