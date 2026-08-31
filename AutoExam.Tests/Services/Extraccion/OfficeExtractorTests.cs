using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services.Extraccion;

/// <summary>
/// <c>OfficeExtractor</c> (AutoExam/Services/OfficeExtractor.cs) — US-008, arquitectura Inc-4
/// §4.1 / §1.2. Cierra la brecha que dejó <c>test-dev-extraccion-multiformato</c> (cortado por
/// rate limit): la factory ya estaba cubierta (<see cref="FactoriaExtractoresTests"/>) pero el
/// comportamiento por formato de este extractor no tenía ningún test.
///
/// Cubre:
/// - AC-T41 / NFR-40 — medida por formato: Word → "documento unico"; Excel → "N hojas · ~M filas";
///   PowerPoint → "N diapositivas".
/// - AC-T42 / NFR-38 — un Office grande no se rechaza ni se trunca por un límite propio de la app
///   (sólo aplica <c>OpcionesExtraccion.MaxCaracteres</c>).
/// - AC-T43 / NFR-37 — archivo que no es un ZIP (dañado / cifrado OLE2) o al que le falta una
///   parte requerida → <see cref="FuenteIlegibleException"/> con la causa, sin material.
/// - AC-T44 / NFR-41 — Office sin texto extraíble → <see cref="ExtraccionResultado"/> sin material
///   (el llamador lo traduce al aviso "no se encontró contenido…"), 0 fragmentos.
///
/// Fixtures generadas en tiempo de test con <see cref="FuentesDePrueba"/> (contenedor OPC = ZIP
/// con sólo las partes que el contrato de <c>OfficeExtractor</c> declara leer) — no se versionan
/// binarios de Office.
/// </summary>
public class OfficeExtractorTests
{
    private static readonly OpcionesExtraccion Opciones = new();

    private static OfficeExtractor Nuevo() => new();

    // ------------------------------------------------------------------
    // Soporta
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(".docx", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".pptx", true)]
    [InlineData(".DOCX", true)]
    [InlineData(".pdf", false)]
    [InlineData(".doc", false)]
    [InlineData(".xls", false)]
    [InlineData(".ppt", false)]
    public void Soporta_SoloLosTresFormatosOfficeModernos(string extension, bool esperado)
        => Assert.Equal(esperado, Nuevo().Soporta(extension));

    // ------------------------------------------------------------------
    // AC-T41 / NFR-40 — medida por formato
    // ------------------------------------------------------------------

    [Fact]
    public async Task MedirAsync_Word_EsDocumentoUnico_AC_T41()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearDocx(carpeta.Ruta, "Un párrafo cualquiera con texto.");

        var medida = await Nuevo().MedirAsync(new[] { ruta }, CancellationToken.None);

        Assert.Equal(TipoFuente.Word, medida.Tipo);
        Assert.Equal("documento unico", medida.Texto);
    }

    [Theory]
    [InlineData(1, 5, "1 hoja · ~5 filas")]
    [InlineData(3, 4, "3 hojas · ~12 filas")]
    public async Task MedirAsync_Excel_InformaHojasYAproximadoDeFilas_AC_T41(int hojas, int filasPorHoja, string esperado)
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearXlsx(carpeta.Ruta, hojas, filasPorHoja);

        var medida = await Nuevo().MedirAsync(new[] { ruta }, CancellationToken.None);

        Assert.Equal(TipoFuente.Excel, medida.Tipo);
        Assert.Equal(esperado, medida.Texto);
    }

    [Theory]
    [InlineData(1, "1 diapositiva")]
    [InlineData(5, "5 diapositivas")]
    public async Task MedirAsync_PowerPoint_CuentaDiapositivas_AC_T41(int diapositivas, string esperado)
    {
        using var carpeta = new CarpetaDescartable();
        string[] textos = Enumerable.Range(1, diapositivas).Select(i => $"Diapositiva {i}").ToArray();
        string ruta = FuentesDePrueba.CrearPptx(carpeta.Ruta, textos);

        var medida = await Nuevo().MedirAsync(new[] { ruta }, CancellationToken.None);

        Assert.Equal(TipoFuente.PowerPoint, medida.Tipo);
        Assert.Equal(esperado, medida.Texto);
    }

    [Fact]
    public async Task MedirAsync_SiempreDevuelveUnaMedida_NoNulaNiVacia_NFR40()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearDocx(carpeta.Ruta, "algo");

        var medida = await Nuevo().MedirAsync(new[] { ruta }, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(medida.Texto));
    }

    // ------------------------------------------------------------------
    // AC-T40 (parcial) — el material extraído lleva el contenido del archivo
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_Word_DevuelveUnFragmentoConElTextoDelCuerpo()
    {
        using var carpeta = new CarpetaDescartable();
        string p1 = "Primer párrafo con contenido suficiente como para superar el umbral de texto útil " +
                    "que usa ExtraccionResultado.TieneTexto para considerar que hay material de estudio real.";
        string p2 = "Segundo párrafo, igualmente largo, para que el documento tenga varios cientos de " +
                    "caracteres y el resultado exponga TieneMaterial == true sin depender de imágenes.";
        string ruta = FuentesDePrueba.CrearDocx(carpeta.Ruta, p1, p2);

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None);

        var fragmento = Assert.Single(resultado.Fragmentos);
        Assert.Equal("documento", fragmento.Etiqueta);
        Assert.Contains("Primer párrafo", fragmento.Texto);
        Assert.Contains("Segundo párrafo", fragmento.Texto);
        Assert.True(resultado.TieneMaterial);
    }

    [Fact]
    public async Task ExtraerAsync_Excel_UnFragmentoPorHoja_ConSharedStringsEInlineStrings()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearXlsx(carpeta.Ruta, hojas: 2, filasPorHoja: 6);

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None);

        Assert.Equal(2, resultado.Fragmentos.Count);
        Assert.Equal(new[] { "hoja 1", "hoja 2" }, resultado.Fragmentos.Select(f => f.Etiqueta));
        Assert.Contains("CeldaCompartidaUno", resultado.Fragmentos[0].Texto);   // t="s" resuelto contra sharedStrings
        Assert.Contains("CeldaEnLinea_H1", resultado.Fragmentos[0].Texto);      // t="inlineStr"
        Assert.Contains("CeldaEnLinea_H2", resultado.Fragmentos[1].Texto);
    }

    [Fact]
    public async Task ExtraerAsync_PowerPoint_UnFragmentoPorDiapositivaConTexto()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearPptx(carpeta.Ruta, "Tema uno de la clase", "Tema dos de la clase", "Cierre");

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None);

        Assert.Equal(3, resultado.Fragmentos.Count);
        Assert.Equal(new[] { "diapositiva 1", "diapositiva 2", "diapositiva 3" }, resultado.Fragmentos.Select(f => f.Etiqueta));
        Assert.Contains("Tema uno", resultado.Fragmentos[0].Texto);
        Assert.Contains("Cierre", resultado.Fragmentos[2].Texto);
    }

    // ------------------------------------------------------------------
    // AC-T42 / NFR-38 — sin límite propio de tamaño
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_PowerPointGrande_NoRecortaDiapositivasPorUnLimitePropio_AC_T42()
    {
        using var carpeta = new CarpetaDescartable();
        // 120 diapositivas con texto corto: el total de caracteres queda por debajo de
        // OpcionesExtraccion.MaxCaracteres (90k), así que NO debe intervenir ningún recorte.
        string[] textos = Enumerable.Range(1, 120).Select(i => $"Punto {i} de la presentación").ToArray();
        string ruta = FuentesDePrueba.CrearPptx(carpeta.Ruta, textos);

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None);

        Assert.Equal(120, resultado.Fragmentos.Count);
        Assert.False(resultado.HuboRecorte);
    }

    [Fact]
    public async Task ExtraerAsync_SoloRecortaCuandoSeSuperaMaxCaracteres_NFR38()
    {
        using var carpeta = new CarpetaDescartable();
        string parrafoLargo = string.Join(" ", Enumerable.Repeat("palabra", 400));
        string ruta = FuentesDePrueba.CrearDocx(carpeta.Ruta, parrafoLargo, parrafoLargo, parrafoLargo);

        var opcionesApretadas = new OpcionesExtraccion { MaxCaracteres = 1_000 };
        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), opcionesApretadas, null, CancellationToken.None);

        Assert.True(resultado.HuboRecorte);
        // El cuerpo íntegro son ~8k caracteres; tras el recorte por presupuesto queda a una
        // fracción de eso (el mínimo por fragmento es ~1,2k, ver OfficeExtractor.AjustarPresupuesto).
        Assert.True(resultado.CaracteresTotales < 2_000, $"CaracteresTotales fue {resultado.CaracteresTotales}");
    }

    // ------------------------------------------------------------------
    // AC-T43 / NFR-37 — rechazo de archivo ilegible
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_ArchivoQueNoEsZip_LanzaFuenteIlegibleConCausa_AC_T43()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearDocxCorrupto(carpeta.Ruta);

        var ex = await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None));

        Assert.Contains("Office", ex.Message);
        Assert.Matches(new Regex("danad|contrasen", RegexOptions.IgnoreCase), ex.Message);
    }

    [Fact]
    public async Task MedirAsync_ArchivoQueNoEsZip_LanzaFuenteIlegible_AC_T43()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearDocxCorrupto(carpeta.Ruta);

        await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().MedirAsync(new[] { ruta }, CancellationToken.None));
    }

    [Fact]
    public async Task ExtraerAsync_ZipSinLaParteRequerida_LanzaFuenteIlegible_AC_T43()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearDocxSinParteRequerida(carpeta.Ruta);

        var ex = await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None));

        Assert.Contains("word/document.xml", ex.Message);
    }

    [Fact]
    public async Task ExtraerAsync_SinRuta_LanzaFuenteIlegible()
    {
        await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => Nuevo().ExtraerAsync(Array.Empty<string>(), new RecorteFuente(), Opciones, null, CancellationToken.None));
    }

    // ------------------------------------------------------------------
    // AC-T44 / NFR-41 — Office sin texto extraíble
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtraerAsync_WordSinTexto_DevuelveResultadoSinMaterial_AC_T44()
    {
        using var carpeta = new CarpetaDescartable();
        string ruta = FuentesDePrueba.CrearDocxSinTexto(carpeta.Ruta);

        var resultado = await Nuevo().ExtraerAsync(new[] { ruta }, new RecorteFuente(), Opciones, null, CancellationToken.None);

        Assert.Empty(resultado.Fragmentos);
        Assert.False(resultado.TieneMaterial);
        Assert.False(resultado.TienePaginasEscaneadas);
    }
}
