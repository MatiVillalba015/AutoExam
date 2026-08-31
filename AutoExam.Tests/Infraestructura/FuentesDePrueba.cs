using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AutoExam.Tests.Infraestructura;

/// <summary>
/// Carpeta temporal descartable para un único test (mismo criterio que
/// <c>RutasAisladasFixture</c> / <c>DirectorioTemporal</c>): limpieza best-effort en
/// <see cref="Dispose"/>, nunca puede tumbar la corrida.
/// </summary>
public sealed class CarpetaDescartable : IDisposable
{
    public string Ruta { get; }

    public CarpetaDescartable()
    {
        Ruta = Path.Combine(Path.GetTempPath(), "AutoExam.Tests.Extraccion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Ruta);
    }

    public string Sub(string nombre)
    {
        var ruta = Path.Combine(Ruta, nombre);
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Ruta))
            {
                Directory.Delete(Ruta, recursive: true);
            }
        }
        catch
        {
            // Best-effort.
        }
    }
}

/// <summary>
/// Genera fixtures mínimos de las fuentes que consume el pipeline de extracción multi-formato
/// (M1, specs/03-architecture.md Incremento 4 §4.1). Se generan en tiempo de test — no se
/// versionan binarios — salvo lo que no se pueda fabricar sin herramientas externas (PDF/OOXML
/// protegidos con contraseña: ver notas en specs/test-plan.md).
///
/// Los OOXML se arman como contenedor OPC (ZIP) con sólo las partes que el contrato de
/// <c>OfficeExtractor</c> declara leer (<c>word/document.xml</c>, <c>xl/sharedStrings.xml</c> +
/// <c>xl/worksheets/sheet*.xml</c>, <c>ppt/slides/slide*.xml</c>) más las partes de estructura
/// mínimas de un OPC real (<c>[Content_Types].xml</c>, <c>_rels/.rels</c>).
/// </summary>
public static class FuentesDePrueba
{
    // ------------------------------------------------------------------
    // PDF
    // ------------------------------------------------------------------

    /// <summary>PDF de <paramref name="paginas"/> páginas; cada página lleva un marcador único
    /// <c>MARCADOR_PAGINA_{n}</c> + texto de relleno (&gt; 40 chars útiles por página para pasar
    /// el umbral <c>OpcionesExtraccion.MinCaracteresPagina</c>).</summary>
    public static string CrearPdf(string carpeta, int paginas, string prefijoMarcador = "MARCADOR_PAGINA_")
    {
        var builder = new PdfDocumentBuilder();
        var fuente = builder.AddStandard14Font(Standard14Font.Helvetica);

        for (int n = 1; n <= paginas; n++)
        {
            var page = builder.AddPage(PageSize.A4);
            string texto = $"{prefijoMarcador}{n} " +
                string.Join(" ", Enumerable.Repeat("contenido de prueba para el extractor de pdf del proyecto autoexam", 3));
            EscribirParrafo(page, texto, fuente);
        }

        string ruta = Path.Combine(carpeta, $"fixture-{paginas}p.pdf");
        File.WriteAllBytes(ruta, builder.Build());
        return ruta;
    }

    /// <summary>Archivo con extensión <c>.pdf</c> cuyo contenido no es un PDF válido — el adapter
    /// debe traducir el fallo de PdfPig a <c>FuenteIlegibleException</c>.</summary>
    public static string CrearPdfCorrupto(string carpeta)
    {
        string ruta = Path.Combine(carpeta, "corrupto.pdf");
        File.WriteAllText(ruta, "%PDF-1.4\nesto no es un pdf: son bytes basura que PdfPig no puede abrir\n%%EOF");
        return ruta;
    }

    private static void EscribirParrafo(PdfPageBuilder page, string texto, PdfDocumentBuilder.AddedFont fuente)
    {
        const int anchoLinea = 60;
        double y = 780;
        for (int i = 0; i < texto.Length; i += anchoLinea)
        {
            string linea = texto.Substring(i, Math.Min(anchoLinea, texto.Length - i));
            page.AddText(linea, 10, new PdfPoint(40, y), fuente);
            y -= 16;
        }
    }

    // ------------------------------------------------------------------
    // Word (.docx)
    // ------------------------------------------------------------------

    public static string CrearDocx(string carpeta, params string[] parrafos)
    {
        var cuerpo = new StringBuilder();
        foreach (var p in parrafos)
        {
            cuerpo.Append("<w:p><w:r><w:t xml:space=\"preserve\">").Append(Escapar(p)).Append("</w:t></w:r></w:p>");
        }

        string documentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
            "<w:body>" + cuerpo + "</w:body></w:document>";

        return EscribirOpc(carpeta, "fixture.docx",
            OverrideWord,
            ("word/document.xml", documentXml));
    }

    /// <summary>.docx estructuralmente válido pero sin ningún <c>w:t</c> con texto — debe dar un
    /// <c>ExtraccionResultado</c> sin material (NFR-41 / AC-T44).</summary>
    public static string CrearDocxSinTexto(string carpeta)
    {
        string documentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
            "<w:body><w:p/><w:sectPr/></w:body></w:document>";

        return EscribirOpc(carpeta, "sin-texto.docx",
            OverrideWord,
            ("word/document.xml", documentXml));
    }

    /// <summary>Archivo <c>.docx</c> que no es un ZIP (equivale, para el extractor, a un OOXML
    /// dañado o cifrado — el cifrado OOXML es un contenedor OLE2, no un ZIP).</summary>
    public static string CrearDocxCorrupto(string carpeta)
    {
        string ruta = Path.Combine(carpeta, "corrupto.docx");
        File.WriteAllBytes(ruta, Encoding.ASCII.GetBytes("PK pero el resto no es un zip valido"));
        return ruta;
    }

    /// <summary>ZIP válido al que le falta la parte requerida <c>word/document.xml</c>.</summary>
    public static string CrearDocxSinParteRequerida(string carpeta)
        => EscribirOpc(carpeta, "sin-parte.docx", OverrideWord /* sólo estructura, sin document.xml */);

    // ------------------------------------------------------------------
    // Excel (.xlsx)
    // ------------------------------------------------------------------

    /// <param name="hojas">Cantidad de <c>xl/worksheets/sheet*.xml</c>.</param>
    /// <param name="filasPorHoja">Filas <c>&lt;row&gt;</c> por hoja. La fila 1 usa shared strings,
    /// la fila 2 usa inlineStr + número, el resto números.</param>
    public static string CrearXlsx(string carpeta, int hojas, int filasPorHoja)
    {
        var partes = new List<(string, string)>
        {
            ("xl/sharedStrings.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"2\" uniqueCount=\"2\">" +
                "<si><t>CeldaCompartidaUno</t></si><si><t>CeldaCompartidaDos</t></si></sst>")
        };

        for (int h = 1; h <= hojas; h++)
        {
            var filas = new StringBuilder();
            for (int f = 1; f <= filasPorHoja; f++)
            {
                if (f == 1)
                {
                    filas.Append($"<row r=\"{f}\"><c r=\"A{f}\" t=\"s\"><v>0</v></c><c r=\"B{f}\" t=\"s\"><v>1</v></c></row>");
                }
                else if (f == 2)
                {
                    filas.Append($"<row r=\"{f}\"><c r=\"A{f}\" t=\"inlineStr\"><is><t>CeldaEnLinea_H{h}</t></is></c><c r=\"B{f}\"><v>4242</v></c></row>");
                }
                else
                {
                    filas.Append($"<row r=\"{f}\"><c r=\"A{f}\"><v>{f}</v></c></row>");
                }
            }

            partes.Add(($"xl/worksheets/sheet{h}.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<sheetData>" + filas + "</sheetData></worksheet>"));
        }

        string sheetOverrides = string.Concat(Enumerable.Range(1, hojas).Select(h =>
            $"<Override PartName=\"/xl/worksheets/sheet{h}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));

        string contentTypes =
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" +
            sheetOverrides;

        return EscribirOpc(carpeta, $"fixture-{hojas}h.xlsx", contentTypes, partes.ToArray());
    }

    // ------------------------------------------------------------------
    // PowerPoint (.pptx)
    // ------------------------------------------------------------------

    public static string CrearPptx(string carpeta, params string[] textoPorDiapositiva)
    {
        var partes = new List<(string, string)>();
        for (int i = 0; i < textoPorDiapositiva.Length; i++)
        {
            partes.Add(($"ppt/slides/slide{i + 1}.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" " +
                "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">" +
                "<p:cSld><p:spTree><p:sp><p:txBody>" +
                "<a:p><a:r><a:t>" + Escapar(textoPorDiapositiva[i]) + "</a:t></a:r></a:p>" +
                "</p:txBody></p:sp></p:spTree></p:cSld></p:sld>"));
        }

        string slideOverrides = string.Concat(Enumerable.Range(1, textoPorDiapositiva.Length).Select(i =>
            $"<Override PartName=\"/ppt/slides/slide{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>"));

        string contentTypes =
            "<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>" +
            slideOverrides;

        return EscribirOpc(carpeta, $"fixture-{textoPorDiapositiva.Length}d.pptx", contentTypes, partes.ToArray());
    }

    // ------------------------------------------------------------------
    // Genéricos
    // ------------------------------------------------------------------

    /// <summary>Archivo cualquiera con la extensión pedida (para .doc/.xls/.ppt legacy y
    /// extensiones desconocidas: el rechazo se decide por extensión, sin abrir el archivo).</summary>
    public static string CrearArchivoConExtension(string carpeta, string extension)
    {
        string ruta = Path.Combine(carpeta, "fixture" + extension);
        // Cabecera OLE2 (D0 CF 11 E0...) — es lo que realmente es un .doc/.xls/.ppt binario.
        File.WriteAllBytes(ruta, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0 });
        return ruta;
    }

    // ------------------------------------------------------------------
    // Imagenes (US-010 — ImagenExtractor / ConversorHeic)
    // ------------------------------------------------------------------

    /// <summary>PNG real y decodificable de <paramref name="lado"/>x<paramref name="lado"/> px,
    /// generado con WPF Imaging (mismo stack que usa la app). Sirve como "foto legible".</summary>
    public static string CrearPng(string carpeta, string nombre, int lado = 48)
        => EscribirImagen(carpeta, nombre, lado, new PngBitmapEncoder());

    /// <summary>JPEG real y decodificable — "foto legible" en el otro formato nativo.</summary>
    public static string CrearJpeg(string carpeta, string nombre, int lado = 48)
        => EscribirImagen(carpeta, nombre, lado, new JpegBitmapEncoder());

    /// <summary>Bytes de un PNG en memoria (para pasarle a <c>ConversorHeic</c> algo que no es HEIC).</summary>
    public static byte[] BytesPng(int lado = 48)
    {
        var cuadro = LienzoDegradado(lado);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(cuadro));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Archivo con extension de imagen cuyo contenido NO es una imagen: WPF Imaging no lo
    /// puede decodificar. Modela una foto truncada / a medio copiar (AC-T50 / NFR-41).</summary>
    public static string CrearImagenIlegible(string carpeta, string nombre)
    {
        string ruta = Path.Combine(carpeta, nombre);
        File.WriteAllBytes(ruta, Encoding.ASCII.GetBytes("esto tiene extension de imagen pero es texto plano, no pixeles"));
        return ruta;
    }

    /// <summary>Copia el HEIC/HEIF real versionado (AutoExam.Tests/Recursos/Imagen) a
    /// <paramref name="carpeta"/> con el nombre pedido. No se puede fabricar un HEIC con WPF
    /// Imaging, por eso se versiona el binario.</summary>
    public static string CopiarHeicReal(string carpeta, string nombre)
    {
        bool heif = nombre.EndsWith(".heif", StringComparison.OrdinalIgnoreCase);
        string origen = ArchivoFuenteHelper.RutaFuente(
            heif ? "AutoExam.Tests/Recursos/Imagen/apunte.heif" : "AutoExam.Tests/Recursos/Imagen/apunte.heic");
        string destino = Path.Combine(carpeta, nombre);
        File.Copy(origen, destino, overwrite: true);
        return destino;
    }

    /// <summary>Bytes del HEIC real versionado.</summary>
    public static byte[] BytesHeicReal()
        => File.ReadAllBytes(ArchivoFuenteHelper.RutaFuente("AutoExam.Tests/Recursos/Imagen/apunte.heic"));

    private static string EscribirImagen(string carpeta, string nombre, int lado, BitmapEncoder encoder)
    {
        encoder.Frames.Add(BitmapFrame.Create(LienzoDegradado(lado)));
        string ruta = Path.Combine(carpeta, nombre);
        using var fs = new FileStream(ruta, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
        return ruta;
    }

    private static BitmapSource LienzoDegradado(int lado)
    {
        int stride = lado * 4;
        var pixeles = new byte[stride * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                int i = y * stride + x * 4;
                pixeles[i + 0] = (byte)(x * 255 / lado);   // B
                pixeles[i + 1] = (byte)(y * 255 / lado);   // G
                pixeles[i + 2] = (byte)((x + y) * 255 / (2 * lado)); // R
                pixeles[i + 3] = 255;                       // A
            }
        }

        var bmp = BitmapSource.Create(lado, lado, 96, 96, PixelFormats.Bgra32, null, pixeles, stride);
        bmp.Freeze();
        return bmp;
    }

    private const string OverrideWord =
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>";

    private static string EscribirOpc(string carpeta, string nombreArchivo, string overrides, params (string nombre, string contenido)[] partes)
    {
        string ruta = Path.Combine(carpeta, nombreArchivo);

        using var fs = new FileStream(ruta, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        Agregar(zip, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            overrides + "</Types>");

        Agregar(zip, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
            "</Relationships>");

        foreach (var (nombre, contenido) in partes)
        {
            Agregar(zip, nombre, contenido);
        }

        return ruta;
    }

    private static void Agregar(ZipArchive zip, string nombre, string contenido)
    {
        var entrada = zip.CreateEntry(nombre, CompressionLevel.Fastest);
        using var w = new StreamWriter(entrada.Open(), new UTF8Encoding(false));
        w.Write(contenido);
    }

    private static string Escapar(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
