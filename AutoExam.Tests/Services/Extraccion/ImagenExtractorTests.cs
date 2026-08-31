using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;

namespace AutoExam.Tests.Services.Extraccion;

/// <summary>
/// <c>ImagenExtractor</c> (AutoExam/Services/ImagenExtractor.cs) — US-010, arquitectura Inc-4
/// §4.1 / §3, AC-T48 / AC-T49 / AC-T50 / AC-T51, NFR-41 / NFR-42 / NFR-43 / NFR-44. Cierra la
/// brecha marcada por test-developer: la familia de fuentes-imagen no tenía ningún test.
///
/// Cubre:
/// - <c>Soporta</c> / <c>MedirAsync</c> — jpg/jpeg/png/heic/heif; medida = cantidad de imágenes.
/// - AC-T49 / NFR-42 — HEIC/HEIF se convierte a un formato que la IA entiende antes del envío;
///   la imagen que queda en <c>PaginasEscaneadas</c> es JPEG, no HEIC.
/// - AC-T50 / NFR-41 — N fotos con alguna ilegible ⇒ genera con el resto y avisa; todas
///   ilegibles ⇒ <see cref="FuenteIlegibleException"/> (0 exámenes vacíos).
/// - AC-T51 / NFR-43 — orden de <c>PaginasEscaneadas</c> = orden de entrada (una falla en el
///   medio no renumera a las siguientes); recorte a <c>MaxPaginasEscaneadas</c>
///   (= <c>AppConfig.MaxImagenesPorMaterial</c>, lo fija el llamador) con aviso del límite concreto.
/// - NFR-44 — aviso de mayor consumo de cuota presente.
///
/// Fixtures de imagen generadas en tiempo de test con WPF Imaging (<see cref="FuentesDePrueba"/>);
/// el HEIC/HEIF es el binario real versionado en Recursos/Imagen.
/// </summary>
public class ImagenExtractorTests
{
    private static ImagenExtractor Nuevo() => new();

    private static OpcionesExtraccion Opciones(string carpeta, int topeImagenes = 12) => new()
    {
        CarpetaImagenes = carpeta,
        MaxPaginasEscaneadas = topeImagenes,
    };

    private static readonly byte[] FirmaJpeg = { 0xFF, 0xD8, 0xFF };

    private static void AssertEsJpeg(byte[] bytes)
        => Assert.Equal(FirmaJpeg, bytes.Take(FirmaJpeg.Length));

    // ------------------------------------------------------------------
    // Soporta / MedirAsync
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".png", true)]
    [InlineData(".heic", true)]
    [InlineData(".heif", true)]
    [InlineData(".JPG", true)]
    [InlineData(".pdf", false)]
    [InlineData(".docx", false)]
    [InlineData(".gif", false)]
    [InlineData(".bmp", false)]
    public void Soporta_SoloFamiliaDeImagenes(string extension, bool esperado)
        => Assert.Equal(esperado, Nuevo().Soporta(extension));

    [Theory]
    [InlineData(1, "1 imagen")]
    [InlineData(3, "3 imagenes")]
    public async Task MedirAsync_CuentaImagenes_SinAbrirArchivos_AC_T48_NFR40(int cantidad, string esperado)
    {
        var rutas = Enumerable.Range(1, cantidad).Select(i => $"C:/no/existe/{i}.png").ToArray();

        var medida = await Nuevo().MedirAsync(rutas, CancellationToken.None);

        Assert.Equal(TipoFuente.SetImagenes, medida.Tipo);
        Assert.Equal(esperado, medida.Texto);
    }

    // ------------------------------------------------------------------
    // AC-T48 / AC-T50 — fotos legibles
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_FotosLegibles_LasDejaComoPaginasEscaneadasYaPreparadas()
    {
        using var carpeta = new CarpetaDescartable();
        string entrada = carpeta.Sub("origen");
        string salida = carpeta.Sub("salida");
        string a = FuentesDePrueba.CrearPng(entrada, "foto-a.png");
        string b = FuentesDePrueba.CrearJpeg(entrada, "foto-b.jpg");
        var progreso = new ProgresoSincrono();

        var r = await Nuevo().ExtraerAsync(new[] { a, b }, new RecorteFuente(), Opciones(salida), progreso, CancellationToken.None);

        Assert.Equal(2, r.PaginasEscaneadas.Count);
        Assert.Equal(2, r.PaginasLeidas);
        Assert.Equal(2, r.PaginasSeleccionadas);
        Assert.All(r.PaginasEscaneadas, p =>
        {
            Assert.True(p.YaPreparada);
            Assert.Equal("image/jpeg", p.MimeType);
            AssertEsJpeg(File.ReadAllBytes(p.Ruta));
        });
        Assert.Equal(new[] { 1, 2 }, r.PaginasEscaneadas.Select(p => p.Pagina));
        Assert.True(r.TienePaginasEscaneadas);
        Assert.True(r.TieneMaterial);
    }

    [Fact]
    public async Task ExtraerAsync_AvisaMayorConsumoDeCuota_NFR44()
    {
        using var carpeta = new CarpetaDescartable();
        string entrada = carpeta.Sub("origen");
        string a = FuentesDePrueba.CrearPng(entrada, "foto.png");
        var progreso = new ProgresoSincrono();

        await Nuevo().ExtraerAsync(new[] { a }, new RecorteFuente(), Opciones(carpeta.Sub("salida")), progreso, CancellationToken.None);

        Assert.True(progreso.Contiene("cuota"));
    }

    // ------------------------------------------------------------------
    // AC-T49 / NFR-42 — HEIC/HEIF → formato soportado antes del envío
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("apunte.heic")]
    [InlineData("apunte.heif")]
    public async Task ExtraerAsync_ConHeic_LoConvierteAJpeg_CeroBytesHeicEnLaSalida_AC_T49_NFR42(string nombre)
    {
        using var carpeta = new CarpetaDescartable();
        string entrada = carpeta.Sub("origen");
        string salida = carpeta.Sub("salida");
        string heic = FuentesDePrueba.CopiarHeicReal(entrada, nombre);
        string png = FuentesDePrueba.CrearPng(entrada, "otra.png");
        var progreso = new ProgresoSincrono();

        var r = await Nuevo().ExtraerAsync(new[] { heic, png }, new RecorteFuente(), Opciones(salida), progreso, CancellationToken.None);

        Assert.Equal(2, r.PaginasEscaneadas.Count);
        Assert.True(progreso.Contiene("Convirtiendo"));

        foreach (var pagina in r.PaginasEscaneadas)
        {
            byte[] bytes = File.ReadAllBytes(pagina.Ruta);
            AssertEsJpeg(bytes);
            string comoTexto = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("ftyp", comoTexto);   // marcador de contenedor HEIC
        }
    }

    // ------------------------------------------------------------------
    // AC-T50 / NFR-41 — resultado parcial / sin material
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_AlgunasFotosIlegibles_GeneraConElResto_YAvisaLaLimitacion_AC_T50()
    {
        using var carpeta = new CarpetaDescartable();
        string entrada = carpeta.Sub("origen");
        string salida = carpeta.Sub("salida");
        // Orden de entrada: buena, ilegible, buena. La ilegible es la #2.
        string b1 = FuentesDePrueba.CrearPng(entrada, "01-buena.png");
        string mala = FuentesDePrueba.CrearImagenIlegible(entrada, "02-rota.jpg");
        string b3 = FuentesDePrueba.CrearJpeg(entrada, "03-buena.jpg");
        var progreso = new ProgresoSincrono();

        var r = await Nuevo().ExtraerAsync(new[] { b1, mala, b3 }, new RecorteFuente(), Opciones(salida), progreso, CancellationToken.None);

        Assert.Equal(3, r.PaginasSeleccionadas);
        Assert.Equal(2, r.PaginasLeidas);
        Assert.Equal(2, r.PaginasEscaneadas.Count);
        // La #2 falló: las que quedan conservan su número de entrada (1 y 3), no se renumera (AC-T51).
        Assert.Equal(new[] { 1, 3 }, r.PaginasEscaneadas.Select(p => p.Pagina));
        Assert.Contains("img_01.jpg", r.PaginasEscaneadas.Select(p => p.Identificador));
        Assert.Contains("img_03.jpg", r.PaginasEscaneadas.Select(p => p.Identificador));
        Assert.True(progreso.Contiene("imagen 2"));
    }

    [Fact]
    public async Task ExtraerAsync_TodasIlegibles_LanzaFuenteIlegible_SinExamenVacio_AC_T50_NFR41()
    {
        using var carpeta = new CarpetaDescartable();
        string entrada = carpeta.Sub("origen");
        string m1 = FuentesDePrueba.CrearImagenIlegible(entrada, "a.jpg");
        string m2 = FuentesDePrueba.CrearImagenIlegible(entrada, "b.png");

        var ex = await Assert.ThrowsAsync<FuenteIlegibleException>(() =>
            Nuevo().ExtraerAsync(new[] { m1, m2 }, new RecorteFuente(), Opciones(carpeta.Sub("salida")), null, CancellationToken.None));

        Assert.Matches("(?i)ninguna|no se pudo leer", ex.Message);
    }

    [Fact]
    public async Task ExtraerAsync_SinRutas_LanzaFuenteIlegible()
    {
        var ex = await Assert.ThrowsAsync<FuenteIlegibleException>(() =>
            Nuevo().ExtraerAsync(Array.Empty<string>(), new RecorteFuente(), new OpcionesExtraccion(), null, CancellationToken.None));

        Assert.Contains("ninguna imagen", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtraerAsync_ArchivoInexistenteJuntoAUnoValido_GeneraConElValido()
    {
        using var carpeta = new CarpetaDescartable();
        string entrada = carpeta.Sub("origen");
        string ok = FuentesDePrueba.CrearPng(entrada, "ok.png");
        string fantasma = Path.Combine(entrada, "no-existe.png");
        var progreso = new ProgresoSincrono();

        var r = await Nuevo().ExtraerAsync(new[] { fantasma, ok }, new RecorteFuente(), Opciones(carpeta.Sub("salida")), progreso, CancellationToken.None);

        Assert.Single(r.PaginasEscaneadas);
        Assert.Equal(2, r.PaginasSeleccionadas);
        Assert.Equal(new[] { 2 }, r.PaginasEscaneadas.Select(p => p.Pagina));
    }

    // ------------------------------------------------------------------
    // AC-T51 / NFR-43 — orden y límite del set
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_SuperaElMaximoPorMaterial_RecortaAlLimiteConAviso_NFR43()
    {
        using var carpeta = new CarpetaDescartable();
        string entrada = carpeta.Sub("origen");
        string salida = carpeta.Sub("salida");
        var rutas = Enumerable.Range(1, 4)
            .Select(i => FuentesDePrueba.CrearPng(entrada, $"foto-{i}.png"))
            .ToArray();
        var progreso = new ProgresoSincrono();

        var r = await Nuevo().ExtraerAsync(rutas, new RecorteFuente(), Opciones(salida, topeImagenes: 2), progreso, CancellationToken.None);

        Assert.Equal(2, r.PaginasEscaneadas.Count);
        Assert.Equal(2, r.PaginasSeleccionadas);
        Assert.Equal(new[] { 1, 2 }, r.PaginasEscaneadas.Select(p => p.Pagina));
        Assert.True(progreso.Contiene("el maximo por material es 2"));
    }

    [Fact]
    public async Task ExtraerAsync_TokenCancelado_LanzaOperationCanceled()
    {
        using var carpeta = new CarpetaDescartable();
        string ok = FuentesDePrueba.CrearPng(carpeta.Sub("origen"), "ok.png");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Nuevo().ExtraerAsync(new[] { ok }, new RecorteFuente(), Opciones(carpeta.Sub("salida")), null, cts.Token));
    }
}
