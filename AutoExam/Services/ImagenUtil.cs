using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutoExam.Services;

/// <summary>Utilidades de imagen basadas en WPF Imaging (sin System.Drawing, sin dependencias nativas extra).</summary>
public static class ImagenUtil
{
    /// <summary>
    /// Reduce la imagen si excede <paramref name="maxLado"/> px para no inflar el Base64 que viaja a Gemini.
    /// Devuelve PNG. Si algo falla, devuelve los bytes originales.
    /// </summary>
    public static byte[] RedimensionarSiHaceFalta(byte[] original, int maxLado = 1024)
    {
        try
        {
            using var ms = new MemoryStream(original);
            var decodificador = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decodificador.Frames[0];

            int lado = Math.Max(frame.PixelWidth, frame.PixelHeight);
            if (lado <= maxLado)
            {
                return original;
            }

            double escala = (double)maxLado / lado;
            var escalada = new TransformedBitmap(frame, new ScaleTransform(escala, escala));
            escalada.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(escalada));

            using var salida = new MemoryStream();
            encoder.Save(salida);
            return salida.ToArray();
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("ImagenUtil.Redimensionar", ex);
            return original;
        }
    }

    /// <summary>
    /// Prepara la imagen de una pagina escaneada para que Gemini le lea el texto: la baja a
    /// <paramref name="maxLado"/> px y la reencoda en JPEG. Es a proposito distinto de
    /// <see cref="RedimensionarSiHaceFalta"/>: una pagina de texto en PNG pesa varios MB y el
    /// Base64 de un lote entero no entraria en el request, mientras que el JPEG a esta
    /// resolucion se lee igual de bien.
    /// </summary>
    public static (byte[] bytes, string mime) PrepararParaLectura(byte[] original, int maxLado = 1600)
    {
        try
        {
            return PrepararNucleo(original, maxLado);
        }
        catch (Exception ex)
        {
            // El rescate de paginas de PDF (PdfExtractorService) quiere seguir con los bytes
            // crudos si el reencode falla; para el caso de una foto suelta que hay que validar,
            // usar TryPrepararParaLectura en su lugar.
            RutasApp.RegistrarError("ImagenUtil.PrepararParaLectura", ex);
            return (original, "image/png");
        }
    }

    /// <summary>
    /// Igual que <see cref="PrepararParaLectura"/>, pero distingue el fallo de decodificacion en
    /// vez de tragarlo: devuelve <c>false</c> (out con los bytes originales) si los datos no son
    /// una imagen que WPF Imaging pueda abrir. Lo usa <see cref="ImagenExtractor"/> (US-010) para
    /// descartar una foto ilegible o truncada y avisar, en lugar de mandar bytes corruptos a la
    /// IA etiquetados como imagen valida (AC-T50 / NFR-41 / NFR-42).
    /// </summary>
    public static bool TryPrepararParaLectura(byte[] original, out byte[] bytes, out string mime, int maxLado = 1600)
    {
        try
        {
            (bytes, mime) = PrepararNucleo(original, maxLado);
            return true;
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("ImagenUtil.TryPrepararParaLectura", ex);
            bytes = original;
            mime = "image/png";
            return false;
        }
    }

    /// <summary>
    /// Alto y ancho en pixeles sin quedarse con la imagen decodificada. Lo usa
    /// <see cref="OfficeExtractor"/> (US-018) para descartar por tamanio las imagenes de un
    /// documento antes de prepararlas: un icono de vinieta o un logo de encabezado no sirven
    /// como figura de una pregunta, y medirlos sale mucho mas barato que convertirlos.
    /// </summary>
    /// <returns><c>false</c> si los datos no son una imagen que WPF Imaging pueda abrir.</returns>
    public static bool TryMedir(byte[] original, out int ancho, out int alto)
    {
        ancho = 0;
        alto = 0;

        try
        {
            using var ms = new MemoryStream(original);

            // DelayCreation + None: alcanza con leer la cabecera para saber las medidas.
            var decodificador = BitmapDecoder.Create(
                ms, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);

            if (decodificador.Frames.Count == 0)
            {
                return false;
            }

            ancho = decodificador.Frames[0].PixelWidth;
            alto = decodificador.Frames[0].PixelHeight;

            return ancho > 0 && alto > 0;
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("ImagenUtil.TryMedir", ex);
            return false;
        }
    }

    private static (byte[] bytes, string mime) PrepararNucleo(byte[] original, int maxLado)
    {
        using var ms = new MemoryStream(original);
        var decodificador = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource cuadro = decodificador.Frames[0];

        int lado = Math.Max(cuadro.PixelWidth, cuadro.PixelHeight);
        if (lado > maxLado)
        {
            double escala = (double)maxLado / lado;
            cuadro = new TransformedBitmap(cuadro, new ScaleTransform(escala, escala));
        }

        // El JPEG no admite 1 bit por pixel, que es justo el formato de un escaneo
        // en blanco y negro: sin esta conversion el encoder tira una excepcion.
        if (cuadro.Format != PixelFormats.Bgr24 && cuadro.Format != PixelFormats.Bgra32)
        {
            cuadro = new FormatConvertedBitmap(cuadro, PixelFormats.Bgr24, null, 0);
        }

        // Una cadena TransformedBitmap/FormatConvertedBitmap no siempre se puede
        // congelar; congelarla es una optimizacion, no un requisito para encodear.
        if (cuadro.CanFreeze)
        {
            cuadro.Freeze();
        }

        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(cuadro));

        using var salida = new MemoryStream();
        encoder.Save(salida);
        return (salida.ToArray(), "image/jpeg");
    }

    /// <summary>Carga un archivo de imagen sin dejar el archivo bloqueado (OnLoad + Freeze).</summary>
    public static BitmapImage? CargarDesdeArchivo(string ruta, int anchoDecodificado = 900)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
            {
                return null;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.DecodePixelWidth = anchoDecodificado;
            bmp.UriSource = new Uri(ruta, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"ImagenUtil.CargarDesdeArchivo({ruta})", ex);
            return null;
        }
    }
}
