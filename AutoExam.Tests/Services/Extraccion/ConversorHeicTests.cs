using System.IO;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services.Extraccion;

/// <summary>
/// <c>ConversorHeic</c> (AutoExam/Services/ConversorHeic.cs) — US-010, arquitectura Inc-4
/// §1.1 / §1.3, AC-T49 / NFR-42. Cierra la brecha señalada por test-developer: el único
/// consumidor de Magick.NET del proyecto no tenía ningún test.
///
/// Cubre:
/// - <c>EsHeic</c> — sólo <c>.heic</c> / <c>.heif</c> (case-insensitive), nunca los formatos
///   nativos ni null.
/// - <c>AConvertir</c> — HEIC/HEIF real → PNG en memoria (firma PNG en los bytes de salida),
///   0 bytes del contenedor ISO-BMFF original (NFR-42: nada de HEIC viaja después).
/// - <c>AConvertir</c> con bytes que no son HEIC (garbage / HEIC truncado) → lanza, para que
///   <see cref="ImagenExtractor"/> lo traduzca a "imagen ilegible" y no a un crash.
///
/// Usa el HEIC/HEIF real versionado en AutoExam.Tests/Recursos/Imagen (no se puede fabricar uno
/// con WPF Imaging). Depende del nativo <c>libheif</c>/<c>libde265</c> que trae
/// <c>Magick.NET-Q8-x64</c> — por eso AutoExam.Tests fija <c>PlatformTarget=x64</c>.
/// </summary>
public class ConversorHeicTests
{
    private static readonly byte[] FirmaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Theory]
    [InlineData(".heic", true)]
    [InlineData(".heif", true)]
    [InlineData(".HEIC", true)]
    [InlineData("  .Heif ", true)]
    [InlineData(".jpg", false)]
    [InlineData(".png", false)]
    [InlineData(".jpeg", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsHeic_SoloReconoceHeicYHeif(string? extension, bool esperado)
        => Assert.Equal(esperado, ConversorHeic.EsHeic(extension));

    [Theory]
    [InlineData("apunte.heic")]
    [InlineData("apunte.heif")]
    public void AConvertir_HeicReal_DevuelvePngEnMemoria_AC_T49(string nombre)
    {
        using var carpeta = new CarpetaDescartable();
        byte[] heic = File.ReadAllBytes(FuentesDePrueba.CopiarHeicReal(carpeta.Ruta, nombre));

        byte[] png = ConversorHeic.AConvertir(heic);

        Assert.True(png.Length > FirmaPng.Length);
        Assert.Equal(FirmaPng, png.Take(FirmaPng.Length));
    }

    [Fact]
    public void AConvertir_HeicReal_NoDejaBytesDelContenedorHeicEnLaSalida_NFR42()
    {
        byte[] png = ConversorHeic.AConvertir(FuentesDePrueba.BytesHeicReal());

        // El marcador de tipo ISO-BMFF de un HEIC ("ftypheic" / "ftypmif1") no debe aparecer
        // en el PNG resultante: lo que sale es una imagen re-encodada, no el archivo original.
        string comoTexto = System.Text.Encoding.ASCII.GetString(png);
        Assert.DoesNotContain("ftyp", comoTexto);
        Assert.DoesNotContain("heic", comoTexto);
    }

    [Fact]
    public void AConvertir_BytesQueNoSonImagen_Lanza()
    {
        byte[] basura = System.Text.Encoding.ASCII.GetBytes("no soy una imagen ni de lejos");

        Assert.ThrowsAny<Exception>(() => ConversorHeic.AConvertir(basura));
    }

    [Fact]
    public void AConvertir_HeicTruncado_Lanza()
    {
        byte[] truncado = FuentesDePrueba.BytesHeicReal().Take(120).ToArray();

        Assert.ThrowsAny<Exception>(() => ConversorHeic.AConvertir(truncado));
    }
}
