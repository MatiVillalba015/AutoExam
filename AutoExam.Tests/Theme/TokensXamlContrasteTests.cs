using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// Gate objetivo de accesibilidad de US-011 (specs/02-tech-spec.md Incremento 3, AC-T36 a
/// AC-T39; specs/03-architecture.md Incremento 3 §1.2/§4.3) sobre
/// <c>Theme/Tokens.Claro.xaml</c> / <c>Theme/Tokens.Oscuro.xaml</c>.
///
/// Arranca en paralelo al developer que está iterando los valores `Color` finales — por eso
/// NO depende de código compilado ni de runtime WPF: parsea los dos diccionarios directo con
/// <c>System.Xml.Linq</c> (mismo enfoque que <c>EstilosXamlAnimacionesHoverPresionTests</c> en
/// el incremento 2) y calcula contraste/matiz con funciones puras propias, sin librería nueva.
/// El developer corre esta suite mientras ajusta colores; algunos casos (en particular los
/// pares "entre estados semánticos" de NFR-33 y el matiz violeta de superficie/borde de
/// NFR-34) están en rojo con los valores hoy commiteados a propósito — recién se ponen en verde
/// cuando el rediseño de US-011 está terminado, no antes.
///
/// Fuera de alcance a propósito: AC-T37 ("ninguna vista quedó atrás") no es verificable
/// parseando solo <c>Tokens.*.xaml</c> — se cumple por construcción según el contrato de
/// arquitectura (mismas 20 claves, mismo mecanismo <c>DynamicResource</c>/<c>TemaService</c>,
/// cero edición de <c>Views/*.xaml</c>), no por un test de este archivo.
/// </summary>
public class TokensXamlContrasteTests
{
    private static readonly string[] Temas = { "Claro", "Oscuro" };

    public static IEnumerable<object[]> TemasData() => Temas.Select(t => new object[] { t });

    private static readonly Dictionary<string, Dictionary<string, string>> Cache = new();

    private static Dictionary<string, string> ClavesDe(string tema)
    {
        if (Cache.TryGetValue(tema, out var cacheado))
        {
            return cacheado;
        }

        var ruta = ArchivoFuenteHelper.RutaFuente($"AutoExam/Theme/Tokens.{tema}.xaml");
        var documento = XDocument.Load(ruta);

        var claves = documento.Root!.Elements()
            .Where(e => e.Name.LocalName == "SolidColorBrush")
            .ToDictionary(
                e => e.Attributes().First(a => a.Name.LocalName == "Key").Value,
                e => e.Attribute("Color")!.Value);

        Cache[tema] = claves;
        return claves;
    }

    // ------------------------------------------------------------------
    // Funciones puras: parseo de color, luminancia relativa WCAG 2.1, contraste, matiz HSL.
    // Sin librería nueva — solo lo que exige specs/03-architecture.md Incremento 3 §4.3.
    // ------------------------------------------------------------------

    private static (byte R, byte G, byte B) ParsearColor(string hex)
    {
        var valor = hex.TrimStart('#');
        // Los tokens del repo son siempre #RRGGBB; se tolera #AARRGGBB por si algún valor futuro
        // trae canal alfa explícito (el alfa no participa del cálculo de contraste/matiz).
        if (valor.Length == 8)
        {
            valor = valor[2..];
        }

        var r = Convert.ToByte(valor[..2], 16);
        var g = Convert.ToByte(valor[2..4], 16);
        var b = Convert.ToByte(valor[4..6], 16);
        return (r, g, b);
    }

    private static double CanalLinealizado(byte canal)
    {
        var c = canal / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double LuminanciaRelativa(string hex)
    {
        var (r, g, b) = ParsearColor(hex);
        return 0.2126 * CanalLinealizado(r) + 0.7152 * CanalLinealizado(g) + 0.0722 * CanalLinealizado(b);
    }

    /// <summary>Fórmula estándar de contraste WCAG 2.1: (L1 + 0.05) / (L2 + 0.05), L1 &gt;= L2.</summary>
    private static double Contraste(string hexA, string hexB)
    {
        var lA = LuminanciaRelativa(hexA);
        var lB = LuminanciaRelativa(hexB);
        var (l1, l2) = lA >= lB ? (lA, lB) : (lB, lA);
        return (l1 + 0.05) / (l2 + 0.05);
    }

    /// <summary>Matiz HSL en grados [0, 360). Devuelve null para un gris puro (R=G=B), donde el
    /// matiz no está definido — ese caso se trata como "sin temperatura violeta" en NFR-34.</summary>
    private static double? MatizHsl(string hex)
    {
        var (r, g, b) = ParsearColor(hex);
        double rn = r / 255.0, gn = g / 255.0, bn = b / 255.0;
        var max = Math.Max(rn, Math.Max(gn, bn));
        var min = Math.Min(rn, Math.Min(gn, bn));
        var delta = max - min;

        if (delta < 0.0001)
        {
            return null;
        }

        double hue;
        if (max == rn)
        {
            hue = 60.0 * (((gn - bn) / delta) % 6.0);
        }
        else if (max == gn)
        {
            hue = 60.0 * (((bn - rn) / delta) + 2.0);
        }
        else
        {
            hue = 60.0 * (((rn - gn) / delta) + 4.0);
        }

        if (hue < 0)
        {
            hue += 360.0;
        }

        return hue;
    }

    /// <summary>Distancia angular entre dos matices, siempre en [0, 180] (círculo de 360°).</summary>
    private static double DiferenciaCircular(double h1, double h2)
    {
        var diferencia = Math.Abs(h1 - h2) % 360.0;
        return diferencia > 180.0 ? 360.0 - diferencia : diferencia;
    }

    // ------------------------------------------------------------------
    // NFR-32 / AC-T38 — contraste texto/fondo
    // ------------------------------------------------------------------

    private static readonly string[] ClavesDeFondo = { "PincelFondo", "PincelSuperficie", "PincelTarjeta" };

    public static IEnumerable<object[]> ParesTextoPrincipalVsFondo() =>
        Temas.SelectMany(tema => ClavesDeFondo.Select(fondo => new object[] { tema, fondo }));

    [Theory]
    [MemberData(nameof(ParesTextoPrincipalVsFondo))]
    public void PincelTexto_ContrastaAlMenos4_5_1ConCadaFondo_NFR32(string tema, string claveFondo)
    {
        var claves = ClavesDe(tema);
        var ratio = Contraste(claves["PincelTexto"], claves[claveFondo]);

        Assert.True(ratio >= 4.5,
            $"Tema {tema}: PincelTexto vs {claveFondo} = {ratio:F2}:1, requiere >= 4.5:1 (NFR-32, AC-T38).");
    }

    public static IEnumerable<object[]> ParesTextoSecundarioVsFondo() =>
        Temas.SelectMany(tema =>
            new[] { "PincelTextoSuave", "PincelTextoTenue" }.SelectMany(texto =>
                ClavesDeFondo.Select(fondo => new object[] { tema, texto, fondo })));

    [Theory]
    [MemberData(nameof(ParesTextoSecundarioVsFondo))]
    public void TextoSuaveOTenue_ContrastaAlMenos3_1ConCadaFondo_NFR32(string tema, string claveTexto, string claveFondo)
    {
        var claves = ClavesDe(tema);
        var ratio = Contraste(claves[claveTexto], claves[claveFondo]);

        Assert.True(ratio >= 3.0,
            $"Tema {tema}: {claveTexto} vs {claveFondo} = {ratio:F2}:1, requiere >= 3:1 (NFR-32, AC-T38).");
    }

    // ------------------------------------------------------------------
    // NFR-33 / AC-T38 — contraste de estados semánticos (fuerte vs. su Suave, y entre sí)
    // ------------------------------------------------------------------

    private static readonly (string Fuerte, string Suave)[] EstadosSemanticos =
    {
        ("PincelAcierto", "PincelAciertoSuave"),
        ("PincelError", "PincelErrorSuave"),
        ("PincelPendiente", "PincelPendienteSuave"),
    };

    public static IEnumerable<object[]> ParesEstadoVsSuPropioSuave() =>
        Temas.SelectMany(tema => EstadosSemanticos.Select(e => new object[] { tema, e.Fuerte, e.Suave }));

    [Theory]
    [MemberData(nameof(ParesEstadoVsSuPropioSuave))]
    public void EstadoSemantico_ContrastaAlMenos3_1ConSuPropioSuave_NFR33(string tema, string claveFuerte, string claveSuave)
    {
        var claves = ClavesDe(tema);
        var ratio = Contraste(claves[claveFuerte], claves[claveSuave]);

        Assert.True(ratio >= 3.0,
            $"Tema {tema}: {claveFuerte} vs {claveSuave} = {ratio:F2}:1, requiere >= 3:1 (NFR-33, AC-T38).");
    }

    private static readonly string[] NombresDeEstadosFuertes = { "PincelAcierto", "PincelError", "PincelPendiente" };

    public static IEnumerable<object[]> ParesEntreEstadosFuertes()
    {
        for (var i = 0; i < NombresDeEstadosFuertes.Length; i++)
        {
            for (var j = i + 1; j < NombresDeEstadosFuertes.Length; j++)
            {
                foreach (var tema in Temas)
                {
                    yield return new object[] { tema, NombresDeEstadosFuertes[i], NombresDeEstadosFuertes[j] };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ParesEntreEstadosFuertes))]
    public void EstadosSemanticosFuertes_ContrastanAlMenos3_1EntreSi_NFR33(string tema, string claveA, string claveB)
    {
        var claves = ClavesDe(tema);
        var ratio = Contraste(claves[claveA], claves[claveB]);

        Assert.True(ratio >= 3.0,
            $"Tema {tema}: {claveA} vs {claveB} = {ratio:F2}:1, requiere >= 3:1 (NFR-33, AC-T38) — deben " +
            "distinguirse uno junto a otro, ej. en BaldosaPregunta.");
    }

    // ------------------------------------------------------------------
    // NFR-34 / AC-T36 — predominancia de morado (matiz de marca + superficie/borde re-tonalizada)
    // ------------------------------------------------------------------

    private static readonly string[] ClavesDeSuperficieYBorde =
    {
        "PincelFondo", "PincelSuperficie", "PincelTarjeta", "PincelTarjetaHover", "PincelBorde", "PincelBordeFuerte",
    };

    private static readonly string[] ClavesDeMarcaSecundarias = { "PincelMarcaFuerte", "PincelMarcaSuave" };

    public static IEnumerable<object[]> ClavesEvaluadasParaPredominanciaDeMorado() =>
        Temas.SelectMany(tema =>
            ClavesDeMarcaSecundarias.Concat(ClavesDeSuperficieYBorde).Select(clave => new object[] { tema, clave }));

    [Theory]
    [MemberData(nameof(ClavesEvaluadasParaPredominanciaDeMorado))]
    public void ClaveDeMarcaOSuperficie_TieneMatizDentroDe20GradosDelVioletaDeMarca_NFR34(string tema, string clave)
    {
        var claves = ClavesDe(tema);
        var hueMarca = MatizHsl(claves["PincelMarca"]);
        var hueClave = MatizHsl(claves[clave]);

        Assert.True(hueMarca.HasValue, $"Tema {tema}: PincelMarca no puede ser un gris puro — no hay violeta de referencia.");
        Assert.True(hueClave.HasValue,
            $"Tema {tema}: '{clave}' es un gris puro sin matiz (R=G=B) — necesita una temperatura violeta " +
            "perceptible para cumplir NFR-34/AC-T36.");

        var diferencia = DiferenciaCircular(hueMarca!.Value, hueClave!.Value);
        Assert.True(diferencia <= 20.0,
            $"Tema {tema}: '{clave}' tiene matiz {hueClave:F1}°, a {diferencia:F1}° del violeta de marca " +
            $"({hueMarca:F1}°, PincelMarca) — excede ±20° (NFR-34/AC-T36).");
    }

    [Theory]
    [MemberData(nameof(TemasData))]
    public void PaletaDeCadaTema_TieneAlMenos3TonosDeMoradoPerceptiblementeDistintos_NFR34(string tema)
    {
        var claves = ClavesDe(tema);
        var candidatos = new[] { "PincelMarca" }
            .Concat(ClavesDeMarcaSecundarias)
            .Concat(ClavesDeSuperficieYBorde)
            .Select(c => claves[c])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(candidatos.Count >= 3,
            $"Tema {tema}: solo {candidatos.Count} tono(s) de color distinto(s) entre marca+superficie/borde " +
            "— NFR-34/AC-T36 exige al menos 3 tonos de morado perceptiblemente distintos.");
    }

    // ------------------------------------------------------------------
    // NFR-35 — paridad de claves entre los dos diccionarios de tema
    // ------------------------------------------------------------------

    [Fact]
    public void TokensClaroYOscuro_ExponenExactamenteElMismoSetDeClaves_NFR35()
    {
        var clarosSet = ClavesDe("Claro").Keys.ToHashSet(StringComparer.Ordinal);
        var oscurosSet = ClavesDe("Oscuro").Keys.ToHashSet(StringComparer.Ordinal);

        var soloEnClaro = clarosSet.Except(oscurosSet).ToList();
        var soloEnOscuro = oscurosSet.Except(clarosSet).ToList();

        Assert.True(soloEnClaro.Count == 0 && soloEnOscuro.Count == 0,
            $"Diferencia de claves entre temas (NFR-35) — solo en Claro: [{string.Join(", ", soloEnClaro)}]; " +
            $"solo en Oscuro: [{string.Join(", ", soloEnOscuro)}].");
    }

    // ------------------------------------------------------------------
    // AC-T39 — restricción dura: el matiz de Acierto/Error/Pendiente (y sus "Suave") no se mueve
    // hacia el violeta; solo se permite ajustar luminancia/saturación para cumplir NFR-33.
    // ------------------------------------------------------------------

    /// <summary>
    /// Matiz de referencia calculado a partir de los colores ya commiteados en
    /// <c>Tokens.Claro.xaml</c>/<c>Tokens.Oscuro.xaml</c> al momento en que se cerró el contrato
    /// de US-011 (specs/03-architecture.md Incremento 3 §4.3). El developer puede iterar
    /// luminancia/saturación de estas 6 claves para cumplir NFR-33, pero el matiz debe quedarse
    /// en la misma familia de color (verde/rojo/ámbar) — nunca correrse hacia el violeta.
    /// </summary>
    private static readonly (string Tema, string Clave, double HueBase)[] BaselinesDeMatizSemantico =
    {
        ("Claro", "PincelAcierto", 157.6),
        ("Claro", "PincelAciertoSuave", 148.2),
        ("Claro", "PincelError", 357.9),
        ("Claro", "PincelErrorSuave", 0.0),
        ("Claro", "PincelPendiente", 39.5),
        ("Claro", "PincelPendienteSuave", 41.3),
        ("Oscuro", "PincelAcierto", 150.0),
        ("Oscuro", "PincelAciertoSuave", 150.0),
        ("Oscuro", "PincelError", 358.7),
        ("Oscuro", "PincelErrorSuave", 353.3),
        ("Oscuro", "PincelPendiente", 42.2),
        ("Oscuro", "PincelPendienteSuave", 36.7),
    };

    private const double ToleranciaMatizSemantico = 12.0;

    public static IEnumerable<object[]> BaselinesDeMatizSemanticoData() =>
        BaselinesDeMatizSemantico.Select(b => new object[] { b.Tema, b.Clave, b.HueBase });

    [Theory]
    [MemberData(nameof(BaselinesDeMatizSemanticoData))]
    public void ClaveSemantica_NoCambioDeMatizRespectoAlOriginal_AC_T39(string tema, string clave, double hueBase)
    {
        var claves = ClavesDe(tema);
        var hueActual = MatizHsl(claves[clave]);

        Assert.True(hueActual.HasValue,
            $"Tema {tema}: '{clave}' quedó como gris puro (R=G=B) — perdió su identidad semántica (AC-T39).");

        var diferencia = DiferenciaCircular(hueBase, hueActual!.Value);
        Assert.True(diferencia <= ToleranciaMatizSemantico,
            $"Tema {tema}: '{clave}' tiene matiz actual {hueActual:F1}°, se alejó {diferencia:F1}° del matiz " +
            $"original {hueBase:F1}° (tolerancia ±{ToleranciaMatizSemantico}°, AC-T39) — solo se permite " +
            "ajustar luminancia/saturación, nunca el matiz.");
    }

    [Theory]
    [MemberData(nameof(BaselinesDeMatizSemanticoData))]
    public void ClaveSemantica_NoSeAcercoAlVioletaDeMarca_AC_T39(string tema, string clave, double _)
    {
        var claves = ClavesDe(tema);
        var hueMarca = MatizHsl(claves["PincelMarca"]);
        var hueClave = MatizHsl(claves[clave]);

        Assert.True(hueMarca.HasValue && hueClave.HasValue,
            $"Tema {tema}: '{clave}' o PincelMarca quedaron como gris puro — no se puede evaluar AC-T39.");

        var diferencia = DiferenciaCircular(hueMarca!.Value, hueClave!.Value);
        Assert.True(diferencia > 60.0,
            $"Tema {tema}: '{clave}' quedó a solo {diferencia:F1}° del violeta de marca ({hueMarca:F1}°) — se " +
            "corrió hacia el morado, violando la restricción semántica de AC-T39 (verde/rojo/ámbar no compiten " +
            "con el acento de marca).");
    }
}
