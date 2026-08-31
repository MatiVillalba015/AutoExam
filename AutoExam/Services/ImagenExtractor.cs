using System.IO;
using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>
/// Extractor de la familia de imagenes de apuntes manuscritos (US-010):
/// <c>.jpg</c> / <c>.jpeg</c> / <c>.png</c> nativos y <c>.heic</c> / <c>.heif</c> por conversion.
///
/// No hace OCR local: cada imagen viaja a la IA por el canal que ya existe
/// (<c>SolicitudGeneracion.PaginasEscaneadas</c> → <c>inline_data</c> base64) y el modelo le lee
/// el texto, igual que hoy con una pagina de PDF escaneado. Implementa el contrato
/// <see cref="IExtractorContenido"/> (arquitectura Inc-4 §4.1); la fabrica lo instancia sin
/// parametros, asi que todo lo configurable llega por <see cref="OpcionesExtraccion"/>.
/// </summary>
public sealed class ImagenExtractor : IExtractorContenido
{
    private static readonly string[] Extensiones = { ".jpg", ".jpeg", ".png", ".heic", ".heif" };

    /// <summary>
    /// Tope de peso por imagen ya preparada, alineado con <c>GeminiApiService.MaxBytesImagen</c>
    /// (3 MB). <see cref="ImagenUtil.TryPrepararParaLectura"/> reescala a
    /// <see cref="OpcionesExtraccion.LadoMaximoPaginaEscaneada"/> + JPEG q85, asi que pasarse es
    /// raro; si pasa igual, se informa (NFR-43) y se manda — el servicio de IA tiene su propia
    /// guarda por lote.
    /// </summary>
    private const long MaxBytesImagen = 3L * 1024 * 1024;

    public bool Soporta(string extension) =>
        Extensiones.Contains(Normalizar(extension));

    /// <summary>
    /// Medida por formato (NFR-40): cantidad de imagenes. No abre ningun archivo — cumple NFR-39
    /// por construccion.
    /// </summary>
    public Task<MedidaFuente> MedirAsync(IReadOnlyList<string> rutas, CancellationToken ct)
    {
        int n = rutas?.Count ?? 0;
        string texto = n == 1 ? "1 imagen" : $"{n} imagenes";
        return Task.FromResult(new MedidaFuente(TipoFuente.SetImagenes, texto));
    }

    public Task<ExtraccionResultado> ExtraerAsync(
        IReadOnlyList<string> rutas,
        RecorteFuente recorte,
        OpcionesExtraccion opciones,
        IProgress<string>? progreso,
        CancellationToken ct)
        => Task.Run(() => Extraer(rutas ?? Array.Empty<string>(), opciones, progreso, ct), ct);

    private static ExtraccionResultado Extraer(
        IReadOnlyList<string> rutas,
        OpcionesExtraccion op,
        IProgress<string>? progreso,
        CancellationToken ct)
    {
        var resultado = new ExtraccionResultado();

        // El limite "por material" (AppConfig.MaxImagenesPorMaterial) llega en
        // OpcionesExtraccion.MaxPaginasEscaneadas: el llamador (AsistenteViewModel, M4) lo fija
        // desde la config antes de invocar. Es el mismo tope que ya usa el PDF para "paginas que
        // viajan como imagen", asi que Gemini recibe siempre la misma cantidad maxima.
        int tope = Math.Max(1, op.MaxPaginasEscaneadas);

        var seleccion = rutas;
        if (rutas.Count > tope)
        {
            progreso?.Report(
                $"Agregaste {rutas.Count} imagenes y el maximo por material es {tope}: " +
                $"se toman las primeras {tope} en el orden en que las agregaste.");
            seleccion = rutas.Take(tope).ToList();
        }

        resultado.PaginasSeleccionadas = seleccion.Count;

        // NFR-44: avisar que una fuente-imagen consume mas cuota y tarda mas que un PDF con texto.
        progreso?.Report(
            $"Se van a mandar {seleccion.Count} imagenes a la IA para que les lea el texto: " +
            "puede tardar mas y consumir mas cuota que un PDF con texto.");

        int numero = 0;
        int fallidas = 0;

        foreach (string ruta in seleccion)
        {
            ct.ThrowIfCancellationRequested();
            numero++;

            try
            {
                byte[] bytes = File.ReadAllBytes(ruta);
                string ext = Normalizar(Path.GetExtension(ruta));

                // HEIC/HEIF → PNG ANTES de cualquier reescalado: 0 bytes HEIC/HEIF viajan en
                // inline_data (NFR-42). Si la conversion falla, esta imagen se descarta y se
                // sigue con el resto.
                if (ConversorHeic.EsHeic(ext))
                {
                    progreso?.Report($"Convirtiendo la imagen {numero}/{seleccion.Count} de HEIC a un formato que entiende la IA...");
                    bytes = ConversorHeic.AConvertir(bytes);
                }

                // Se valida la decodificacion: una .jpg truncada o un archivo que no es imagen
                // se descarta y se informa, igual que un HEIC que no decodifica — nunca se
                // mandan bytes corruptos a la IA etiquetados como imagen valida (AC-T50/NFR-41).
                if (!ImagenUtil.TryPrepararParaLectura(bytes, out byte[] preparada, out string mime, op.LadoMaximoPaginaEscaneada))
                {
                    fallidas++;
                    progreso?.Report(
                        $"No se pudo leer la imagen {numero} ({Path.GetFileName(ruta)}): " +
                        "archivo danado o incompleto. Se genera el examen con las demas.");
                    continue;
                }

                if (preparada.LongLength > MaxBytesImagen)
                {
                    progreso?.Report(
                        $"La imagen {numero} sigue pesando mas de {MaxBytesImagen / (1024 * 1024)} MB " +
                        "despues de reducirla: se manda igual, pero la IA podria rechazarla.");
                }

                string identificador = $"img_{numero:00}.jpg";
                string destino = string.Empty;

                if (!string.IsNullOrWhiteSpace(op.CarpetaImagenes))
                {
                    Directory.CreateDirectory(op.CarpetaImagenes);
                    destino = Path.Combine(op.CarpetaImagenes, identificador);
                    File.WriteAllBytes(destino, preparada);
                }

                resultado.PaginasEscaneadas.Add(new ImagenExtraida
                {
                    Identificador = identificador,
                    Ruta = destino,
                    MimeType = mime,
                    Pagina = numero,
                    Etiqueta = $"imagen {numero}",
                    YaPreparada = true
                });

                resultado.PaginasLeidas++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                fallidas++;
                RutasApp.RegistrarError($"ImagenExtractor({ruta})", ex);
                progreso?.Report(
                    $"No se pudo leer la imagen {numero} ({Path.GetFileName(ruta)}): " +
                    "se genera el examen con las demas.");
            }
        }

        // Ninguna imagen aporto contenido → no se crea un examen vacio (RN-4 / NFR-41):
        // el llamador traduce FuenteIlegibleException a un aviso.
        if (resultado.PaginasEscaneadas.Count == 0)
        {
            throw new FuenteIlegibleException(
                fallidas > 0
                    ? "Ninguna de las imagenes se pudo leer (formato danado, ilegible o HEIC no decodificable)."
                    : "No se agrego ninguna imagen para generar el examen.");
        }

        return resultado;
    }

    private static string Normalizar(string? extension) =>
        (extension ?? string.Empty).Trim().ToLowerInvariant();
}
