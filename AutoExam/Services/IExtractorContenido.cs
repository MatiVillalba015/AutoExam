using AutoExam.Models;

namespace AutoExam.Services;

// =============================================================================
//  Contrato del pipeline de extraccion multi-formato — arquitectura Inc-4 §4.1.
//
//  SINCRONIZACION (arquitectura Inc-4 §5): el owner de estos tipos es M1
//  (extraccion-multiformato). M3 (modelo-fuente-biblioteca) publico aca la
//  superficie del contrato en modo contract-first; M1 la completo:
//   - Este archivo conserva la superficie del contrato (interface, records,
//     excepciones, ExtensionesAdmitidas) — no se partio en varios archivos.
//   - FactoriaExtractores.Para(...) ya devuelve los extractores reales:
//     PdfExtractor (Services/PdfExtractor.cs, adapter de PdfExtractorService),
//     OfficeExtractor (Services/OfficeExtractor.cs, ZipArchive + XmlReader) e
//     ImagenExtractor (Services/ImagenExtractor.cs, M2).
// =============================================================================

/// <summary>Medida de tamanio de una fuente, expresada segun su formato (NFR-40).</summary>
/// <remarks>
/// Texto de ejemplo: "34 paginas" | "34 diapositivas" | "5 hojas · ~1.2k filas" |
/// "8 imagenes" | "documento unico".
/// </remarks>
public sealed record MedidaFuente(TipoFuente Tipo, string Texto);

/// <summary>
/// Subconjunto del material sobre el que generar el examen. Vacio ⇒ material completo.
/// <see cref="Paginas"/> se puebla solo para PDF (camino Files API); el resto de los
/// formatos usan unicamente <see cref="TemaLibre"/>.
/// </summary>
public sealed class RecorteFuente
{
    public IReadOnlyList<RangoPaginas>? Paginas { get; init; }

    public string TemaLibre { get; init; } = string.Empty;

    public bool MaterialCompleto => Paginas is null || Paginas.Count == 0;
}

/// <summary>Extractor de contenido de una familia de formatos (PDF / Office / imagenes).</summary>
public interface IExtractorContenido
{
    /// <summary>true si esta implementacion cubre la extension dada (".pdf", ".docx", ".heic"...).</summary>
    bool Soporta(string extension);

    /// <summary>Medida de tamanio por formato (NFR-40). No materializa el archivo entero (NFR-39).</summary>
    Task<MedidaFuente> MedirAsync(IReadOnlyList<string> rutas, CancellationToken ct);

    /// <summary>Extrae texto/imagenes del material, acotado por <paramref name="recorte"/>.</summary>
    Task<ExtraccionResultado> ExtraerAsync(
        IReadOnlyList<string> rutas,
        RecorteFuente recorte,
        OpcionesExtraccion opciones,
        IProgress<string>? progreso,
        CancellationToken ct);
}

/// <summary>
/// Selecciona el extractor por extension. <see cref="Para"/> devuelve null para
/// extensiones legacy (.doc/.xls/.ppt) o desconocidas — el llamador traduce eso a
/// <see cref="FormatoNoSoportadoException"/> y no crea la fuente.
/// </summary>
public static class FactoriaExtractores
{
    /// <summary>Extensiones que la app acepta como fuente valida (para el filtro del dialogo).</summary>
    public static readonly IReadOnlyList<string> ExtensionesAdmitidas = new[]
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png", ".heic", ".heif"
    };

    /// <summary>
    /// Extractor para la extension dada (case-insensitive), o null para extensiones
    /// legacy (.doc/.xls/.ppt) o desconocidas — el llamador traduce ese null a
    /// <see cref="FormatoNoSoportadoException"/> y no crea la fuente.
    /// </summary>
    public static IExtractorContenido? Para(string extension)
    {
        string ext = (extension ?? string.Empty).Trim().ToLowerInvariant();

        return ext switch
        {
            ".pdf" => new PdfExtractor(),
            ".docx" or ".xlsx" or ".pptx" => new OfficeExtractor(),
            ".jpg" or ".jpeg" or ".png" or ".heic" or ".heif" => new ImagenExtractor(),
            _ => null,
        };
    }
}

/// <summary>La extension no corresponde a ningun formato admitido (RN-8 / NFR-37).</summary>
public sealed class FormatoNoSoportadoException : Exception
{
    public FormatoNoSoportadoException()
        : base("Ese formato no se puede usar como fuente. Formatos admitidos: PDF, Word (.docx), " +
               "Excel (.xlsx), PowerPoint (.pptx) e imagenes (.jpg, .jpeg, .png, .heic, .heif). " +
               "Si es un .doc/.xls/.ppt viejo, volve a guardarlo en el formato actual.")
    {
    }

    public FormatoNoSoportadoException(string message) : base(message)
    {
    }
}

/// <summary>La fuente existe pero no se puede leer (protegida, danada, HEIC no decodificable).</summary>
public sealed class FuenteIlegibleException : Exception
{
    public FuenteIlegibleException(string message) : base(message)
    {
    }

    public FuenteIlegibleException(string message, Exception inner) : base(message, inner)
    {
    }
}
