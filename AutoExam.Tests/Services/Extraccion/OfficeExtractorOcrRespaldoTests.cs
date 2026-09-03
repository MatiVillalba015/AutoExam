using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services.Extraccion;

/// <summary>
/// US-014 — respaldo por interpretación de imágenes cuando un documento no tiene texto.
///
/// El caso real: alguien saca fotos a las páginas de un libro con el celular, las pega en un
/// Word y lo sube como material. El extractor de texto no encuentra un solo carácter y hasta
/// ahora la app respondía "no se encontró contenido" sin intentar nada más.
///
/// El respaldo no hace OCR local: recupera las imágenes incrustadas y las deja en
/// <see cref="ExtraccionResultado.PaginasEscaneadas"/>, que es el mismo canal por el que ya
/// viajan las fotos de apuntes de US-010 (<c>inline_data</c> → la IA les lee el texto). Por eso
/// estos tests miran ese contenedor y no un texto reconocido: el reconocimiento pasa del otro
/// lado de la red.
/// </summary>
public class OfficeExtractorOcrRespaldoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(), "AutoExam.Tests.Ocr", Guid.NewGuid().ToString("N"));

    public OfficeExtractorOcrRespaldoTests() => Directory.CreateDirectory(_carpeta);

    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static OpcionesExtraccion Opciones(string carpetaImagenes) => new()
    {
        CarpetaImagenes = carpetaImagenes,
        MaxPaginasEscaneadas = 10,
    };

    private async Task<ExtraccionResultado> ExtraerAsync(string ruta)
        => await new OfficeExtractor().ExtraerAsync(
            new[] { ruta }, new RecorteFuente(), Opciones(Path.Combine(_carpeta, "img")), null, default);

    // ------------------------------------------------------------------
    // AC — un documento cuyas páginas son sólo imágenes recupera contenido
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocxSinTextoPeroConImagenes_RecuperaLasImagenesComoMaterial()
    {
        string docx = FuentesDePrueba.CrearDocxSoloImagenes(_carpeta, 1, 2, 3);

        var r = await ExtraerAsync(docx);

        Assert.Equal(3, r.PaginasEscaneadas.Count);

        // Lo que de verdad importa: el resto del flujo pregunta por TieneMaterial para decidir
        // si sigue o si corta con "no se encontró contenido".
        Assert.True(r.TieneMaterial,
            "Un documento con fotos pegadas tiene material para generar preguntas: la app no " +
            "debería cortar con 'no se encontró contenido'.");
    }

    [Fact]
    public async Task PptxSinTextoPeroConImagenes_RecuperaLasImagenesComoMaterial()
    {
        string pptx = FuentesDePrueba.CrearPptxSoloImagenes(_carpeta, imagenes: 2);

        var r = await ExtraerAsync(pptx);

        Assert.Equal(2, r.PaginasEscaneadas.Count);
        Assert.True(r.TieneMaterial);
    }

    [Fact]
    public async Task LasImagenesRecuperadas_QuedanListasParaViajarALaIA()
    {
        string docx = FuentesDePrueba.CrearDocxSoloImagenes(_carpeta, 1);

        var imagen = Assert.Single((await ExtraerAsync(docx)).PaginasEscaneadas);

        // YaPreparada le dice al servicio de IA que no vuelva a reescalar: si viniera en false,
        // la imagen se procesaría dos veces.
        Assert.True(imagen.YaPreparada);
        Assert.False(string.IsNullOrWhiteSpace(imagen.MimeType));
        Assert.True(File.Exists(imagen.Ruta), $"La imagen recuperada no se escribió en disco: '{imagen.Ruta}'.");
    }

    // ------------------------------------------------------------------
    // AC — el orden del material se respeta
    // ------------------------------------------------------------------

    [Fact]
    public async Task LasImagenes_SeOrdenanPorNumeroNaturalNoAlfabeticamente()
    {
        // image10 va después de image2, no antes: ordenar por texto rompería el orden del
        // material, que es el orden en que la IA lo lee.
        string docx = FuentesDePrueba.CrearDocxSoloImagenes(_carpeta, 10, 2, 1);

        var r = await ExtraerAsync(docx);

        Assert.Equal(new[] { 1, 2, 3 }, r.PaginasEscaneadas.Select(p => p.Pagina).ToArray());
    }

    // ------------------------------------------------------------------
    // RN-10 — no se gasta cuota si el documento ya tenía texto
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocxConTexto_NoDisparaElRespaldoDeImagenes()
    {
        // Un documento con texto no manda nada por el canal de páginas escaneadas: el material
        // ya lo tenemos como texto y leerlo de nuevo desde una imagen gastaría cuota de más.
        //
        // Ojo con el alcance de esta afirmación, que US-022 acotó: un documento con texto Y con
        // imágenes SÍ aprovecha esas imágenes, pero por el otro canal, como figuras. Lo que
        // sigue valiendo —y es lo que este test fija— es que no se convierten en material de
        // lectura. El reparto entre los dos canales lo cubre OfficeExtractorContenidoMixtoTests.
        string parrafo = new('a', 400);
        string docx = FuentesDePrueba.CrearDocx(_carpeta, parrafo);

        var r = await ExtraerAsync(docx);

        Assert.True(r.TieneTexto);
        Assert.Empty(r.PaginasEscaneadas);
    }

    // ------------------------------------------------------------------
    // AC — si no se recupera nada, el mensaje es el de siempre
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocxConImagenesIlegibles_NoInventaMaterial()
    {
        string docx = FuentesDePrueba.CrearDocxSoloImagenesIlegibles(_carpeta);

        var r = await ExtraerAsync(docx);

        Assert.Empty(r.PaginasEscaneadas);
        Assert.False(r.TieneMaterial,
            "Si ninguna imagen se pudo decodificar, no hay material: el llamador tiene que " +
            "mostrar el mismo aviso de 'no se encontró contenido' de siempre.");
    }

    [Fact]
    public async Task DocxSinTextoNiImagenes_SigueSinMaterial()
    {
        string docx = FuentesDePrueba.CrearDocxSinTexto(_carpeta);

        var r = await ExtraerAsync(docx);

        Assert.Empty(r.PaginasEscaneadas);
        Assert.False(r.TieneMaterial);
    }

    // ------------------------------------------------------------------
    // RN-3 — avisar que este camino tarda más y gasta más cuota
    // ------------------------------------------------------------------

    [Fact]
    public async Task AlRecuperarImagenes_SeAvisaQueTardaMasYGastaMasCuota()
    {
        string docx = FuentesDePrueba.CrearDocxSoloImagenes(_carpeta, 1, 2);

        var avisos = new List<string>();
        var progreso = new Progress<string>(avisos.Add);

        await new OfficeExtractor().ExtraerAsync(
            new[] { docx }, new RecorteFuente(), Opciones(Path.Combine(_carpeta, "aviso")), progreso, default);

        // Progress<T> despacha por el SynchronizationContext; sin uno, va al thread pool.
        // Se espera a que llegue en vez de asumir que ya está.
        for (int i = 0; i < 50 && avisos.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.Contains(avisos, a => a.Contains("cuota", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------
    // NFR-43 — el tope por material se respeta
    // ------------------------------------------------------------------

    [Fact]
    public async Task SiHayMasImagenesQueElTope_SeTomanLasPrimeras()
    {
        string docx = FuentesDePrueba.CrearDocxSoloImagenes(_carpeta, 1, 2, 3, 4, 5);

        var opciones = Opciones(Path.Combine(_carpeta, "tope"));
        opciones.MaxPaginasEscaneadas = 2;

        var r = await new OfficeExtractor()
            .ExtraerAsync(new[] { docx }, new RecorteFuente(), opciones, null, default);

        Assert.Equal(2, r.PaginasEscaneadas.Count);
    }
}
