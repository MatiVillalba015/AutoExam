using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AutoExam.Services;

/// <summary>Referencia a un archivo ya subido y procesado por Google.</summary>
public record ArchivoRemoto(string Nombre, string Uri, string MimeType, DateTime SubidoEn)
{
    /// <summary>
    /// Google conserva los archivos 48 h y despues los borra solo. Se toman 47 para no
    /// mandar un fileUri que caduca entre el request y la respuesta.
    /// </summary>
    public bool Vigente => DateTime.UtcNow - SubidoEn < TimeSpan.FromHours(47);
}

/// <summary>
/// Cliente de la Google Files API (<c>/upload/v1beta/files</c>).
///
/// Sirve para mandarle a Gemini un PDF entero sin inflar el cuerpo del request: el archivo
/// se sube una vez, Google devuelve un <c>fileUri</c>, y a partir de ahi cada
/// generateContent lo referencia con una linea de JSON en vez de arrastrar megabytes de
/// Base64. Es lo que permite que un examen entre en una sola llamada.
/// </summary>
public class GeminiFilesService
{
    /// <summary>Raiz del servicio. Settable solo para que las pruebas apunten a un servidor local.</summary>
    public static string RaizApi { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>Tope de subida de la Files API. Por encima de esto no tiene sentido intentar.</summary>
    public const long MaxBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// Un PDF recien subido queda en PROCESSING unos segundos. Se consulta con esta
    /// separacion hasta que pasa a ACTIVE.
    /// </summary>
    private static readonly TimeSpan PausaEntreConsultas = TimeSpan.FromSeconds(1.5);

    private static readonly TimeSpan EsperaMaximaProcesado = TimeSpan.FromMinutes(3);

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    /// <summary>
    /// Sube un PDF y espera a que Google termine de procesarlo. Devuelve la referencia
    /// lista para usar en generateContent.
    /// </summary>
    public async Task<ArchivoRemoto> SubirPdfAsync(
        string apiKey,
        string rutaArchivo,
        string nombreVisible,
        IProgress<string>? progreso = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(rutaArchivo))
        {
            throw new GeminiException($"No se encontro el PDF a subir: {rutaArchivo}");
        }

        var info = new FileInfo(rutaArchivo);

        if (info.Length > MaxBytes)
        {
            throw new GeminiException(
                $"El PDF pesa {info.Length / (1024 * 1024)} MB y la Files API admite hasta 2 GB.");
        }

        progreso?.Report($"Subiendo el PDF a Google ({info.Length / (1024.0 * 1024):0.0} MB)...");

        string urlSubida = await IniciarSubidaAsync(apiKey, info.Length, nombreVisible, ct).ConfigureAwait(false);
        var subido = await EnviarBytesAsync(apiKey, urlSubida, rutaArchivo, info.Length, ct).ConfigureAwait(false);

        progreso?.Report("PDF subido. Esperando a que Google lo procese...");

        return await EsperarProcesadoAsync(apiKey, subido, progreso, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Paso 1 del protocolo resumable: se anuncia el tamanio y el tipo, y Google contesta
    /// con la URL temporal donde hay que dejar los bytes.
    /// </summary>
    private static async Task<string> IniciarSubidaAsync(
        string apiKey, long bytes, string nombreVisible, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{RaizApi}/upload/v1beta/files");
        AgregarClave(request, apiKey);

        request.Headers.TryAddWithoutValidation("X-Goog-Upload-Protocol", "resumable");
        request.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "start");
        request.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Length", bytes.ToString());
        request.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Type", "application/pdf");

        var cuerpo = new JsonObject
        {
            ["file"] = new JsonObject { ["display_name"] = Recortar(nombreVisible, 120) }
        };

        request.Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");

        using var respuesta = await Http.SendAsync(request, ct).ConfigureAwait(false);

        if (!respuesta.IsSuccessStatusCode)
        {
            string detalle = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw CrearError("No se pudo iniciar la subida del PDF", respuesta.StatusCode, detalle);
        }

        // La URL viene en una cabecera, no en el cuerpo.
        if (!respuesta.Headers.TryGetValues("X-Goog-Upload-URL", out var valores) &&
            !respuesta.Headers.TryGetValues("x-goog-upload-url", out valores))
        {
            throw new GeminiException(
                "Google acepto iniciar la subida pero no devolvio la URL de destino (X-Goog-Upload-URL).");
        }

        return valores.First();
    }

    /// <summary>Paso 2: se mandan los bytes y se cierra la subida en la misma peticion.</summary>
    private static async Task<ArchivoRemoto> EnviarBytesAsync(
        string apiKey, string urlSubida, string rutaArchivo, long bytes, CancellationToken ct)
    {
        using var flujo = new FileStream(
            rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, urlSubida);
        AgregarClave(request, apiKey);

        request.Headers.TryAddWithoutValidation("X-Goog-Upload-Offset", "0");
        request.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "upload, finalize");

        request.Content = new StreamContent(flujo);
        request.Content.Headers.ContentLength = bytes;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        using var respuesta = await Http.SendAsync(request, ct).ConfigureAwait(false);
        string contenido = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw CrearError("Fallo la subida del PDF", respuesta.StatusCode, contenido);
        }

        return LeerArchivo(contenido);
    }

    /// <summary>
    /// Paso 3: el archivo queda en PROCESSING hasta que Google lo indexa. Mandarle un
    /// fileUri en ese estado a generateContent da un 400, asi que hay que esperar el ACTIVE.
    /// </summary>
    private static async Task<ArchivoRemoto> EsperarProcesadoAsync(
        string apiKey, ArchivoRemoto archivo, IProgress<string>? progreso, CancellationToken ct)
    {
        var limite = DateTime.UtcNow + EsperaMaximaProcesado;
        int vuelta = 0;

        while (DateTime.UtcNow < limite)
        {
            ct.ThrowIfCancellationRequested();

            var (estado, actualizado) = await ConsultarAsync(apiKey, archivo, ct).ConfigureAwait(false);

            if (string.Equals(estado, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                return actualizado;
            }

            if (string.Equals(estado, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                throw new GeminiException(
                    "Google no pudo procesar el PDF subido. Puede estar danado o protegido con contrasenia.");
            }

            if (++vuelta % 4 == 0)
            {
                progreso?.Report($"Google sigue procesando el PDF ({vuelta * PausaEntreConsultas.TotalSeconds:0} s)...");
            }

            await Task.Delay(PausaEntreConsultas, ct).ConfigureAwait(false);
        }

        throw new GeminiException(
            $"Google no termino de procesar el PDF en {EsperaMaximaProcesado.TotalMinutes:0} minutos. " +
            "Proba con un alcance mas chico.");
    }

    private static async Task<(string estado, ArchivoRemoto archivo)> ConsultarAsync(
        string apiKey, ArchivoRemoto archivo, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{RaizApi}/v1beta/{archivo.Nombre}");
        AgregarClave(request, apiKey);

        using var respuesta = await Http.SendAsync(request, ct).ConfigureAwait(false);
        string contenido = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw CrearError("No se pudo consultar el estado del PDF subido", respuesta.StatusCode, contenido);
        }

        var raiz = JsonNode.Parse(contenido);

        // Al consultar, el archivo viene en la raiz; al subirlo, envuelto en "file".
        var nodo = raiz?["file"] ?? raiz;
        string estado = nodo?["state"]?.GetValue<string>() ?? "PROCESSING";

        return (estado, archivo with
        {
            Uri = nodo?["uri"]?.GetValue<string>() ?? archivo.Uri,
            MimeType = nodo?["mimeType"]?.GetValue<string>() ?? archivo.MimeType
        });
    }

    /// <summary>
    /// Borra el archivo del lado de Google. Es best-effort: si falla, igual caduca solo a
    /// las 48 h, asi que nunca debe romper la generacion del examen.
    /// </summary>
    public async Task<bool> EliminarAsync(string apiKey, ArchivoRemoto archivo, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{RaizApi}/v1beta/{archivo.Nombre}");
            AgregarClave(request, apiKey);

            using var respuesta = await Http.SendAsync(request, ct).ConfigureAwait(false);
            return respuesta.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Files API / eliminar", ex);
            return false;
        }
    }

    private static ArchivoRemoto LeerArchivo(string json)
    {
        var raiz = JsonNode.Parse(json);
        var nodo = raiz?["file"] ?? raiz;

        string nombre = nodo?["name"]?.GetValue<string>() ?? string.Empty;
        string uri = nodo?["uri"]?.GetValue<string>() ?? string.Empty;

        if (nombre.Length == 0 || uri.Length == 0)
        {
            throw new GeminiException("Google acepto el PDF pero no devolvio su identificador (name/uri).");
        }

        return new ArchivoRemoto(
            nombre,
            uri,
            nodo?["mimeType"]?.GetValue<string>() ?? "application/pdf",
            DateTime.UtcNow);
    }

    /// <summary>La clave viaja en cabecera, igual que en generateContent y por el mismo motivo.</summary>
    private static void AgregarClave(HttpRequestMessage request, string apiKey)
    {
        if (!request.Headers.TryAddWithoutValidation("x-goog-api-key", GeminiApiService.NormalizarApiKey(apiKey)))
        {
            throw new GeminiException(
                "La API Key tiene caracteres que no se pueden enviar en una cabecera HTTP. " +
                "Copiala de nuevo desde Google AI Studio, sin espacios ni saltos de linea.");
        }
    }

    private static GeminiException CrearError(string contexto, HttpStatusCode codigo, string cuerpo)
    {
        string detalle = string.Empty;

        try
        {
            detalle = JsonNode.Parse(cuerpo)?["error"]?["message"]?.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            // Cuerpo no-JSON: se usa el crudo recortado.
        }

        if (detalle.Length == 0)
        {
            detalle = Recortar(cuerpo, 300);
        }

        return new GeminiException($"{contexto} ({(int)codigo}). {detalle}") { Codigo = codigo };
    }

    private static string Recortar(string texto, int max)
        => string.IsNullOrEmpty(texto) || texto.Length <= max ? texto ?? string.Empty : texto[..max] + "...";
}
