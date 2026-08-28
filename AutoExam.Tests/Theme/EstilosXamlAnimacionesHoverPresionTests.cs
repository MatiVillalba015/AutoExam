using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// Cobertura estructural de US-008 (specs/02-tech-spec.md Incremento 2; NFR-20 a NFR-23,
/// AC-T23 a AC-T26) sobre los 7 <c>ControlTemplate</c> de <c>Theme/Estilos.xaml</c> alcanzados
/// (<c>Chip</c>, <c>ChipAccion</c>, <c>OpcionExamen</c>, <c>BaldosaPregunta</c>,
/// <c>ItemNavegacion</c>, <c>ItemLibro</c>, <c>ZonaSoltar</c>) — contrato ya cerrado en
/// specs/03-architecture.md (Incremento 2) §3.4 y en la sección "Decisiones de diseño" (US-008)
/// de specs/02-tech-spec.md.
///
/// Parsea el XAML directo del checkout con <c>System.Xml.Linq</c>, SIN levantar runtime WPF
/// (team-roster.yaml, <c>test-dev-animaciones-shell</c>): evita depender de si el recurso
/// resuelve en tiempo real solo para leer qué nombre de recurso quedó escrito en un atributo, y
/// corre sin afinidad de hilo STA.
///
/// Nota sobre <c>StaticResource</c> vs. <c>DynamicResource</c>: el contrato original (tech-spec/
/// arquitectura) asumía <c>DynamicResource</c> para <c>DuracionHover</c>/<c>DuracionPresion</c>/
/// <c>SuavizadoSalida</c> dentro de los <c>Storyboard</c>. El developer encontró un problema real
/// al implementar: WPF no puede <c>Freeze</c> un <c>Storyboard</c> usado en
/// <c>Trigger.EnterActions</c>/<c>ExitActions</c> si sus <c>Duration</c>/<c>EasingFunction</c>
/// usan <c>DynamicResource</c> (excepción reproducible instanciando <c>ExamenView</c>). Pasar a
/// <c>StaticResource</c> es seguro porque <c>Theme/Estilos.xaml</c> nunca se reemplaza al
/// cambiar de tema (solo <c>Tokens.Claro/Oscuro.xaml</c> se intercambian) — sigue siendo una
/// sola fuente de verdad, solo cambia cuándo se resuelve el valor. Los <c>Setter</c> de color
/// siguen en <c>DynamicResource</c> sin cambios (esos sí dependen del tema).
///
/// Fuera de alcance a propósito (riesgo R-8, specs/03-architecture.md Incremento 2 §5): la
/// duración PERCIBIDA real y que hover "se sienta" distinto de press (AC-T23/AC-T24 tal cual
/// están redactados) no son verificables por un test estructural — quedan para QA manual. Lo
/// que sigue cubre exclusivamente lo verificable por inspección del árbol XAML: NFR-20/NFR-21
/// (recurso de duración compartido, cero valores hardcodeados), NFR-22 (guardia
/// <c>IsEnabled=True</c> en los <c>MultiTrigger</c> de hover/press) y NFR-23 (cero animación de
/// Brush/Color directo sobre Background/Fill/Stroke).
///
/// Nota sobre <c>ItemLibro</c> (<c>ListBoxItem</c>): a diferencia de los otros 6 estilos
/// (basados en <c>ButtonBase</c>/<c>ToggleButton</c>), <c>ListBoxItem</c> no expone una
/// propiedad <c>IsPressed</c> nativa — el tech-spec/arquitectura no cierran con qué mecanismo
/// concreto se resuelve el feedback de "presión" ahí (queda a criterio del developer, no es un
/// punto que este documento deba inventar). Por eso la guardia de <c>IsEnabled</c> para el caso
/// "press" se verifica de forma condicional (si existe un <c>MultiTrigger</c> con
/// <c>IsPressed=True</c>, debe tener la guardia) en vez de exigir su existencia literal en los
/// 7 estilos; la guardia de hover (<c>IsMouseOver</c>), en cambio, sí se exige en los 7, porque
/// esa propiedad es uniforme en todos los tipos de control usados aquí.
/// </summary>
public class EstilosXamlAnimacionesHoverPresionTests
{
    private static readonly string[] EstilosAlcanzados =
    {
        "Chip", "ChipAccion", "OpcionExamen", "BaldosaPregunta", "ItemNavegacion", "ItemLibro", "ZonaSoltar",
    };

    public static IEnumerable<object[]> NombresDeEstilos() =>
        EstilosAlcanzados.Select(nombre => new object[] { nombre });

    private static readonly Lazy<XDocument> Documento = new(() =>
        XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Theme/Estilos.xaml")));

    private static XElement ObtenerEstilo(string clave)
    {
        var estilo = Documento.Value.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                  e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave));

        Assert.True(estilo is not null,
            $"No se encontro <Style x:Key=\"{clave}\"> en AutoExam/Theme/Estilos.xaml.");
        return estilo!;
    }

    // ------------------------------------------------------------------
    // NFR-20 / NFR-21 — duracion compartida, cero valores hardcodeados
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(NombresDeEstilos))]
    public void CadaEstilo_AnimaConDuracionHoverCompartida_NFR20(string clave)
    {
        var duraciones = AtributosDuration(ObtenerEstilo(clave));

        Assert.Contains("{StaticResource DuracionHover}", duraciones);
    }

    [Theory]
    [MemberData(nameof(NombresDeEstilos))]
    public void CadaEstilo_AnimaConDuracionPresionCompartida_NFR21(string clave)
    {
        var duraciones = AtributosDuration(ObtenerEstilo(clave));

        Assert.Contains("{StaticResource DuracionPresion}", duraciones);
    }

    [Theory]
    [MemberData(nameof(NombresDeEstilos))]
    public void CadaEstilo_NoTieneNingunaDuracionHardcodeadaFueraDeLosDosRecursosCompartidos(string clave)
    {
        var duraciones = AtributosDuration(ObtenerEstilo(clave));

        Assert.All(duraciones, valor => Assert.True(
            valor is "{StaticResource DuracionHover}" or "{StaticResource DuracionPresion}",
            $"Estilo '{clave}': Duration '{valor}' no es uno de los dos recursos compartidos " +
            "(DuracionHover/DuracionPresion) — valor hardcodeado o recurso ajeno (NFR-20/NFR-21)."));
    }

    private static List<string> AtributosDuration(XElement estilo) =>
        estilo.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName == "Duration")
            .Select(a => a.Value)
            .ToList();

    // ------------------------------------------------------------------
    // NFR-22 — guardia IsEnabled=True en los MultiTrigger de hover/press
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(NombresDeEstilos))]
    public void CadaEstilo_TieneUnMultiTriggerDeHoverConGuardiaIsEnabled_NFR22(string clave)
    {
        var multiTriggers = MultiTriggersDe(ObtenerEstilo(clave));

        Assert.Contains(multiTriggers, mt =>
            TieneCondicion(mt, "IsMouseOver", "True") && TieneCondicion(mt, "IsEnabled", "True"));
    }

    [Theory]
    [MemberData(nameof(NombresDeEstilos))]
    public void NingunMultiTriggerDeHoverOPresionCareceDeLaGuardiaIsEnabled_NFR22(string clave)
    {
        var multiTriggers = MultiTriggersDe(ObtenerEstilo(clave));

        var deHoverOPresion = multiTriggers
            .Where(mt => TieneCondicion(mt, "IsMouseOver", "True") || TieneCondicion(mt, "IsPressed", "True"))
            .ToList();

        Assert.All(deHoverOPresion, mt => Assert.True(TieneCondicion(mt, "IsEnabled", "True"),
            $"Estilo '{clave}': un MultiTrigger de hover/press no tiene la guardia IsEnabled=True " +
            "(NFR-22) — dispararia la animacion con el control deshabilitado."));
    }

    private static List<XElement> MultiTriggersDe(XElement estilo) =>
        estilo.Descendants().Where(e => e.Name.LocalName == "MultiTrigger").ToList();

    private static bool TieneCondicion(XElement multiTrigger, string propiedad, string valor) =>
        multiTrigger.Descendants()
            .Any(e => e.Name.LocalName == "Condition" &&
                      e.Attributes().Any(a => a.Name.LocalName == "Property" && a.Value == propiedad) &&
                      e.Attributes().Any(a => a.Name.LocalName == "Value" && a.Value == valor));

    // ------------------------------------------------------------------
    // NFR-23 — cero animacion de Brush/Color directo sobre Background/Fill/Stroke
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(NombresDeEstilos))]
    public void CadaEstilo_NoUsaColorAnimationNiNingunTipoDeBrushAnimation_NFR23(string clave)
    {
        var prohibidos = ObtenerEstilo(clave).Descendants()
            .Where(e => EsAnimacionDeColorOBrush(e.Name.LocalName))
            .Select(e => e.Name.LocalName)
            .ToList();

        Assert.Empty(prohibidos);
    }

    private static bool EsAnimacionDeColorOBrush(string nombreLocal) =>
        (nombreLocal.Contains("Color") || nombreLocal.Contains("Brush")) && nombreLocal.Contains("Animation");

    [Theory]
    [MemberData(nameof(NombresDeEstilos))]
    public void CadaEstilo_NingunaAnimacionApuntaABackgroundFillOStroke_NFR23(string clave)
    {
        var animaciones = ObtenerEstilo(clave).Descendants()
            .Where(e => e.Name.LocalName.EndsWith("Animation") || e.Name.LocalName.EndsWith("KeyFrames"));

        foreach (var animacion in animaciones)
        {
            var targetProperty = animacion.Attributes()
                .FirstOrDefault(a => a.Name.LocalName.EndsWith("TargetProperty"))?.Value ?? string.Empty;

            Assert.False(ContieneAlgunaDeEstasPalabras(targetProperty, "Background", "Fill", "Stroke"),
                $"Estilo '{clave}': una animacion apunta a '{targetProperty}' — NFR-23 exige que solo " +
                "se anime Opacity o un ScaleTransform, nunca Background/Fill/Stroke directamente.");
        }
    }

    private static bool ContieneAlgunaDeEstasPalabras(string texto, params string[] palabras) =>
        palabras.Any(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase));
}
