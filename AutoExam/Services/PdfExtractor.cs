using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>
/// Adapter de <see cref="IExtractorContenido"/> para PDF (US-008, arquitectura Inc-4 §4.1).
///
/// Envuelve <see cref="PdfExtractorService"/> <b>sin modificarlo</b> (NFR-A5): traduce el
/// <see cref="RecorteFuente"/> a la <see cref="RangoPaginas"/> que ese servicio ya entiende
/// —si el recorte viene vacio, arma el rango del documento completo— y normaliza los fallos
/// de apertura (PDF danado / protegido con contrasenia) a <see cref="FuenteIlegibleException"/>
/// para que el llamador muestre un aviso en vez de un crash (NFR-37).
/// </summary>
public sealed class PdfExtractor : IExtractorContenido
{
    private readonly PdfExtractorService _pdf = new();

    public bool Soporta(string extension) =>
        Normalizar(extension) == ".pdf";

    /// <summary>Medida por formato (NFR-40): cantidad de paginas del PDF.</summary>
    public async Task<MedidaFuente> MedirAsync(IReadOnlyList<string> rutas, CancellationToken ct)
    {
        string ruta = PrimeraRuta(rutas);

        int paginas;
        try
        {
            paginas = await _pdf.ContarPaginasAsync(ruta, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw NoSePudoAbrir(ex);
        }

        string texto = paginas == 1 ? "1 pagina" : $"{paginas} paginas";
        return new MedidaFuente(TipoFuente.Pdf, texto);
    }

    public async Task<ExtraccionResultado> ExtraerAsync(
        IReadOnlyList<string> rutas,
        RecorteFuente recorte,
        OpcionesExtraccion opciones,
        IProgress<string>? progreso,
        CancellationToken ct)
    {
        string ruta = PrimeraRuta(rutas);
        recorte ??= new RecorteFuente();

        // El camino Files API llena recorte.Paginas para PDF (arquitectura Inc-4 §4.5); si viene
        // vacio ("material completo"), se lee el documento entero.
        IReadOnlyList<RangoPaginas> rangos = recorte.Paginas is { Count: > 0 }
            ? recorte.Paginas
            : await RangoCompletoAsync(ruta, ct).ConfigureAwait(false);

        try
        {
            return await _pdf.ExtraerAsync(ruta, rangos, opciones, progreso, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw NoSePudoAbrir(ex);
        }
    }

    private async Task<IReadOnlyList<RangoPaginas>> RangoCompletoAsync(string ruta, CancellationToken ct)
    {
        int total;
        try
        {
            total = await _pdf.ContarPaginasAsync(ruta, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw NoSePudoAbrir(ex);
        }

        return new[] { new RangoPaginas(1, Math.Max(1, total), "Documento completo") };
    }

    private static FuenteIlegibleException NoSePudoAbrir(Exception inner) =>
        new("No se pudo abrir el PDF: puede estar danado o protegido con contrasenia.", inner);

    private static string PrimeraRuta(IReadOnlyList<string> rutas) =>
        rutas is { Count: > 0 } && !string.IsNullOrWhiteSpace(rutas[0])
            ? rutas[0]
            : throw new FuenteIlegibleException("No se indico ningun archivo PDF para extraer.");

    private static string Normalizar(string? extension) =>
        (extension ?? string.Empty).Trim().ToLowerInvariant();
}
