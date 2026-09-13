using System.Globalization;
using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-017 — el contenido se centra al maximizar, en vez de quedar pegado a un borde (RN-13).
///
/// Las vistas ya tenían un ancho máximo de diseño, así que el texto nunca se estiraba; el
/// problema era el otro: con <c>HorizontalAlignment="Left"</c>, todo ese espacio sobrante en un
/// monitor ancho quedaba junto, a la derecha, y la app se veía escorada.
///
/// Las dos formas válidas de centrar son <c>Center</c> y <c>Stretch</c> acotado por
/// <c>MaxWidth</c>: cuando el tope es menor que el espacio disponible, WPF reparte el sobrante
/// a los dos lados y el resultado en pantalla es el mismo. Cuál usar depende del elemento, y no
/// es una cuestión de gusto: un <c>Grid</c> con columnas <c>*</c> centrado se mide por su
/// contenido —las columnas <c>*</c> no se estiran— y entonces la pantalla cambia de ancho según
/// qué se esté mostrando adentro. Esos casos van con <c>Stretch</c> (US-058 en el asistente).
/// Lo que sigue prohibido es <c>Left</c> y <c>Right</c>, que es lo que US-017 vino a arreglar.
///
/// La verificación es estructural sobre el XAML del checkout: comprueba la propiedad de layout
/// que produce el centrado, no un render. Un test de píxeles necesitaría levantar ventanas
/// reales a varias resoluciones, y lo que se puede romper en un refactor es exactamente este
/// atributo.
/// </summary>
public class CentradoPantallaCompletaTests
{
    /// <summary>
    /// Piso a partir del cual un <c>MaxWidth</c> es "columna de contenido de una pantalla" y no
    /// una caja chica (una imagen, un estado vacío, un texto suelto). Las columnas del proyecto
    /// van de 620 a 920; las cajas chicas, de 260 a 360.
    /// </summary>
    private const double AnchoDeColumna = 600;

    private static readonly string CarpetaVistas =
        Path.GetDirectoryName(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/ExamenView.xaml"))!;

    private static IEnumerable<(string Vista, XElement Elemento, double Ancho, string Alineacion)> Columnas()
    {
        foreach (string ruta in Directory.GetFiles(CarpetaVistas, "*.xaml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var e in XDocument.Load(ruta).Descendants())
            {
                string crudo = e.Attribute("MaxWidth")?.Value ?? string.Empty;

                if (!double.TryParse(crudo, NumberStyles.Float, CultureInfo.InvariantCulture, out double ancho) ||
                    ancho < AnchoDeColumna)
                {
                    continue;
                }

                string alineacion = e.Attribute("HorizontalAlignment")?.Value ?? string.Empty;

                yield return (Path.GetFileName(ruta), e, ancho, alineacion);
            }
        }
    }

    // ------------------------------------------------------------------
    // AC — centrado, no pegado a un borde
    // ------------------------------------------------------------------

    [Fact]
    public void NingunaColumnaDeContenido_QuedaPegadaAUnBorde()
    {
        var escoradas = Columnas()
            .Where(c => c.Alineacion is "Left" or "Right")
            .Select(c => $"{c.Vista}: <{c.Elemento.Name.LocalName} MaxWidth=\"{c.Ancho}\" HorizontalAlignment=\"{c.Alineacion}\">")
            .ToList();

        Assert.True(escoradas.Count == 0,
            "Con la ventana maximizada estas columnas quedan pegadas a un borde en vez de centradas (US-017):\n  " +
            string.Join("\n  ", escoradas));
    }

    [Fact]
    public void LasColumnasDeContenido_DeclaranElCentradoExplicitamente()
    {
        // Explícito y no por omisión: aunque Stretch sea el default, dejarlo implícito hace que
        // un cambio de alineación no se vea en el diff y que no quede dicho que el centrado de
        // esa columna es una decisión y no una casualidad.
        var sinDeclarar = Columnas()
            .Where(c => c.Alineacion.Length == 0)
            .Select(c => $"{c.Vista}: <{c.Elemento.Name.LocalName} MaxWidth=\"{c.Ancho}\">")
            .ToList();

        Assert.True(sinDeclarar.Count == 0,
            "Estas columnas no declaran su HorizontalAlignment (Center, o Stretch acotado por MaxWidth):\n  " +
            string.Join("\n  ", sinDeclarar));
    }

    [Fact]
    public void CadaPantallaPrincipal_TieneSuColumnaDeContenidoCentrada()
    {
        // Guarda contra el falso verde: si alguien borrara los MaxWidth, los dos tests de arriba
        // pasarían sin nada que verificar.
        string[] pantallas =
        {
            "AsistenteView.xaml", "ExamenView.xaml", "HistorialView.xaml",
            "AjustesView.xaml", "BibliotecaView.xaml",
        };

        var centradasPorVista = Columnas()
            .Where(c => c.Alineacion is "Center" or "Stretch")
            .GroupBy(c => c.Vista, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (string pantalla in pantallas)
        {
            Assert.True(centradasPorVista.TryGetValue(pantalla, out int n) && n > 0,
                $"{pantalla} no tiene ninguna columna de contenido centrada ni acotada (US-017).");
        }
    }

    // ------------------------------------------------------------------
    // RN-13 / AC — sigue habiendo tope de ancho, y la ventana sigue teniendo mínimo
    // ------------------------------------------------------------------

    [Fact]
    public void ElContenido_ConservaSuTopeDeAncho_RN13()
    {
        // Centrar sin tope dejaría el texto estirado de punta a punta en un monitor ancho, que
        // es la otra mitad de lo que pide RN-13.
        Assert.NotEmpty(Columnas().ToList());
    }

    [Fact]
    public void LaVentana_ConservaSuTamanioMinimo_ParaQueElContenidoSigaUsableEnChico()
    {
        var ventana = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/MainWindow.xaml")).Root!;

        string minAncho = ventana.Attribute("MinWidth")?.Value ?? string.Empty;
        string minAlto = ventana.Attribute("MinHeight")?.Value ?? string.Empty;

        Assert.False(string.IsNullOrWhiteSpace(minAncho), "MainWindow perdió su MinWidth.");
        Assert.False(string.IsNullOrWhiteSpace(minAlto), "MainWindow perdió su MinHeight.");

        // El mínimo tiene que dar lugar a la columna más ancha más la navegación lateral; si no,
        // centrar no alcanza y aparece recorte.
        Assert.True(double.Parse(minAncho, CultureInfo.InvariantCulture) >= 900,
            $"MinWidth={minAncho}: muy chico para la columna de contenido más ancha del proyecto.");
    }
}
