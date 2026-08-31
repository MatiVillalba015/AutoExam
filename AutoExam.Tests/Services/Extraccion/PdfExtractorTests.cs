using System.Threading;
using System.Threading.Tasks;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services.Extraccion;

/// <summary>
/// <c>PdfExtractor</c> (AutoExam/Services/PdfExtractor.cs) — adapter de
/// <see cref="PdfExtractorService"/> hacia <see cref="IExtractorContenido"/> (US-008,
/// arquitectura Inc-4 §4.1 / §4.5, NFR-A5). Cierra la brecha de
/// <c>test-dev-extraccion-multiformato</c>: la factory ya devolvía un <c>PdfExtractor</c>
/// (<see cref="FactoriaExtractoresTests"/>) pero la traducción del recorte y el manejo de
/// errores del adapter no tenían test.
///
/// Cubre:
/// - la traducción de <c>RecorteFuente.Paginas</c> (una <see cref="RangoPaginas"/>) al camino de
///   <c>PdfExtractorService.ExtraerAsync</c> — sólo llegan las páginas del rango.
/// - recorte vacío / null ⇒ documento completo (<c>RecorteFuente.MaterialCompleto</c>).
/// - PDF dañado / ilegible ⇒ <see cref="FuenteIlegibleException"/> con la causa (NFR-37), tanto
///   en <c>ExtraerAsync</c> como en <c>MedirAsync</c>.
/// - AC-T41 / NFR-40 — medida por formato: cantidad de páginas.
///
/// Se apoya en el <see cref="PdfExtractorService"/> real (no un doble): el adapter es una capa
/// fina y lo que importa es justamente el borde adapter↔servicio. Fixtures PDF generadas con
/// PdfPig (<see cref="FuentesDePrueba"/>).
/// </summary>
public class PdfExtractorTests
{
    private const string Marcador = "PAGINAMARCA";
    private static readonly OpcionesExtraccion Opciones = new();

    private static PdfExtractor Nuevo() => new();

    private static string TextoUnido(ExtraccionResultado r) =>
        new(string.Concat(r.Fragmentos.Select(f => f.Texto)).Where(c => !char.IsWhiteSpace(c)).ToArray());

    // ------------------------------------------------------------------
    // Soporta / medida
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(".pdf", true)]
    [InlineData(".PDF", true)]
    [InlineData(".docx", false)]
    [InlineData(".png", false)]
    public void Soporta_SoloPdf(string extension, bool esperado)
        => Assert.Equal(esperado, Nuevo().Soporta(extension));

    [Theory]
    [InlineData(1, "1 pagina")]
    [InlineData(5, "5 paginas")]
    public async Task MedirAsync_InformaCantidadDePaginas_AC_T41(int paginas, string esperado)
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdf(carpeta.Ruta, paginas, Marcador);

        var medida = await Nuevo().MedirAsync(new[] { ruta }, CancellationToken.None);

        Assert.Equal(TipoFuente.Pdf, medida.Tipo);
        Assert.Equal(esperado, medida.Texto);
    }

    // ------------------------------------------------------------------
    // Traducción RecorteFuente.Paginas -> RangoPaginas
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_ConRecorteDePaginas_SoloExtraeEseRango()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdf(carpeta.Ruta, paginas: 5, prefijoMarcador: Marcador);

        var recorte = new RecorteFuente { Paginas = new[] { new RangoPaginas(2, 3, "Seleccion") } };
        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, recorte, Opciones, null, CancellationToken.None);

        string texto = TextoUnido(resultado);
        Assert.Contains($"{Marcador}2", texto);
        Assert.Contains($"{Marcador}3", texto);
        Assert.DoesNotContain($"{Marcador}1", texto);
        Assert.DoesNotContain($"{Marcador}4", texto);
        Assert.DoesNotContain($"{Marcador}5", texto);
    }

    [Fact]
    public async Task ExtraerAsync_ConVariosRangos_ExtraeLaUnionDeEllos()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdf(carpeta.Ruta, paginas: 6, prefijoMarcador: Marcador);

        var recorte = new RecorteFuente
        {
            Paginas = new[] { new RangoPaginas(1, 1, "a"), new RangoPaginas(5, 6, "b") },
        };
        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, recorte, Opciones, null, CancellationToken.None);

        string texto = TextoUnido(resultado);
        Assert.Contains($"{Marcador}1", texto);
        Assert.Contains($"{Marcador}5", texto);
        Assert.Contains($"{Marcador}6", texto);
        Assert.DoesNotContain($"{Marcador}3", texto);
    }

    // ------------------------------------------------------------------
    // Recorte vacío / null -> documento completo
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_RecorteVacio_ExtraeElDocumentoCompleto()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdf(carpeta.Ruta, paginas: 4, prefijoMarcador: Marcador);

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None);

        string texto = TextoUnido(resultado);
        for (int n = 1; n <= 4; n++)
        {
            Assert.Contains($"{Marcador}{n}", texto);
        }
    }

    [Fact]
    public async Task ExtraerAsync_RecorteConListaDePaginasVacia_ExtraeElDocumentoCompleto()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdf(carpeta.Ruta, paginas: 3, prefijoMarcador: Marcador);

        var recorte = new RecorteFuente { Paginas = Array.Empty<RangoPaginas>() };
        Assert.True(recorte.MaterialCompleto);

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, recorte, Opciones, null, CancellationToken.None);

        string texto = TextoUnido(resultado);
        Assert.Contains($"{Marcador}1", texto);
        Assert.Contains($"{Marcador}3", texto);
    }

    [Fact]
    public async Task ExtraerAsync_RecorteNull_NoRevienta_ExtraeElDocumentoCompleto()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdf(carpeta.Ruta, paginas: 2, prefijoMarcador: Marcador);

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, null!, Opciones, null, CancellationToken.None);

        string texto = TextoUnido(resultado);
        Assert.Contains($"{Marcador}1", texto);
        Assert.Contains($"{Marcador}2", texto);
    }

    // ------------------------------------------------------------------
    // PDF dañado -> FuenteIlegibleException
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_PdfDaniado_LanzaFuenteIlegibleConCausa_NFR37()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdfCorrupto(carpeta.Ruta);

        var ex = await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None));

        Assert.Contains("PDF", ex.Message);
    }

    [Fact]
    public async Task ExtraerAsync_PdfDaniadoConRecorteExplicito_TambienLanzaFuenteIlegible_NFR37()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdfCorrupto(carpeta.Ruta);

        await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().ExtraerAsync(
                new[] { ruta },
                new RecorteFuente { Paginas = new[] { new RangoPaginas(1, 1, "x") } },
                Opciones, null, CancellationToken.None));
    }

    [Fact]
    public async Task MedirAsync_PdfDaniado_LanzaFuenteIlegible_NFR37()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPdfCorrupto(carpeta.Ruta);

        await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().MedirAsync(new[] { ruta }, CancellationToken.None));
    }

    [Fact]
    public async Task ExtraerAsync_SinRuta_LanzaFuenteIlegible()
    {
        await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().ExtraerAsync(Array.Empty<string>(), new RecorteFuente(), Opciones, null, CancellationToken.None));
    }
}
