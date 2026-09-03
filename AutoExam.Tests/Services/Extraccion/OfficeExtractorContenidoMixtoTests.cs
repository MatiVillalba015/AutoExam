using System.IO;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services.Extraccion;

/// <summary>
/// US-022 (archivo con contenido mezclado) y la parte de US-018 que aporta el extractor
/// (figuras del propio material).
///
/// El caso: un solo <c>.docx</c> con párrafos escritos, capturas de pantalla y fotos de papel,
/// todo junto. Antes de US-022 el extractor decidía por archivo entero —o texto, o imágenes— y
/// un documento así perdía la mitad del material.
///
/// La regla que fijan estos tests es la del ROL de cada imagen, que es donde está la decisión
/// de diseño:
///  - documento SIN texto  → las imágenes son el material  → PaginasEscaneadas (US-014)
///  - documento CON texto  → las imágenes son ilustración  → Imagenes/figuras (US-018)
///
/// Y sobre todo: nunca las dos cosas a la vez. Duplicar una imagen en los dos canales gastaría
/// el doble de cuota por el mismo contenido, y el prompt les da instrucciones opuestas (a una
/// figura hay que referenciarla desde una pregunta; a una página escaneada, no).
/// </summary>
public class OfficeExtractorContenidoMixtoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(), "AutoExam.Tests.Mixto", Guid.NewGuid().ToString("N"));

    public OfficeExtractorContenidoMixtoTests() => Directory.CreateDirectory(_carpeta);

    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static readonly string[] Parrafos =
    {
        new('a', 300),
        new('b', 300),
    };

    private OpcionesExtraccion Opciones(string sub = "img") => new()
    {
        CarpetaImagenes = Path.Combine(_carpeta, sub),
        ExtraerImagenes = true,
        MaxImagenes = 12,
        MaxPaginasEscaneadas = 10,
    };

    private async Task<ExtraccionResultado> ExtraerAsync(string ruta, OpcionesExtraccion? op = null)
        => await new OfficeExtractor().ExtraerAsync(
            new[] { ruta }, new RecorteFuente(), op ?? Opciones(), null, default);

    // ------------------------------------------------------------------
    // AC — el texto nativo y las imágenes se aprovechan juntos
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocxMixto_ConservaElTextoYAdemasAprovechaLasImagenes()
    {
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 3);

        var r = await ExtraerAsync(docx);

        Assert.True(r.TieneTexto, "El texto nativo del documento no puede perderse.");
        Assert.NotEmpty(r.Imagenes);

        Assert.True(r.TieneMaterial);
    }

    [Fact]
    public async Task DocxMixto_LasImagenesVanComoFigurasYNoComoPaginasEscaneadas()
    {
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 2);

        var r = await ExtraerAsync(docx);

        Assert.Equal(2, r.Imagenes.Count);
        Assert.Empty(r.PaginasEscaneadas);
    }

    [Fact]
    public async Task NingunaImagen_ViajaPorLosDosCanalesALaVez()
    {
        // La guarda que evita pagar dos veces la misma imagen.
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 3);

        var r = await ExtraerAsync(docx);

        var enAmbos = r.Imagenes.Select(i => i.Identificador)
            .Intersect(r.PaginasEscaneadas.Select(p => p.Identificador), StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(enAmbos.Count == 0,
            "Estas imágenes viajan como figura Y como página escaneada: " + string.Join(", ", enAmbos));
    }

    [Fact]
    public async Task DocxSinTexto_LasImagenesSiguenSiendoElMaterial_US014()
    {
        // La otra mitad de la regla: sin texto, las imágenes no son ilustración, son el material.
        string docx = FuentesDePrueba.CrearDocxSoloImagenes(_carpeta, 1, 2);

        var r = await ExtraerAsync(docx, Opciones("solo"));

        Assert.NotEmpty(r.PaginasEscaneadas);
        Assert.Empty(r.Imagenes);
    }

    // ------------------------------------------------------------------
    // US-018 — las figuras quedan utilizables como imagen de referencia
    // ------------------------------------------------------------------

    [Fact]
    public async Task LasFiguras_QuedanConIdentificadorYArchivoParaPoderReferenciarlas()
    {
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 1);

        var figura = Assert.Single((await ExtraerAsync(docx)).Imagenes);

        // El identificador es lo que el modelo escribe en "ImagenReferencia"; sin él la figura
        // no se le puede mostrar al alumno aunque esté extraída.
        Assert.False(string.IsNullOrWhiteSpace(figura.Identificador));
        Assert.True(File.Exists(figura.Ruta), $"La figura no se escribió en disco: '{figura.Ruta}'.");
        Assert.True(figura.YaPreparada);
    }

    [Fact]
    public async Task LosIconosYLogos_NoSeUsanComoFigura()
    {
        // Una imagen de 32 px es una viñeta o un logo de encabezado, no un esquema: mostrarla
        // como referencia de una pregunta no aporta nada.
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 4, lado: 32);

        var r = await ExtraerAsync(docx);

        Assert.Empty(r.Imagenes);
        Assert.True(r.TieneTexto, "Descartar los iconos no puede llevarse puesto el texto.");
    }

    [Fact]
    public async Task SiLaExtraccionDeImagenesEstaApagada_NoSeRecogenFiguras()
    {
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 3);

        var op = Opciones("apagada");
        op.ExtraerImagenes = false;

        var r = await ExtraerAsync(docx, op);

        Assert.Empty(r.Imagenes);
        Assert.True(r.TieneTexto);
    }

    [Fact]
    public async Task LasFiguras_RespetanElTopeConfigurado()
    {
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 6);

        var op = Opciones("tope");
        op.MaxImagenes = 2;

        var r = await ExtraerAsync(docx, op);

        Assert.Equal(2, r.Imagenes.Count);
    }

    // ------------------------------------------------------------------
    // AC — se avisa que puede tardar más y consumir más cuota
    // ------------------------------------------------------------------

    [Fact]
    public async Task DocxMixto_AvisaQuePuedeTardarMasYGastarMasCuota()
    {
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 2);

        var avisos = new List<string>();
        var progreso = new Progress<string>(avisos.Add);

        await new OfficeExtractor().ExtraerAsync(
            new[] { docx }, new RecorteFuente(), Opciones("aviso"), progreso, default);

        for (int i = 0; i < 50 && avisos.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.Contains(avisos, a => a.Contains("cuota", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------
    // AC — una sección ilegible no se lleva puesto el resto del archivo
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnaImagenIlegible_NoTumbaLaExtraccionDelResto()
    {
        string docx = FuentesDePrueba.CrearDocxMixto(_carpeta, Parrafos, imagenes: 2);

        // Se le agrega una parte que no es una imagen válida, como una sección corrupta.
        using (var zip = System.IO.Compression.ZipFile.Open(docx, System.IO.Compression.ZipArchiveMode.Update))
        {
            var entrada = zip.CreateEntry("word/media/image9.png");
            using var s = entrada.Open();
            byte[] basura = System.Text.Encoding.ASCII.GetBytes("no soy un png");
            s.Write(basura, 0, basura.Length);
        }

        var r = await ExtraerAsync(docx, Opciones("ilegible"));

        Assert.True(r.TieneTexto, "El texto tiene que sobrevivir a una sección ilegible.");
        Assert.Equal(2, r.Imagenes.Count);
    }
}
