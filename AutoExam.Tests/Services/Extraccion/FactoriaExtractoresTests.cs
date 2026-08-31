using System.Linq;
using AutoExam.Services;

namespace AutoExam.Tests.Services.Extraccion;

/// <summary>
/// Contrato de <c>FactoriaExtractores</c> — specs/03-architecture.md Incremento 4 §4.1,
/// specs/02-tech-spec.md "Contratos de integración" US-008/009/010 y AC-T40/AC-T43/AC-T48.
///
/// Trabajo contract-first (specs/team-roster.yaml, <c>test-dev-extraccion-multiformato</c>): estos
/// tests referencian directo los tipos que fija §4.1 (<c>FactoriaExtractores</c>,
/// <c>IExtractorContenido</c>, <c>PdfExtractor</c>, <c>OfficeExtractor</c>, <c>TipoFuente</c>)
/// aunque M1 todavía no los haya escrito — el build de AutoExam.Tests es el gate que confirma que
/// se implementó la forma exacta del contrato (mismos nombres, misma firma).
///
/// Supuesto documentado: la resolución por extensión es case-insensitive (los archivos llegan
/// como <c>.PDF</c>/<c>.Docx</c> con frecuencia y el filesystem de Windows no distingue). El
/// contrato lista las extensiones en minúscula pero no dice explícitamente qué pasa con otras
/// capitalizaciones; se asume normalización. Si M1 decide lo contrario, este test lo fuerza a
/// una conversación en review en vez de dejar el hueco.
/// </summary>
public class FactoriaExtractoresTests
{
    [Theory]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".heic")]
    [InlineData(".heif")]
    public void Para_ExtensionAdmitida_DevuelveUnExtractorQueLaSoporta(string extension)
    {
        var extractor = FactoriaExtractores.Para(extension);

        Assert.NotNull(extractor);
        Assert.True(extractor!.Soporta(extension),
            $"El extractor devuelto para '{extension}' debería reportar Soporta('{extension}') == true.");
    }

    [Theory]
    [InlineData(".PDF")]
    [InlineData(".Docx")]
    [InlineData(".XLSX")]
    [InlineData(".HEIC")]
    public void Para_ExtensionAdmitidaEnMayusculas_TambienResuelve(string extension)
    {
        Assert.NotNull(FactoriaExtractores.Para(extension));
    }

    [Fact]
    public void Para_Pdf_DevuelveElAdapterDePdf()
    {
        Assert.IsType<PdfExtractor>(FactoriaExtractores.Para(".pdf"));
    }

    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    public void Para_Office_DevuelveElOfficeExtractor(string extension)
    {
        Assert.IsType<OfficeExtractor>(FactoriaExtractores.Para(extension));
    }

    [Theory]
    [InlineData(".doc")]   // Word 97-2003 (OLE2) — RN-8, fuera de v1
    [InlineData(".xls")]   // Excel 97-2003
    [InlineData(".ppt")]   // PowerPoint 97-2003
    [InlineData(".odt")]   // OpenDocument — fuera de alcance (01-spec)
    [InlineData(".rtf")]
    [InlineData(".txt")]
    [InlineData(".webp")]  // imagen fuera de RN-9
    [InlineData(".tiff")]
    [InlineData(".zip")]
    [InlineData("")]
    [InlineData(".")]
    public void Para_ExtensionNoAdmitida_DevuelveNull(string extension)
    {
        // §4.1: "null => FormatoNoSoportadoException" — la factory devuelve null y el llamador
        // (M3/M4) es quien lanza la excepción con el mensaje al usuario.
        Assert.Null(FactoriaExtractores.Para(extension));
    }

    [Fact]
    public void ExtensionesAdmitidas_ContieneLasNueveDeV1YNingunaLegacy()
    {
        var admitidas = FactoriaExtractores.ExtensionesAdmitidas
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        foreach (var esperada in new[] { ".pdf", ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png", ".heic", ".heif" })
        {
            Assert.Contains(esperada, admitidas);
        }

        foreach (var prohibida in new[] { ".doc", ".xls", ".ppt" })
        {
            Assert.DoesNotContain(prohibida, admitidas);
        }
    }

    [Fact]
    public void ExtensionesAdmitidas_TodasResuelvenPorLaFactory()
    {
        // Coherencia interna: lo que se ofrece en el filtro del diálogo (§4.3) es exactamente lo
        // que la factory sabe atender.
        Assert.All(FactoriaExtractores.ExtensionesAdmitidas,
            ext => Assert.NotNull(FactoriaExtractores.Para(ext)));
    }
}
