using System.Globalization;
using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-045 — rediseño visual de Biblioteca, y RN-53, que convierte el fondo con luz violeta y
/// las animaciones de hover/click en el estándar de toda la app.
///
/// Es un cambio de apariencia: el criterio dice que ninguna funcionalidad cambia. Lo único
/// nuevo de comportamiento es "Volver a generar", que pide otro resumen en vez de devolver el
/// guardado. Buena parte de estos tests verifica lo que tiene que haber quedado igual.
/// </summary>
public class BibliotecaRedisenadaTests
{
    private const string Vista = "AutoExam/Views/BibliotecaView.xaml";

    private static XDocument Doc(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    /// <summary>La ficha del libro abierto: lo que está dentro del ScrollViewer del detalle.</summary>
    private static XElement Detalle() => Doc(Vista).Descendants()
        .First(e => e.Name.LocalName == "Grid" &&
                    e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FichaDelLibro"));

    // ------------------------------------------------------------------
    // AC — el detalle ocupa todo el ancho disponible
    // ------------------------------------------------------------------

    /// <summary>
    /// El primer intento puso <c>MaxWidth="1400" HorizontalAlignment="Center"</c> en la ficha,
    /// para no romper el test de centrado de US-017. No alcanzaba, y encima era contraproducente:
    /// un Grid centrado se mide por su contenido, así que la columna estrella del cuerpo nunca
    /// se estiraba y la ficha se renderizaba angosta —con espacio muerto a los dos costados—
    /// aunque el tope fuera de 1400. Este test fija que la ficha no lleva ninguna de las dos
    /// propiedades: estirada es como ocupa el ancho que el criterio pide.
    /// </summary>
    [Fact]
    public void ElDetalle_OcupaTodoElAnchoDisponible_US045()
    {
        var ficha = Detalle();

        Assert.Null(ficha.Attribute("MaxWidth"));
        Assert.Null(ficha.Attribute("HorizontalAlignment"));
        Assert.Null(ficha.Attribute("Width"));

        // Era MaxWidth 640: en un monitor ancho quedaba media pantalla vacía al costado.
        var angostas = Doc(Vista).Descendants()
            .Where(e => e.Name.LocalName is "Grid" or "StackPanel" or "Border" or "ScrollViewer")
            .Select(e => e.Attribute("MaxWidth")?.Value ?? string.Empty)
            .Where(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) &&
                        n is >= 600 and <= 900)
            .ToList();

        Assert.Empty(angostas);
    }

    /// <summary>
    /// RN-13 no desaparece con el ancho completo: se muda al único texto largo de la pantalla.
    /// Los campos, los botones y los módulos son cajas cortas y estirarlos no hace ilegible
    /// ninguna línea; el resumen de "De qué trata" sí es un párrafo, y sin tope una línea suya
    /// cruzaría un monitor ancho de punta a punta.
    /// </summary>
    [Fact]
    public void ElResumenLargo_ConservaSuTopeDeAncho_RN13()
    {
        var parrafo = Doc(Vista).Descendants()
            .First(e => e.Name.LocalName == "TextBlock" &&
                        (e.Attribute("Text")?.Value ?? string.Empty)
                            .Contains("TextoDeQueTrata", StringComparison.Ordinal));

        double tope = double.Parse(parrafo.Attribute("MaxWidth")!.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture);

        Assert.InRange(tope, 600d, 1000d);
        Assert.Equal("Center", parrafo.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Wrap", parrafo.Attribute("TextWrapping")?.Value);
    }

    [Fact]
    public void ElEncabezado_TieneLaMateriaConSuColorYElTitulo_US045()
    {
        var encabezado = Detalle().Elements()
            .First(e => e.Name.LocalName == "Border");

        // La materia primero, con su punto de color (RN-30 / RN-34), como en la lista.
        Assert.Contains(encabezado.Descendants(),
            e => e.Name.LocalName == "Ellipse" &&
                 (e.Attribute("Fill")?.Value ?? string.Empty)
                     .Contains("ColorDeLaMateria", StringComparison.Ordinal));

        Assert.Contains(encabezado.Descendants(),
            e => e.Name.LocalName == "TextBlock" &&
                 (e.Attribute("Text")?.Value ?? string.Empty)
                     .Contains("MateriaEnMayusculas", StringComparison.Ordinal));

        Assert.Contains(encabezado.Descendants(),
            e => e.Name.LocalName == "TextBlock" &&
                 (e.Attribute("Style")?.Value ?? string.Empty).Contains("TxtTitulo", StringComparison.Ordinal) &&
                 (e.Attribute("Text")?.Value ?? string.Empty).Contains("TituloLibro", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("PaginasTexto")]
    [InlineData("ArchivoTexto")]
    [InlineData("ResumenModulos")]
    [InlineData("FechaSubidaTexto")]
    public void ElEncabezado_MuestraLosCuatroDatosDelLibro_US045(string dato)
    {
        var encabezado = Detalle().Elements().First(e => e.Name.LocalName == "Border");

        Assert.Contains(encabezado.Descendants(),
            e => e.Name.LocalName == "TextBlock" &&
                 (e.Attribute("Text")?.Value ?? string.Empty).Contains(dato, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("GuardarCommand")]
    [InlineData("QuitarCommand")]
    public void ElEncabezado_LlevaLasDosAccionesDelLibro_US045(string comando)
    {
        var encabezado = Detalle().Elements().First(e => e.Name.LocalName == "Border");

        Assert.Contains(encabezado.Descendants(),
            e => (e.Attribute("Command")?.Value ?? string.Empty).Contains(comando, StringComparison.Ordinal));
    }

    [Fact]
    public void DebajoDelEncabezado_HayDosColumnas_EdicionYContenido_US045()
    {
        var cuerpo = Detalle().Elements()
            .First(e => e.Name.LocalName == "Grid");

        var columnas = cuerpo.Elements()
            .First(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .ToList();

        Assert.Equal(3, columnas.Count);

        var izquierda = cuerpo.Elements()
            .First(e => (e.Attribute("Grid.Column")?.Value ?? string.Empty) == "0");

        var derecha = cuerpo.Elements()
            .First(e => (e.Attribute("Grid.Column")?.Value ?? string.Empty) == "2");

        // A la izquierda lo que se edita.
        Assert.Contains(izquierda.Descendants(),
            e => (e.Attribute("Text")?.Value ?? string.Empty).Contains("TituloLibro", StringComparison.Ordinal));
        Assert.Contains(izquierda.Descendants(),
            e => (e.Attribute("Text")?.Value ?? string.Empty).Contains("Binding Materia", StringComparison.Ordinal));

        // A la derecha lo que se lee.
        Assert.Contains(derecha.Descendants(),
            e => (e.Attribute("Command")?.Value ?? string.Empty)
                .Contains("VerDeQueTrataCommand", StringComparison.Ordinal));
        Assert.Contains(derecha.Descendants(),
            e => (e.Attribute("ItemsSource")?.Value ?? string.Empty)
                .Contains("Modulos", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // AC — "Volver a generar" el resumen
    // ------------------------------------------------------------------

    [Fact]
    public void JuntoAVerDeQueTrata_HayUnBotonParaPedirOtroResumen_US045()
    {
        var boton = Doc(Vista).Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 (e.Attribute("Command")?.Value ?? string.Empty)
                                     .Contains("VolverAGenerarCommand", StringComparison.Ordinal));

        Assert.True(boton is not null, "Falta el botón \"Volver a generar\" (US-045).");
        Assert.Equal("Volver a generar", boton!.Attribute("Content")?.Value);

        // Sólo cuando ya hay un resumen guardado: sin uno previo, "Ver de qué trata" ya genera
        // el primero y los dos botones harían lo mismo.
        Assert.Contains("TieneResumenGuardado", boton.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VolverAGenerar_IgnoraElResumenGuardado_YVerDeQueTrataNo_US045()
    {
        // El resumen se guarda para no volver a pagar la cuota (RN-17). "Volver a generar" es
        // la única puerta que saltea esa caché; si no la salteara, devolvería el mismo texto.
        string codigo = Fuente("AutoExam/ViewModels/BibliotecaViewModel.cs");

        Assert.Contains("VerDeQueTrataAsync() => ResumirAsync(forzar: false)", codigo, StringComparison.Ordinal);
        Assert.Contains("VolverAGenerarAsync() => ResumirAsync(forzar: true)", codigo, StringComparison.Ordinal);

        // Y el atajo a lo guardado está condicionado a no estar forzando.
        Assert.Contains("libro.TieneResumen && !forzar", codigo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — los módulos, en una grilla de dos columnas
    // ------------------------------------------------------------------

    [Fact]
    public void LosModulos_SeListanEnDosColumnas_NoEnUnaTiraVertical_US045()
    {
        var lista = Doc(Vista).Descendants()
            .First(e => e.Name.LocalName == "ItemsControl" &&
                        (e.Attribute("ItemsSource")?.Value ?? string.Empty)
                            .Contains("Modulos", StringComparison.Ordinal));

        var panel = lista.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "UniformGrid");

        Assert.True(panel is not null, "Los módulos no se reparten en una grilla (US-045).");
        Assert.Equal("2", panel!.Attribute("Columns")?.Value);
    }

    /// <summary>
    /// Venía plegado de cuando el detalle era una columna angosta y los módulos eran una tira
    /// vertical que empujaba todo lo demás fuera de la pantalla. En dos columnas, en la mitad
    /// derecha de una ficha a ancho completo, entran a la vista sin tapar nada, así que
    /// esconderlos solo agrega un click para ver de qué partes está hecho el libro.
    /// </summary>
    [Fact]
    public void LosModulos_SeVenSinTenerQueAbrirNada_US045()
    {
        var tarjeta = Doc(Vista).Descendants()
            .First(e => e.Name.LocalName == "CardExpander" &&
                        e.Descendants().Any(d => d.Name.LocalName == "ItemsControl" &&
                                                 (d.Attribute("ItemsSource")?.Value ?? string.Empty)
                                                     .Contains("Modulos", StringComparison.Ordinal)));

        Assert.Equal("True", tarjeta.Attribute("IsExpanded")?.Value);
    }

    [Fact]
    public void CadaModulo_MuestraSuEtiquetaSuNombreYSuRangoDePaginas_US045()
    {
        var plantilla = Doc(Vista).Descendants()
            .First(e => e.Name.LocalName == "ItemsControl" &&
                        (e.Attribute("ItemsSource")?.Value ?? string.Empty)
                            .Contains("Modulos", StringComparison.Ordinal))
            .Descendants()
            .First(e => e.Name.LocalName == "DataTemplate");

        // La etiqueta "M1" sale de la posición en la lista, no de un campo guardado: agregar o
        // quitar un módulo tiene que renumerar el resto solo.
        Assert.Contains(plantilla.Descendants(),
            r => r.Name.LocalName == "Run" && r.Attribute("Text")?.Value == "M");

        Assert.Contains(plantilla.Descendants(),
            r => (r.Attribute("Text")?.Value ?? string.Empty)
                .Contains("AlternationIndex", StringComparison.Ordinal));

        Assert.Contains(plantilla.Descendants(),
            e => (e.Attribute("Text")?.Value ?? string.Empty).Contains("Nombre", StringComparison.Ordinal));

        foreach (string extremo in new[] { "DesdePagina", "HastaPagina" })
        {
            Assert.Contains(plantilla.Descendants(),
                r => (r.Attribute("Text")?.Value ?? string.Empty).Contains(extremo, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void LaGrillaDeModulos_NoReemplazaAlEditor_US045()
    {
        // El criterio dice que no cambia ninguna funcionalidad: la grilla es de sólo lectura y
        // los módulos se siguen editando donde se editaban.
        string xaml = Fuente(Vista);

        foreach (string herramienta in new[]
                 {
                     "DetectarCapitulosCommand", "AgregarModuloCommand",
                     "QuitarModuloCommand", "DividirCommand",
                 })
        {
            Assert.Contains(herramienta, xaml, StringComparison.Ordinal);
        }

        Assert.Contains(Doc(Vista).Descendants(), e => e.Name.LocalName == "DataGrid");
    }

    // ------------------------------------------------------------------
    // RN-53 — el fondo y las animaciones son el estándar de la app
    // ------------------------------------------------------------------

    [Fact]
    public void Biblioteca_TieneElMismoFondoDeLuzVioletaQueElHistorial_RN53()
    {
        var manchas = Doc(Vista).Descendants()
            .Where(e => e.Name.LocalName == "Ellipse" &&
                        e.Descendants().Any(g => g.Name.LocalName == "RadialGradientBrush"))
            .ToList();

        Assert.NotEmpty(manchas);

        foreach (var mancha in manchas)
        {
            double opacidad = double.Parse(mancha.Attribute("Opacity")!.Value, CultureInfo.InvariantCulture);

            Assert.InRange(opacidad, 0.01, 0.4);
        }

        var lienzo = manchas[0].Ancestors().First(a => a.Name.LocalName == "Canvas");

        Assert.Equal("False", lienzo.Attribute("IsHitTestVisible")?.Value);
    }

    [Fact]
    public void LaConfiguracionInicialDeLaClave_QuedaSinEseFondo_RN53()
    {
        // Es la excepción que la regla nombra: US-042 no se toca.
        var manchas = Doc("AutoExam/Views/OnboardingView.xaml").Descendants()
            .Where(e => e.Name.LocalName == "Ellipse" &&
                        e.Descendants().Any(g => g.Name.LocalName == "RadialGradientBrush"))
            .ToList();

        Assert.Empty(manchas);
    }

    [Fact]
    public void LaFichaDeUnLibro_RespondeAlMouseComoElRestoDeLaApp_RN53()
    {
        var estilo = Doc("AutoExam/Theme/Estilos.xaml").Descendants()
            .First(e => e.Name.LocalName == "Style" &&
                        e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == "ItemLibro"));

        // El mismo anillo de acento que las tarjetas del menú y las del asistente.
        Assert.Contains(estilo.Descendants(),
            e => e.Name.LocalName == "Border" &&
                 e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "AnilloHover"));

        var hover = estilo.Descendants()
            .First(e => e.Name.LocalName == "MultiTrigger" &&
                        e.Descendants().Any(c =>
                            c.Name.LocalName == "Condition" &&
                            (c.Attribute("Property")?.Value ?? string.Empty)
                                .EndsWith("IsMouseOver", StringComparison.Ordinal)));

        var zoom = hover.Descendants()
            .Where(a => a.Name.LocalName == "DoubleAnimation" &&
                        (a.Attribute("Storyboard.TargetName")?.Value ?? string.Empty) == "Escala")
            .Select(a => double.Parse(a.Attribute("To")!.Value, CultureInfo.InvariantCulture))
            .ToList();

        Assert.Contains(zoom, v => v is > 1 and <= 1.03);
        Assert.Contains(zoom, v => v == 1);

        Assert.Contains(hover.Descendants(),
            c => c.Name.LocalName == "Condition" &&
                 (c.Attribute("Property")?.Value ?? string.Empty)
                     .Contains("MovimientoReducido", StringComparison.Ordinal) &&
                 (c.Attribute("Value")?.Value ?? string.Empty) == "False");
    }

    // ------------------------------------------------------------------
    // AC — ninguna funcionalidad cambia
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SoltarCommand")]
    [InlineData("ElegirArchivoCommand")]
    [InlineData("Filtro")]
    [InlineData("LimpiarFiltroCommand")]
    [InlineData("LibrosPorMateria")]
    [InlineData("Materias")]
    [InlineData("RenombrarMateriaCommand")]
    [InlineData("EliminarMateriaCommand")]
    [InlineData("UsarMateriaCommand")]
    [InlineData("VerDeQueTrataCommand")]
    [InlineData("CerrarDeQueTrataCommand")]
    [InlineData("GuardarCommand")]
    [InlineData("QuitarCommand")]
    public void ElRedisenio_NoDesengancholNadaDeLoQueYaFuncionaba_US045(string enlace)
    {
        Assert.Contains(enlace, Fuente(Vista), StringComparison.Ordinal);
    }

    [Fact]
    public void LaEtiquetaDeUnModulo_CuentaDesdeUno_US045()
    {
        var converter = new AutoExam.MasUnoConverter();

        Assert.Equal("1", converter.Convert(0, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("4", converter.Convert(3, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
