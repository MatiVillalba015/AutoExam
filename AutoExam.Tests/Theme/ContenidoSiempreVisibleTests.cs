using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// RN-38 — el contenido esencial de una tarjeta o botón nunca depende del hover para verse.
///
/// El bug: la tarjeta "Generar examen" del menú se veía vacía. La causa real resultó ser al
/// revés de como se percibe: la tarjeta no estaba en blanco *hasta* pasar el mouse, se vaciaba
/// *con* el mouse encima. El <c>ContentPresenter</c> vivía adentro del borde de fondo y la capa
/// de realce del hover se dibujaba después, encima; como <c>PincelTarjetaHover</c> es un pincel
/// OPACO (#2C2741 en oscuro, #DFDDEB en claro), al llegar a Opacity 1 tapaba el contenido
/// entero.
///
/// No era un problema de una tarjeta: seis templates de Estilos.xaml tenían la misma
/// estructura, incluidas las opciones del examen y las fichas de la biblioteca.
///
/// Esta suite fija la regla estructural que lo evita: dentro de un ControlTemplate, ninguna
/// capa que el hover vuelve visible puede dibujarse DESPUÉS del contenido.
/// </summary>
public class ContenidoSiempreVisibleTests
{
    private static XDocument Estilos() =>
        XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Theme/Estilos.xaml"));

    private static string Nombre(XElement e) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value ?? string.Empty;

    private static string ClaveDelEstilo(XElement dentroDelTemplate)
    {
        var estilo = dentroDelTemplate.Ancestors()
            .FirstOrDefault(a => a.Name.LocalName == "Style" &&
                                 a.Attributes().Any(at => at.Name.LocalName == "Key"));

        return estilo?.Attributes().First(a => a.Name.LocalName == "Key").Value ?? "(sin clave)";
    }

    /// <summary>Todos los ControlTemplate del archivo que tienen un ContentPresenter.</summary>
    private static IEnumerable<XElement> TemplatesConContenido() =>
        Estilos().Descendants()
            .Where(e => e.Name.LocalName == "ControlTemplate")
            .Where(t => t.Descendants().Any(d => d.Name.LocalName == "ContentPresenter"));

    [Fact]
    public void NingunaCapaDeHover_SeDibujaEncimaDelContenido_RN38()
    {
        var infractores = new List<string>();

        foreach (var template in TemplatesConContenido())
        {
            // El orden de dibujo dentro de un Grid es el orden del documento: lo último se
            // pinta arriba. Con Descendants() en orden, comparar posiciones alcanza.
            var enOrden = template.Descendants().ToList();

            int contenido = enOrden.FindIndex(e => e.Name.LocalName == "ContentPresenter");

            if (contenido < 0)
            {
                continue;
            }

            // Una capa de realce: un Border con Opacity="0" que el hover sube a 1. Se
            // reconoce por el pincel, que es el mismo en los seis templates.
            var realcesDespues = enOrden
                .Skip(contenido)
                .Where(e => e.Name.LocalName == "Border" &&
                            (e.Attribute("Opacity")?.Value ?? string.Empty) == "0" &&
                            (e.Attribute("Background")?.Value ?? string.Empty)
                                .Contains("PincelTarjetaHover", StringComparison.Ordinal))
                .ToList();

            foreach (var realce in realcesDespues)
            {
                infractores.Add($"{ClaveDelEstilo(template)} → {Nombre(realce)}");
            }
        }

        Assert.True(infractores.Count == 0,
            "Estas capas de hover se dibujan por encima del contenido, así que al pasar el mouse " +
            "lo tapan (PincelTarjetaHover es opaco) y el control queda vacío:\n  " +
            string.Join("\n  ", infractores));
    }

    [Fact]
    public void ElContenido_NoViveDentroDeNingunElementoQueArranqueInvisible_RN38()
    {
        // La otra forma de que el contenido dependa del hover: meterlo adentro de una capa
        // que empieza en Opacity 0 y sólo el hover levanta.
        var infractores = new List<string>();

        foreach (var template in TemplatesConContenido())
        {
            foreach (var presenter in template.Descendants().Where(e => e.Name.LocalName == "ContentPresenter"))
            {
                var invisible = presenter.Ancestors()
                    .TakeWhile(a => a.Name.LocalName != "ControlTemplate")
                    .FirstOrDefault(a => (a.Attribute("Opacity")?.Value ?? string.Empty) == "0" ||
                                         (a.Attribute("Visibility")?.Value ?? string.Empty) == "Collapsed");

                if (invisible is not null)
                {
                    infractores.Add($"{ClaveDelEstilo(template)} → dentro de {Nombre(invisible)}");
                }
            }
        }

        Assert.Empty(infractores);
    }

    [Fact]
    public void LosSeisTemplatesCorregidos_SiguenTeniendoSuContenido()
    {
        // Al sacar el ContentPresenter de adentro del Border de fondo es fácil borrarlo sin
        // querer: el control seguiría compilando y andando, sólo que vacío para siempre.
        string[] esperados =
        {
            "Chip", "ChipAccion", "OpcionExamen", "TarjetaAcceso", "FilaDeActividad", "ItemLibro",
        };

        var doc = Estilos();

        foreach (string clave in esperados)
        {
            var estilo = doc.Descendants()
                .Single(e => e.Name.LocalName == "Style" &&
                             e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave));

            Assert.True(
                estilo.Descendants().Any(d => d.Name.LocalName == "ContentPresenter"),
                $"El template de {clave} se quedó sin ContentPresenter: el control se dibujaría vacío.");
        }
    }

    [Fact]
    public void ElContenido_ConservaSuEspaciado_AlSalirDelBordeDeFondo()
    {
        // El padding lo daba el Border que envolvía al contenido. Al mover el contenido afuera
        // hay que reponerlo como Margin, si no el texto queda pegado al borde de la tarjeta.
        string[] conPadding = { "Chip", "ChipAccion", "OpcionExamen", "TarjetaAcceso", "FilaDeActividad" };

        var doc = Estilos();

        foreach (string clave in conPadding)
        {
            var presenter = doc.Descendants()
                .Single(e => e.Name.LocalName == "Style" &&
                             e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave))
                .Descendants().First(d => d.Name.LocalName == "ContentPresenter");

            Assert.Contains("TemplateBinding Padding", presenter.Attribute("Margin")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------
    // US-029 — descripción al mantener el mouse encima
    // ------------------------------------------------------------------

    [Fact]
    public void LosTooltips_AparecenDebajoDelBoton_US029()
    {
        // El criterio dice "aparece una breve descripción de qué hace ese botón DEBAJO de él".
        // Va como estilo implícito de ToolTip para que ninguna pantalla pueda quedarse afuera.
        var estilo = Estilos().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                 (e.Attribute("TargetType")?.Value ?? string.Empty) == "ToolTip" &&
                                 !e.Attributes().Any(a => a.Name.LocalName == "Key"));

        Assert.True(estilo is not null,
            "No hay estilo implícito de ToolTip: cada botón tendría que declarar dónde aparece el globo.");

        var placement = estilo!.Elements()
            .FirstOrDefault(s => (s.Attribute("Property")?.Value ?? string.Empty) == "Placement");

        Assert.True(placement is not null, "El tooltip no fija dónde aparece.");
        Assert.Equal("Bottom", placement!.Attribute("Value")?.Value);
    }

    [Theory]
    [InlineData("AutoExam/Views/AjustesView.xaml")]
    [InlineData("AutoExam/Views/AsistenteView.xaml")]
    [InlineData("AutoExam/Views/BibliotecaView.xaml")]
    [InlineData("AutoExam/Views/HistorialView.xaml")]
    [InlineData("AutoExam/Views/ExamenView.xaml")]
    [InlineData("AutoExam/Views/OnboardingView.xaml")]
    public void CadaPantallaPrincipal_TieneDescripcionesEnSusBotones_US029(string ruta)
    {
        // No se puede exigir tooltip en TODO botón —los del menú ya tienen su descripción
        // permanente y RN-38 dice que ahí el tooltip sobra—, pero una pantalla entera sin
        // ninguno es señal de que se pasó por alto.
        string xaml = File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

        int tooltips = xaml.Split("ToolTip=").Length - 1;

        Assert.True(tooltips >= 2,
            $"{ruta} tiene {tooltips} tooltip(s): sus botones no explican qué hacen al mantener el mouse.");
    }

    [Fact]
    public void LasTarjetasDelMenu_NoNecesitanTooltip_PorqueYaExplicanQueHacen_RN38()
    {
        // RN-38 acota el tooltip a "una descripción adicional que no tenía lugar fijo en el
        // layout". La tarjeta del menú ya muestra la suya siempre, así que un globo encima
        // sería repetir lo que se está leyendo.
        var tarjeta = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/InicioView.xaml"))
            .Descendants()
            .First(e => e.Name.LocalName == "Button" &&
                        (e.Attribute("Style")?.Value ?? string.Empty).Contains("TarjetaAcceso"));

        Assert.Null(tarjeta.Attribute("ToolTip"));

        bool descripcionPermanente = tarjeta.Descendants()
            .Any(d => (d.Attribute("Text")?.Value ?? string.Empty).Contains("Descripcion", StringComparison.Ordinal));

        Assert.True(descripcionPermanente,
            "La tarjeta del menú no muestra su descripción de forma permanente (RN-38).");
    }
}
