using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// US-015 — emojis contextuales en una lista acotada de textos (RN-12).
///
/// La regla que de verdad hay que sostener no es "estos textos tienen emoji" sino la de al
/// lado: "y ningún otro". Un emoji suelto agregado de paso en cualquier vista no rompe nada
/// visible, así que sin un test que enumere la lista completa, la restricción de RN-12 se
/// diluye sola con el tiempo. Por eso <see cref="NingunOtroTexto_TieneEmoji_RN12"/> es la
/// pieza central de esta suite y no un extra.
///
/// Los emojis se escriben en el XAML como referencias de carácter (<c>&amp;#x1F4DD;</c>) y no
/// como el carácter literal: las vistas del proyecto son ASCII puro a propósito —se escribe
/// "estadisticas" sin tilde— y una referencia respeta esa convención sin depender de cómo esté
/// guardado el archivo.
/// </summary>
public class EmojisContextualesTests
{
    /// <summary>
    /// La lista acotada de RN-12: superficie → emoji esperado. Cualquier emoji en las vistas
    /// que no esté acá hace fallar la suite.
    /// </summary>
    private static readonly (string Vista, string Emoji, string Texto)[] Lista =
    {
        ("AutoExam/Views/AsistenteView.xaml",  "1F4DD", "Nuevo examen"),      // memo: examen
        ("AutoExam/Views/BibliotecaView.xaml", "1F4DA", "Libros"),            // libros: material
        ("AutoExam/Views/HistorialView.xaml",  "1F4CA", "Historial"),         // grafico: estadisticas
        ("AutoExam/Views/HistorialView.xaml",  "1F5D1", "Borrar historial"),  // tacho: borrar
        ("AutoExam/Views/ExamenView.xaml",     "1F389", ""),                  // fiesta: felicitacion US-013
    };

    private static readonly string[] Vistas = Directory
        .GetFiles(Path.GetDirectoryName(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/ExamenView.xaml"))!, "*.xaml")
        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Referencias de carácter a un plano astral (donde viven los emojis).</summary>
    private static readonly Regex RefEmoji =
        new(@"&#x([0-9A-Fa-f]{4,6});", RegexOptions.Compiled);

    // ------------------------------------------------------------------
    // AC — cada texto de la lista lleva su emoji, sin perder el texto
    // ------------------------------------------------------------------

    [Fact]
    public void CadaTextoDeLaLista_LlevaSuEmojiYConservaElTexto()
    {
        foreach (var (vista, emoji, texto) in Lista)
        {
            string contenido = File.ReadAllText(ArchivoFuenteHelper.RutaFuente(vista));

            Assert.True(contenido.Contains($"&#x{emoji};", StringComparison.OrdinalIgnoreCase),
                $"{vista} debería llevar el emoji U+{emoji} (US-015).");

            // "sin reemplazar el texto": el literal tiene que seguir estando.
            if (texto.Length > 0)
            {
                Assert.True(contenido.Contains(texto, StringComparison.Ordinal),
                    $"El emoji no puede reemplazar el texto \"{texto}\" en {vista}.");
            }
        }
    }

    [Fact]
    public void ElMensajeDeFelicitacion_LlevaEmojiSinTocarLaConstanteDeUS013()
    {
        var doc = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/ExamenView.xaml"));

        // US-013 fija el literal por contrato: el emoji lo acompaña desde otro elemento, nunca
        // metido dentro de la constante.
        var conLaConstante = doc.Descendants()
            .Where(e => e.Name.LocalName == "TextBlock")
            .Where(e => (e.Attribute("Text")?.Value ?? string.Empty).Contains("MensajeFelicitacion"))
            .ToList();

        var textBlock = Assert.Single(conLaConstante);

        var contenedor = textBlock.Parent;
        Assert.True(contenedor is not null, "El mensaje de felicitación quedó sin contenedor.");

        bool hayEmojiAlLado = contenedor!.Elements()
            .Any(e => (e.Attribute("Text")?.Value ?? string.Empty).Contains("&#x1F389;", StringComparison.OrdinalIgnoreCase)
                   || (e.Attribute("Text")?.Value ?? string.Empty).Contains("\U0001F389", StringComparison.Ordinal));

        Assert.True(hayEmojiAlLado,
            "El mensaje de felicitación debería estar acompañado por el emoji, en un elemento aparte.");
    }

    // ------------------------------------------------------------------
    // RN-12 — la lista es acotada: nada de emojis "a mansalva"
    // ------------------------------------------------------------------

    [Fact]
    public void NingunOtroTexto_TieneEmoji_RN12()
    {
        var permitidos = Lista.Select(l => l.Emoji.ToUpperInvariant()).ToHashSet();
        var intrusos = new List<string>();

        foreach (string ruta in Vistas)
        {
            string contenido = File.ReadAllText(ruta);
            string nombre = Path.GetFileName(ruta);

            // Referencias de carácter fuera del BMP.
            foreach (Match m in RefEmoji.Matches(contenido))
            {
                string hex = m.Groups[1].Value.ToUpperInvariant();
                int cp = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                // FE0F es el selector de presentación emoji: acompaña, no es un emoji propio.
                if (cp == 0xFE0F || cp <= 0xFFFF)
                {
                    continue;
                }

                if (!permitidos.Contains(hex.TrimStart('0')) && !permitidos.Contains(hex))
                {
                    intrusos.Add($"{nombre}: &#x{hex};");
                }
            }

            // Y emojis escritos como carácter literal, por si alguien los pega directo.
            foreach (var runa in contenido.EnumerateRunes())
            {
                if (runa.Value > 0xFFFF && !permitidos.Contains(runa.Value.ToString("X", CultureInfo.InvariantCulture)))
                {
                    intrusos.Add($"{nombre}: U+{runa.Value:X} literal");
                }
            }
        }

        Assert.True(intrusos.Count == 0,
            "RN-12: sólo los textos de la lista acotada llevan emoji. Aparecieron otros: " +
            string.Join(", ", intrusos.Distinct()));
    }

    [Fact]
    public void LosEmojisSeEscribenComoReferencia_NoComoCaracterLiteral()
    {
        // Las vistas son ASCII a propósito (ver "estadisticas" sin tilde en HistorialView).
        // Un emoji literal ataría el render a que el archivo se guarde siempre en UTF-8.
        foreach (string ruta in Vistas)
        {
            var literales = File.ReadAllText(ruta).EnumerateRunes()
                .Where(r => r.Value > 0xFFFF)
                .Select(r => $"U+{r.Value:X}")
                .Distinct()
                .ToList();

            Assert.True(literales.Count == 0,
                $"{Path.GetFileName(ruta)} tiene emojis como carácter literal ({string.Join(", ", literales)}); " +
                "usá una referencia de carácter (&#x...;) como el resto de las vistas.");
        }
    }
}
