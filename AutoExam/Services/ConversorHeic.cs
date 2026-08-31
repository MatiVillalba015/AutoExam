using ImageMagick;

namespace AutoExam.Services;

/// <summary>
/// Unico consumidor de Magick.NET en todo el proyecto (US-010, arquitectura Inc-4 §1.1/§1.3).
///
/// Decodifica los bytes de un HEIC/HEIF y los devuelve como PNG en memoria, sin depender de
/// ningun codec instalado en el SO destino: el paquete <c>Magick.NET-Q8-x64</c> trae
/// <c>libheif</c> + <c>libde265</c> ya compilados. El resto del pipeline de imagen sigue en
/// <see cref="ImagenUtil"/> (WPF Imaging, sin System.Drawing): este conversor solo entrega el
/// <c>byte[]</c> PNG que despues pasa por <see cref="ImagenUtil.PrepararParaLectura"/>.
/// </summary>
public static class ConversorHeic
{
    private static readonly string[] Extensiones = { ".heic", ".heif" };

    /// <summary>true si la extension corresponde a un formato que hay que convertir antes de enviar a la IA.</summary>
    public static bool EsHeic(string? extension) =>
        Extensiones.Contains((extension ?? string.Empty).Trim().ToLowerInvariant());

    /// <summary>
    /// HEIC/HEIF (<paramref name="heic"/>) → PNG (<c>byte[]</c>) en memoria.
    /// Lanza si Magick.NET no puede decodificar el archivo; el llamador
    /// (<see cref="ImagenExtractor"/>) traduce esa falla a "imagen ilegible" —
    /// nunca a un crash de la app (NFR-42).
    /// </summary>
    public static byte[] AConvertir(byte[] heic)
    {
        using var imagen = new MagickImage(heic);
        imagen.Format = MagickFormat.Png;
        return imagen.ToByteArray();
    }
}
