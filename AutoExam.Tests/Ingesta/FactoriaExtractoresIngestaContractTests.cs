using System.Collections;
using System.Reflection;

namespace AutoExam.Tests.Ingesta;

/// <summary>
/// Superficie de <c>FactoriaExtractores</c> / <c>IExtractorContenido</c> / <c>TipoFuente</c> que
/// consume el Módulo M4 (ingesta) — specs/03-architecture.md (Incremento 4) §4.1.
///
/// Alcance deliberado (evita duplicar con <c>test-dev-extraccion-multiformato</c>, DoD del rol):
/// acá se cubre SÓLO lo que M4 usa para armar el selector y decidir el rechazo —
/// <c>ExtensionesAdmitidas</c> (filtro del OpenFileDialog / lista de la ZonaSoltar, §4.3/§4.4) y
/// <c>Para(ext)</c> devolviendo <c>null</c> para lo no soportado (el VM lo traduce a aviso,
/// NFR-37). El comportamiento de <c>MedirAsync</c>/<c>ExtraerAsync</c> por formato es de M1.
/// </summary>
public class FactoriaExtractoresIngestaContractTests
{
    private static Type Factoria => ContratoM4.Tipo("AutoExam.Services.FactoriaExtractores");
    private static Type Interfaz => ContratoM4.Tipo("AutoExam.Services.IExtractorContenido");
    private static Type TipoFuente => ContratoM4.Tipo("AutoExam.Models.TipoFuente");

    // Las 9 extensiones que 01-spec.md RN-8 (Office) + RN-9 (imagen) admiten en v1.
    private static readonly string[] Admitidas =
        { ".pdf", ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png", ".heic", ".heif" };

    // Formatos legacy (fuera de alcance, RN-8) + basura: la factory NO debe resolverlos.
    private static readonly string[] NoAdmitidas =
        { ".doc", ".xls", ".ppt", ".odt", ".txt", ".rtf", ".webp", ".tiff", ".bmp", ".gif", ".zip", "" };

    private static IEnumerable<string> ExtensionesAdmitidas()
    {
        var miembro = Factoria.GetMember(
            "ExtensionesAdmitidas",
            MemberTypes.Property | MemberTypes.Field,
            BindingFlags.Public | BindingFlags.Static).FirstOrDefault()
            ?? throw new InvalidOperationException("FactoriaExtractores.ExtensionesAdmitidas no existe (§4.1).");

        object? valor = miembro switch
        {
            PropertyInfo p => p.GetValue(null),
            FieldInfo f => f.GetValue(null),
            _ => null,
        };

        Assert.True(valor is IEnumerable, "ExtensionesAdmitidas debería ser una colección enumerable de string.");
        return ((IEnumerable)valor!).Cast<object>().Select(x => x?.ToString() ?? string.Empty);
    }

    private static MethodInfo Para => ContratoM4.Metodo(
        Factoria, "Para", BindingFlags.Public | BindingFlags.Static);

    [Fact] // §4.1
    public void TipoFuente_TieneLosCincoValoresDelContrato()
    {
        Assert.True(TipoFuente.IsEnum);
        Assert.Equal(
            new[] { "Pdf", "Word", "Excel", "PowerPoint", "SetImagenes" }.OrderBy(x => x),
            Enum.GetNames(TipoFuente).OrderBy(x => x));
    }

    [Fact] // AC-T40 / AC-T48 — el filtro del diálogo y la ZonaSoltar salen de acá
    public void ExtensionesAdmitidas_SonExactamenteLasNueveDeV1()
    {
        var actuales = ExtensionesAdmitidas().Select(e => e.ToLowerInvariant()).ToHashSet();

        Assert.Equal(Admitidas.OrderBy(x => x), actuales.OrderBy(x => x));
    }

    [Fact] // AC-T40 / AC-T48
    public void Para_ResuelveUnExtractorParaCadaExtensionAdmitida()
    {
        foreach (var ext in Admitidas)
        {
            var extractor = Para.Invoke(null, new object?[] { ext });
            Assert.True(extractor is not null, $"FactoriaExtractores.Para(\"{ext}\") devolvió null y debería resolver un extractor.");
            Assert.IsAssignableFrom(Interfaz, extractor);
        }
    }

    [Fact] // NFR-37 — .doc/.xls/.ppt y cualquier otra → el VM las rechaza sin crear fuente
    public void Para_DevuelveNull_ParaFormatosNoSoportados()
    {
        foreach (var ext in NoAdmitidas)
        {
            var extractor = Para.Invoke(null, new object?[] { ext });
            Assert.True(extractor is null, $"FactoriaExtractores.Para(\"{ext}\") debería devolver null (formato fuera de v1, RN-8).");
        }
    }

    [Theory] // el OpenFileDialog y el drop entregan la extensión tal cual la tipeó el SO
    [InlineData(".PDF")]
    [InlineData(".Docx")]
    [InlineData(".JPG")]
    [InlineData(".HEIC")]
    public void Para_EsInsensibleAMayusculas(string ext)
        => Assert.NotNull(Para.Invoke(null, new object?[] { ext }));

    [Fact] // §4.1 — forma de IExtractorContenido
    public void IExtractorContenido_ExponeSoportaMedirYExtraer()
    {
        Assert.NotNull(Interfaz.GetMethod("Soporta"));
        Assert.NotNull(Interfaz.GetMethod("MedirAsync"));
        Assert.NotNull(Interfaz.GetMethod("ExtraerAsync"));
    }
}
