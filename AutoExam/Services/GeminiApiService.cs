using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AutoExam.Models;

namespace AutoExam.Services;

public class GeminiException : Exception
{
    public GeminiException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }

    public HttpStatusCode? Codigo { get; init; }

    /// <summary>Se agoto la cuota (429). Reintentar con otro prompt no ayuda: solo gasta mas cuota.</summary>
    public bool EsCuota => Codigo == HttpStatusCode.TooManyRequests;

    /// <summary>La cuota agotada es la diaria, no la del minuto: esperar no sirve.</summary>
    public bool EsCuotaDiaria { get; init; }
}

/// <summary>
/// Acumula por que fallo cada intento, para poder mostrar un error accionable en vez de
/// un generico "no devolvio preguntas validas".
/// </summary>
public class DiagnosticoGeneracion
{
    private readonly List<string> _notas = new();

    public IReadOnlyList<string> Notas => _notas;

    public void Registrar(string nota)
    {
        if (!string.IsNullOrWhiteSpace(nota) && _notas.Count < 12)
        {
            _notas.Add(nota);
        }
    }

    public string Resumen() => _notas.Count == 0
        ? "  (sin detalle)"
        : string.Join(Environment.NewLine, _notas.Select(n => "  · " + n));
}

/// <summary>Todo lo que necesita el servicio para armar los prompts de un examen.</summary>
public class SolicitudGeneracion
{
    /// <summary>Clave principal. Se conserva para los llamadores que todavia pasan una sola.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Todas las claves utilizables, en orden. Si viene mas de una, un 429 rota a la
    /// siguiente en vez de fallar. Si viene vacia se usa <see cref="ApiKey"/>.
    /// </summary>
    public List<string> Claves { get; set; } = new();

    /// <summary>
    /// Modelo a usar. No se hardcodea ninguno aca: la unica fuente de verdad es
    /// <see cref="AppConfig.ModeloPorDefecto"/>, y lo normal es que el llamador pase el que
    /// el usuario eligio en Ajustes. Tener un literal distinto en este campo fue durante un
    /// tiempo la causa de que el codigo pareciera apuntar a un modelo retirado.
    /// </summary>
    public string Modelo { get; set; } = AppConfig.ModeloPorDefecto;

    /// <summary>PDF original en disco. Necesario para poder subirlo con la Files API.</summary>
    public string RutaPdf { get; set; } = string.Empty;

    /// <summary>Paginas que abarca el examen. Se usan para recortar el PDF antes de subirlo.</summary>
    public List<RangoPaginas> Rangos { get; set; } = new();

    /// <summary>Permite subir el PDF en vez de mandar el texto extraido. Ver ConvieneSubirElPdf.</summary>
    public bool UsarFilesApi { get; set; } = true;

    public string TituloLibro { get; set; } = string.Empty;
    public string Materia { get; set; } = string.Empty;

    /// <summary>Texto libre del usuario para orientar el examen (ej. "Contratos y Obligaciones").</summary>
    public string TemaLibre { get; set; } = string.Empty;

    public string AlcanceDescripcion { get; set; } = string.Empty;

    public int CantidadPreguntas { get; set; } = 10;
    public int PreguntasPorLote { get; set; } = 12;
    public bool IncluirImagenes { get; set; } = true;

    public List<FragmentoTexto> Fragmentos { get; set; } = new();

    /// <summary>Figuras del PDF sobre las que se pueden pedir preguntas.</summary>
    public List<ImagenExtraida> Imagenes { get; set; } = new();

    /// <summary>
    /// Paginas escaneadas que hacen de bibliografia: el modelo les lee el texto. No son
    /// figuras y nunca quedan adjuntas a una pregunta.
    /// </summary>
    public List<ImagenExtraida> PaginasEscaneadas { get; set; } = new();
}

/// <summary>Cliente HTTP de la API de Google Gemini (generateContent), con generacion por lotes.</summary>
public class GeminiApiService
{
    /// <summary>
    /// Endpoint base. Es settable solo para que las pruebas puedan apuntar a un servidor
    /// local y contar peticiones sin gastar cuota real. La app nunca lo cambia.
    /// </summary>
    public static string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";

    // ------------------------------------------------------------------
    // Presupuesto de peticiones
    //
    // El nivel gratuito limita por minuto (del orden de 10-15 peticiones), asi que lo
    // que hace saltar el 429 no es el tamanio del PDF (eso se procesa local) sino la
    // cantidad de requests. Con 60 preguntas y lotes de 12 salian 5 lotes, y cada lote
    // podia reintentar 3 variantes: hasta 45 peticiones en pocos segundos.
    // ------------------------------------------------------------------

    /// <summary>
    /// Preguntas por peticion. Con lotes de 15, un examen de 60 sale en 4 llamadas.
    ///
    /// Es un intercambio explicito y conviene tenerlo a la vista: partir en lotes protege
    /// contra la respuesta truncada, pero multiplica el consumo de cuota. El nivel gratuito
    /// limita por DIA —el error de Google dice "limit: 20" sobre
    /// generate_content_free_tier_requests—, asi que a 4 peticiones por examen entran 5
    /// examenes por dia y por clave, contra los 20 que entraban con una sola peticion.
    /// La rotacion de claves es lo que compensa esto: con 3 claves vuelven a ser 15.
    /// </summary>
    private const int MaxPreguntasPorLote = 15;

    /// <summary>
    /// Lotes planificados por examen: 60 preguntas / 15 por lote. Existe para que un pedido
    /// absurdo no se traduzca en decenas de peticiones.
    /// </summary>
    private const int MaxLotesPorExamen = 4;

    /// <summary>
    /// Lotes extra que se conceden cuando el modelo rinde menos de lo pedido. Sin esto, un
    /// examen de 60 en el que cada lote devuelve 3 preguntas termina con 12 y sin apelacion.
    /// </summary>
    private const int MaxLotesDeRelleno = 3;

    /// <summary>
    /// Tope absoluto de peticiones de generacion por examen, relleno incluido. Es el freno
    /// que evita cambiar un examen corto por una cuota diaria quemada: 6 de las 20
    /// generaciones del dia es lo maximo que puede costar un examen.
    /// </summary>
    private const int MaxPeticionesPorExamen = 6;

    /// <summary>Lotes seguidos sin preguntas nuevas antes de rendirse.</summary>
    private const int MaxLotesEsteriles = 2;

    /// <summary>
    /// Separacion minima entre peticiones: 2,5 s, o sea 24 por minuto como techo teorico.
    /// Con lotes de 15 preguntas un examen de 60 son 4 llamadas, asi que la pausa agrega
    /// 7,5 s al total. Es settable solo para que las pruebas midan el ritmo sin tardar
    /// minutos; la app nunca la cambia.
    /// </summary>
    public static TimeSpan SeparacionEntrePeticiones { get; set; } = TimeSpan.FromMilliseconds(2500);

    /// <summary>Base del backoff ante un 429. Settable solo para pruebas, igual que la separacion.</summary>
    public static TimeSpan EsperaBaseReintento { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Un solo turno a la vez. No es para exprimir paralelismo: es justo lo contrario.
    /// Mandar 2 o 3 peticiones en paralelo contra un limite por minuto solo adelanta el
    /// 429. El semaforo serializa los envios y, junto con la separacion de arriba,
    /// mantiene el ritmo por debajo del limite aunque haya varias pantallas pidiendo
    /// (generar un examen y "Probar conexion" al mismo tiempo, por ejemplo).
    /// </summary>
    private static readonly SemaphoreSlim Turno = new(1, 1);

    private static DateTime _ultimaPeticion = DateTime.MinValue;

    /// <summary>Ultimo envio de cada clave: el ritmo se controla por clave, no global.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _ultimaPorClave = new(StringComparer.Ordinal);

    /// <summary>
    /// Figuras adjuntas por peticion. Subio de 3 a 5 al bajar el examen a 3 peticiones:
    /// con 3 por lote solo 9 de las 12 figuras extraidas llegaban a ver al modelo.
    /// </summary>
    private const int MaxImagenesPorLote = 5;

    /// <summary>Paginas escaneadas por request. Cuatro paginas ya son ~4 MB de Base64.</summary>
    private const int MaxPaginasPorLote = 4;

    private const int MaxBytesImagen = 3 * 1024 * 1024;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(4)
    };

    private static readonly JsonSerializerOptions OpcionesLectura = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Techo de salida que se asume cuando todavia no se consulto ListModels.
    ///
    /// 8.192 es el techo real de la familia 1.5 y el minimo comun del resto, asi que como
    /// suposicion no puede quedar por encima de lo que el modelo admite. Pedir de mas no
    /// amplia nada: el modelo corta igual donde tiene su limite, y la respuesta llega
    /// truncada a mitad de un objeto JSON. Con el techo real, en cambio, el modelo sabe
    /// cuanto espacio tiene y cierra el array.
    /// </summary>
    private const int TopeTokensPorDefecto = 8192;

    /// <summary>Techo de respaldo si el modelo rechaza el pedido por el valor del techo.</summary>
    private const int TopeTokensCompatible = 8192;

    /// <summary>
    /// Techo maximo que se le pide a un modelo del que se conoce el limite. Existe para no
    /// pedir los 65.536 de un modelo nuevo cuando el examen mas largo no llega ni a 10.000:
    /// un techo desmesurado invita al modelo a explayarse en vez de ser conciso.
    /// </summary>
    private const int TopeTokensMaximo = 16384;

    /// <summary>
    /// Tope aprendido en caliente: solo baja, y solo cuando un modelo rechaza el pedido por el
    /// valor del techo. Arranca sin limite propio para no recortar el techo real del modelo;
    /// quien pone el piso de la cuenta es <see cref="CalcularTopeTokens"/>.
    /// </summary>
    private static int _topeTokensVigente = int.MaxValue;

    // ------------------------------------------------------------------
    // Prueba de conexion (pestania Ajustes y pantalla de inicio)
    // ------------------------------------------------------------------

    /// <summary>
    /// Valida la API Key y el modelo con el pedido mas barato posible: una palabra, sin esquema
    /// JSON ni material del PDF. La prueba solo tiene que contestar "¿esta clave puede hablar con
    /// este modelo?"; armar un examen para averiguarlo es lo que la hacia fallar de mentira.
    /// </summary>
    public async Task<(bool ok, string mensaje)> ProbarConexionAsync(
        string apiKey, string modelo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "Falta la API Key.");
        }

        if (string.IsNullOrWhiteSpace(modelo))
        {
            return (false, "Falta elegir un modelo.");
        }

        try
        {
            string respuesta;
            try
            {
                respuesta = await EnviarAsync(AnilloDeClaves.DeUna(apiKey), modelo, CuerpoDePrueba(sinRazonamiento: true), ct)
                    .ConfigureAwait(false);
            }
            catch (GeminiException ex) when (ex.Message.Contains("(400)", StringComparison.Ordinal))
            {
                // Varios modelos no permiten apagar el razonamiento: se reintenta sin ese campo.
                respuesta = await EnviarAsync(AnilloDeClaves.DeUna(apiKey), modelo, CuerpoDePrueba(sinRazonamiento: false), ct)
                    .ConfigureAwait(false);
            }

            string texto = LeerTextoTolerante(respuesta, out string razon, out string? bloqueo);

            if (!string.IsNullOrEmpty(bloqueo))
            {
                return (false, $"La clave y el modelo responden, pero Gemini bloqueo hasta este pedido trivial ({bloqueo}).");
            }

            if (!string.IsNullOrWhiteSpace(texto))
            {
                return (true, $"Conexion correcta con {modelo}. Respuesta: {Recortar(texto.Trim(), 80)}");
            }

            // HTTP 200 sin texto ya prueba lo unico que importa: la clave es valida y el modelo
            // existe. Suele pasar cuando el modelo gasta el cupo de la prueba razonando.
            return (true,
                $"Conexion correcta con {modelo}. Contesto sin texto (finishReason: {razon}), " +
                "algo habitual en los modelos que razonan antes de responder: la clave y el modelo son validos.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Pedido de prueba: una palabra, sin esquema JSON, con un techo de 100 tokens.</summary>
    private static JsonObject CuerpoDePrueba(bool sinRazonamiento)
    {
        var config = new JsonObject
        {
            ["temperature"] = 0,
            ["maxOutputTokens"] = 100
        };

        if (sinRazonamiento)
        {
            // Sin esto el modelo puede gastar el cupo entero pensando y devolver texto vacio.
            config["thinkingConfig"] = new JsonObject { ["thinkingBudget"] = 0 };
        }

        return new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray { new JsonObject { ["text"] = "Responde OK" } }
                }
            },
            ["generationConfig"] = config
        };
    }

    /// <summary>
    /// Lee la respuesta sin lanzar excepciones: para la prueba de conexion, una respuesta sin
    /// texto no es un fallo de la clave y no debe presentarse como tal.
    /// </summary>
    private static string LeerTextoTolerante(string respuestaJson, out string finishReason, out string? bloqueo)
    {
        finishReason = "desconocido";
        bloqueo = null;

        try
        {
            var raiz = JsonNode.Parse(respuestaJson);

            bloqueo = raiz?["promptFeedback"]?["blockReason"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(bloqueo))
            {
                return string.Empty;
            }

            var candidato = raiz?["candidates"]?.AsArray().FirstOrDefault();
            if (candidato is null)
            {
                return string.Empty;
            }

            finishReason = candidato["finishReason"]?.GetValue<string>() ?? "desconocido";

            var partes = candidato["content"]?["parts"]?.AsArray();
            if (partes is null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var parte in partes)
            {
                if (parte?["thought"]?.GetValue<bool>() == true)
                {
                    continue;
                }

                sb.Append(parte?["text"]?.GetValue<string>() ?? string.Empty);
            }

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    // ------------------------------------------------------------------
    // Listado de modelos habilitados para la clave del usuario
    // ------------------------------------------------------------------

    /// <summary>Modelos que exponen generateContent pero no sirven para generar un examen de texto.</summary>
    private static readonly string[] FamiliasDescartadas =
    {
        "embedding", "aqa", "-image", "imagen", "veo", "-tts", "audio", "learnlm", "gemma"
    };

    /// <summary>
    /// Consulta a Google que modelos tiene habilitados esta API Key. Evita depender de una
    /// lista fija en el codigo, que queda vieja cada vez que Google retira una generacion.
    /// </summary>
    public async Task<List<string>> ListarModelosAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GeminiException("Falta la API Key.");
        }

        var modelos = new List<string>();
        string? pageToken = null;

        do
        {
            string url = $"{BaseUrl}?pageSize=200";
            if (!string.IsNullOrEmpty(pageToken))
            {
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            // Tambien pasa por el turno: si el usuario toca "Detectar modelos" mientras se
            // genera un examen, esta peticion cuenta igual para el limite por minuto.
            using var respuesta = await PedirConTurnoAsync(
                () => CrearRequest(HttpMethod.Get, url, apiKey), ct, apiKey).ConfigureAwait(false);

            string contenido = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!respuesta.IsSuccessStatusCode)
            {
                throw new GeminiException(DescribirError(respuesta.StatusCode, contenido))
                {
                    Codigo = respuesta.StatusCode,
                    EsCuotaDiaria = respuesta.StatusCode == HttpStatusCode.TooManyRequests && EsCuotaDiaria(contenido)
                };
            }

            modelos.AddRange(ParsearListaModelos(contenido, out pageToken));
        }
        while (!string.IsNullOrEmpty(pageToken));

        return OrdenarModelos(modelos);
    }

    /// <summary>Extrae de una respuesta de ListModels los modelos utilizables para generar examenes.</summary>
    public static List<string> ParsearListaModelos(string json, out string? nextPageToken)
    {
        var salida = new List<string>();
        nextPageToken = null;

        var raiz = JsonNode.Parse(json);
        nextPageToken = raiz?["nextPageToken"]?.GetValue<string>();

        foreach (var nodo in raiz?["models"]?.AsArray() ?? new JsonArray())
        {
            string nombre = nodo?["name"]?.GetValue<string>() ?? string.Empty;
            if (nombre.StartsWith("models/", StringComparison.Ordinal))
            {
                nombre = nombre["models/".Length..];
            }

            if (nombre.Length == 0)
            {
                continue;
            }

            var metodos = nodo?["supportedGenerationMethods"]?.AsArray();
            bool generaContenido = metodos is not null && metodos.Any(m =>
                string.Equals(m?.GetValue<string>(), "generateContent", StringComparison.OrdinalIgnoreCase));

            if (!generaContenido)
            {
                continue;
            }

            if (FamiliasDescartadas.Any(f => nombre.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // ListModels informa el techo de salida real de cada modelo. Guardarlo evita
            // tener que adivinarlo: gemini-1.5-flash topea en 8.192 tokens y los flash de
            // las generaciones nuevas mucho mas arriba, y pedir de mas termina en una
            // respuesta cortada a la mitad, que es justo lo que hace que un lote rinda 3
            // preguntas de 15.
            int techo = nodo?["outputTokenLimit"]?.GetValue<int>() ?? 0;
            if (techo > 0)
            {
                _techoDeSalida[nombre] = techo;
            }

            salida.Add(nombre);
        }

        return salida;
    }

    /// <summary>Techo de salida por modelo, tal como lo informa ListModels.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _techoDeSalida =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Techo conocido para un modelo, o 0 si todavia no se consulto ListModels.</summary>
    public static int TechoDeSalidaConocido(string modelo)
        => _techoDeSalida.TryGetValue(modelo, out int techo) ? techo : 0;

    /// <summary>Orden descendente: las generaciones mas nuevas quedan arriba del desplegable.</summary>
    public static List<string> OrdenarModelos(IEnumerable<string> modelos) => modelos
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(m => m, StringComparer.OrdinalIgnoreCase)
        .ToList();

    // ------------------------------------------------------------------
    // Generacion del examen
    // ------------------------------------------------------------------
    public async Task<List<Pregunta>> GenerarPreguntasAsync(
        SolicitudGeneracion solicitud,
        IProgress<string>? progreso = null,
        CancellationToken ct = default)
    {
        var claves = ArmarAnillo(solicitud);

        if (claves.Vacio)
        {
            throw new GeminiException("No hay API Key configurada. Carga tu clave de Gemini en la pestania Ajustes.");
        }

        // El PDF subido es material por si mismo: con el, un alcance sin texto extraible ya
        // no es un callejon sin salida, porque Gemini le lee las paginas del lado de Google.
        var pdfRemoto = await SubirPdfSiConvieneAsync(solicitud, claves, progreso, ct).ConfigureAwait(false);

        // Sin texto todavia se puede generar: las paginas escaneadas viajan como imagen y
        // el modelo les lee el contenido. Lo unico irrecuperable es no tener ni una cosa ni la otra.
        if (pdfRemoto is null && solicitud.Fragmentos.Count == 0 && solicitud.PaginasEscaneadas.Count == 0)
        {
            throw new GeminiException(
                "No se pudo extraer texto del PDF con el alcance elegido, ni rescatar sus paginas " +
                "como imagen. Puede tratarse de un rango de paginas vacio o de un escaneo con una " +
                "compresion que no se puede decodificar (JBIG2, JPEG 2000).");
        }

        var preguntas = new List<Pregunta>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var diagnostico = new DiagnosticoGeneracion();

        // Con un eje tematico, el material se filtra ACA y no se le delega al modelo:
        // pedirle "hablá de arritmias" mientras se le mandan paginas sobre membranas
        // celulares no puede terminar bien. Se le manda el material que trata el eje.
        var material = FiltrarPorTema(solicitud.Fragmentos, solicitud.TemaLibre, out int conTema, out int total);

        if (!string.IsNullOrWhiteSpace(solicitud.TemaLibre))
        {
            progreso?.Report(conTema > 0
                ? $"Eje \"{solicitud.TemaLibre}\": {conTema} de {total} fragmentos del alcance lo tratan."
                : $"Aviso: no se encontro \"{solicitud.TemaLibre}\" en el alcance elegido. " +
                  "El examen va a salir del material disponible.");
        }

        int porLote = CalcularPreguntasPorLote(solicitud.CantidadPreguntas, solicitud.PreguntasPorLote);
        int lotesPlanificados = Math.Min(
            MaxLotesPorExamen,
            (int)Math.Ceiling(solicitud.CantidadPreguntas / (double)porLote));

        // Presupuesto real de peticiones, mayor que el plan. Los lotes de mas existen para
        // un caso concreto: el modelo devuelve menos preguntas de las pedidas (por respuesta
        // cortada, por preguntas mal formadas que se descartan, o por repetidas) y sin
        // reintentos el examen queda corto sin remedio. Antes el tope era el plan, asi que
        // cuatro lotes flojos daban un examen de 12 preguntas cuando se habian pedido 60.
        int presupuesto = Math.Min(MaxPeticionesPorExamen, lotesPlanificados + MaxLotesDeRelleno);

        progreso?.Report(
            $"Plan: {lotesPlanificados} peticion(es) para las {solicitud.CantidadPreguntas} preguntas" +
            (claves.Cantidad > 1 ? $", con {claves.Cantidad} claves disponibles" : string.Empty) +
            (pdfRemoto is not null ? ", leyendo el PDF subido a Google." : "."));

        var imagenesDisponibles = solicitud.IncluirImagenes
            ? new Queue<ImagenExtraida>(solicitud.Imagenes)
            : new Queue<ImagenExtraida>();

        // La correccion de modelo se intenta una sola vez: si el reemplazo tampoco anda,
        // el problema no es el nombre del modelo y seguir probando solo gasta cuota.
        bool modeloYaCorregido = false;

        // Lotes seguidos que no aportaron ni una pregunta nueva. Con dos ya no es mala
        // suerte: es un modelo que no va a cumplir con este material, y seguir pidiendole
        // lo mismo solo gasta cuota diaria.
        int lotesEsteriles = 0;

        for (int lote = 0; lote < presupuesto; lote++)
        {
            ct.ThrowIfCancellationRequested();

            int faltantes = solicitud.CantidadPreguntas - preguntas.Count;
            if (faltantes <= 0)
            {
                break;
            }

            if (lotesEsteriles >= MaxLotesEsteriles)
            {
                diagnostico.Registrar(
                    $"Se corto tras {lotesEsteriles} lotes seguidos sin preguntas nuevas.");
                break;
            }

            bool esRelleno = lote >= lotesPlanificados;

            // En los lotes de relleno se pide de mas: si el modelo viene rindiendo la mitad,
            // pedirle justo lo que falta garantiza volver a quedarse corto.
            int pedir = esRelleno
                ? Math.Min(porLote, Math.Max(faltantes, Math.Min(porLote, faltantes * 2)))
                : Math.Min(porLote, faltantes);

            progreso?.Report(esRelleno
                ? $"Faltan {faltantes} preguntas: pidiendo un lote mas " +
                  $"({preguntas.Count}/{solicitud.CantidadPreguntas} listas)"
                : lotesPlanificados == 1
                    ? $"Generando las {pedir} preguntas con Gemini..."
                    : $"Generando preguntas con Gemini... lote {lote + 1}/{lotesPlanificados} " +
                      $"({preguntas.Count}/{solicitud.CantidadPreguntas} listas)");

            // Cada lote recibe una ventana distinta del material: asi el examen cubre todo el
            // alcance. Los de relleno siguen rotando, para no repreguntar sobre lo mismo.
            var fragmentosLote = SeleccionarFragmentos(material, lote, Math.Max(lotesPlanificados, lote + 1));
            var imagenesLote = TomarImagenes(imagenesDisponibles, MaxImagenesPorLote);

            // Con el PDF ya subido, mandar ademas sus paginas como imagen es pagar dos veces
            // por el mismo material: Gemini lo lee del lado de Google.
            var paginasLote = pdfRemoto is not null
                ? new List<ImagenExtraida>()
                : SeleccionarVentana(solicitud.PaginasEscaneadas, lote, lotesPlanificados, MaxPaginasPorLote);

            List<Pregunta> generadas;
            try
            {
                generadas = await GenerarLoteConReintentosAsync(
                    solicitud, claves, pdfRemoto, fragmentosLote, imagenesLote, paginasLote,
                    pedir, lote + 1, diagnostico, progreso, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (GeminiException ex) when (ex.Codigo == HttpStatusCode.NotFound && !modeloYaCorregido)
            {
                // El modelo guardado no existe para esta clave. En vez de fallar y mandar al
                // usuario a Ajustes, se le pregunta a Google cuales tiene habilitados y se
                // sigue con uno de esos: la lista real de la clave siempre le gana a
                // cualquier nombre fijo escrito en el codigo, que envejece cada vez que
                // Google retira una generacion.
                modeloYaCorregido = true;

                string anterior = solicitud.Modelo;
                string? reemplazo = await BuscarModeloVigenteAsync(claves, progreso, ct).ConfigureAwait(false);

                if (reemplazo is null)
                {
                    throw;
                }

                solicitud.Modelo = reemplazo;
                ModeloCorregido?.Invoke(reemplazo);

                progreso?.Report($"\"{anterior}\" no esta disponible para tu clave. Continuando con {reemplazo}.");
                diagnostico.Registrar($"El modelo \"{anterior}\" devolvio 404; se reemplazo por \"{reemplazo}\".");

                lote--;
                continue;
            }
            catch (Exception ex) when (preguntas.Count > 0)
            {
                // Con preguntas ya generadas se prefiere entregar un examen mas corto antes que fallar entero.
                RutasApp.RegistrarError($"GenerarLote {lote + 1}", ex);
                progreso?.Report($"El lote {lote + 1} fallo ({ex.Message}). Se continua con las preguntas obtenidas.");
                break;
            }

            int nuevas = 0;
            int repetidas = 0;

            foreach (var p in generadas)
            {
                string clave = NormalizarClave(p.TextoPregunta);

                if (clave.Length <= 10)
                {
                    continue;
                }

                if (vistas.Add(clave))
                {
                    preguntas.Add(p);
                    nuevas++;
                }
                else
                {
                    repetidas++;
                }
            }

            // Un lote que devuelve solo repetidas rinde cero aunque el modelo haya
            // contestado: cuenta como esteril, o el bucle seguiria pidiendo lo mismo.
            lotesEsteriles = nuevas == 0 ? lotesEsteriles + 1 : 0;

            if (nuevas < pedir)
            {
                diagnostico.Registrar(
                    $"Lote {lote + 1}: se pidieron {pedir} preguntas y quedaron {nuevas}" +
                    (repetidas > 0 ? $" ({repetidas} repetidas de lotes anteriores)" : string.Empty) + ".");
            }
        }

        if (preguntas.Count == 0)
        {
            throw new GeminiException(ArmarMensajeSinPreguntas(solicitud, diagnostico));
        }

        var finales = preguntas.Take(solicitud.CantidadPreguntas).ToList();

        // Decir cuantas figuras quedaron adjuntas: sin esto, un examen sin imagenes se ve
        // igual venga de un PDF sin figuras o de un modelo que no completo ImagenReferencia.
        if (solicitud.IncluirImagenes && solicitud.Imagenes.Count > 0)
        {
            int conImagen = finales.Count(p => !string.IsNullOrWhiteSpace(p.RutaImagenAdjunta));

            progreso?.Report(conImagen > 0
                ? $"{conImagen} de {finales.Count} preguntas quedaron con figura ({solicitud.Imagenes.Count} extraidas del PDF)."
                : $"Se extrajeron {solicitud.Imagenes.Count} figuras del PDF pero el modelo no las uso en ninguna pregunta.");
        }

        return finales;
    }

    // ------------------------------------------------------------------
    // Modelo
    // ------------------------------------------------------------------

    /// <summary>
    /// Se dispara cuando la generacion tuvo que cambiar de modelo porque el configurado
    /// devolvio 404. Lo escucha la capa de arriba para guardar el nuevo en config.json y
    /// que la correccion no se pierda al cerrar la app.
    /// </summary>
    public event Action<string>? ModeloCorregido;

    /// <summary>
    /// Busca un modelo que la clave tenga realmente habilitado. Devuelve null si no hay
    /// ninguno o si la consulta falla, porque en ese caso el error original describe mejor
    /// el problema que un fallo al buscar el reemplazo.
    /// </summary>
    private async Task<string?> BuscarModeloVigenteAsync(
        AnilloDeClaves claves, IProgress<string>? progreso, CancellationToken ct)
    {
        try
        {
            progreso?.Report("El modelo configurado no responde. Consultando cuales habilita tu clave...");

            var modelos = await ListarModelosAsync(claves.Actual, ct).ConfigureAwait(false);

            return modelos.Count == 0 ? null : ElegirFlash(modelos);
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("BuscarModeloVigente", ex);
            return null;
        }
    }

    /// <summary>
    /// De los modelos disponibles, el flash estable: es el que mejor rinde en el nivel
    /// gratuito, que es donde corre esta app.
    /// </summary>
    public static string ElegirFlash(List<string> modelos)
        => modelos.FirstOrDefault(m =>
               m.Contains("flash", StringComparison.OrdinalIgnoreCase) &&
               !m.Contains("lite", StringComparison.OrdinalIgnoreCase) &&
               !m.Contains("preview", StringComparison.OrdinalIgnoreCase))
           ?? modelos.FirstOrDefault(m => m.Contains("flash", StringComparison.OrdinalIgnoreCase))
           ?? modelos[0];

    // ------------------------------------------------------------------
    // Claves
    // ------------------------------------------------------------------

    /// <summary>
    /// Arma el anillo de claves de la solicitud. Acepta las dos formas de configurarlo, la
    /// lista nueva y la clave suelta de siempre, para que un llamador viejo siga andando.
    /// </summary>
    private static AnilloDeClaves ArmarAnillo(SolicitudGeneracion solicitud)
    {
        var todas = new List<string>();

        if (!string.IsNullOrWhiteSpace(solicitud.ApiKey))
        {
            todas.Add(solicitud.ApiKey);
        }

        todas.AddRange(solicitud.Claves ?? new List<string>());

        return new AnilloDeClaves(todas);
    }

    // ------------------------------------------------------------------
    // Files API
    // ------------------------------------------------------------------

    private static readonly GeminiFilesService Archivos = new();

    /// <summary>
    /// PDFs ya subidos en esta sesion, para no volver a subir el mismo alcance. Google los
    /// conserva 48 h, asi que el segundo examen sobre el mismo capitulo arranca sin subida.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ArchivoRemoto> _pdfsSubidos =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Decide si conviene subir el PDF en vez de mandar el texto ya extraido.
    ///
    /// No siempre conviene, y por eso no es incondicional: para un alcance chico con texto
    /// limpio, mandar 40.000 caracteres es mas rapido y mas barato que hacerle leer a Gemini
    /// las paginas una por una (cada pagina de PDF le cuesta ~258 tokens de entrada). La
    /// subida gana en los dos casos donde el texto plano no alcanza:
    ///  · el alcance no tiene texto extraible (PDF escaneado);
    ///  · el alcance es tan grande que la extraccion tuvo que muestrear y se perdio material.
    /// </summary>
    public static bool ConvieneSubirElPdf(SolicitudGeneracion solicitud)
    {
        if (!solicitud.UsarFilesApi || string.IsNullOrWhiteSpace(solicitud.RutaPdf))
        {
            return false;
        }

        if (!File.Exists(solicitud.RutaPdf))
        {
            return false;
        }

        bool sinTexto = solicitud.Fragmentos.Sum(f => f.Texto.Length) < 2000;
        bool alcanceGrande = solicitud.Rangos.Sum(r => Math.Abs(r.Hasta - r.Desde) + 1) > 120;

        return sinTexto || alcanceGrande;
    }

    /// <summary>
    /// Recorta el PDF al alcance y lo sube, si el caso lo justifica. Devuelve null cuando
    /// no corresponde subir o cuando la subida falla: en los dos casos la generacion sigue
    /// por el camino de texto, que es el que siempre funciono.
    /// </summary>
    private static async Task<ArchivoRemoto?> SubirPdfSiConvieneAsync(
        SolicitudGeneracion solicitud,
        AnilloDeClaves claves,
        IProgress<string>? progreso,
        CancellationToken ct)
    {
        if (!ConvieneSubirElPdf(solicitud))
        {
            return null;
        }

        string llave = ClaveDeCache(solicitud, claves.Actual);

        if (_pdfsSubidos.TryGetValue(llave, out var cacheado) && cacheado.Vigente)
        {
            progreso?.Report("Reutilizando el PDF que ya estaba subido a Google.");
            return cacheado;
        }

        string? recortado = null;

        try
        {
            var pdf = new PdfExtractorService();

            string destino = Path.Combine(
                Path.GetTempPath(), "AutoExam", $"alcance-{Guid.NewGuid():N}.pdf");

            var recorte = await pdf.RecortarAsync(solicitud.RutaPdf, solicitud.Rangos, destino, ct)
                .ConfigureAwait(false);

            if (recorte is null)
            {
                return null;
            }

            // Si el alcance era el libro entero, RecortarAsync devuelve el original: no hay
            // archivo temporal que borrar despues.
            if (!string.Equals(recorte.Value.ruta, solicitud.RutaPdf, StringComparison.OrdinalIgnoreCase))
            {
                recortado = recorte.Value.ruta;
            }

            progreso?.Report($"Preparando {recorte.Value.paginas} paginas del alcance para subirlas...");

            var subido = await Archivos.SubirPdfAsync(
                claves.Actual,
                recorte.Value.ruta,
                $"{solicitud.TituloLibro} - {solicitud.AlcanceDescripcion}",
                progreso,
                ct).ConfigureAwait(false);

            _pdfsSubidos[llave] = subido;

            return subido;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // La subida es una optimizacion, no un requisito: si falla se avisa y se sigue
            // con el texto extraido, que es material suficiente para generar el examen.
            RutasApp.RegistrarError("Files API / subir PDF del alcance", ex);
            progreso?.Report($"No se pudo subir el PDF ({ex.Message}). Se continua con el texto extraido.");

            return null;
        }
        finally
        {
            if (recortado is not null)
            {
                try
                {
                    File.Delete(recortado);
                }
                catch
                {
                    // Temporal huerfano: lo limpia Windows.
                }
            }
        }
    }

    /// <summary>Identifica un alcance subido: mismo PDF, mismas paginas y misma clave.</summary>
    private static string ClaveDeCache(SolicitudGeneracion solicitud, string apiKey)
    {
        string rangos = string.Join(",", solicitud.Rangos.Select(r => $"{r.Desde}-{r.Hasta}"));

        // De la clave solo se usan los ultimos caracteres: alcanza para no mezclar archivos
        // entre cuentas y evita dejar la clave entera en una estructura en memoria.
        string sufijo = apiKey.Length > 6 ? apiKey[^6..] : apiKey;

        return $"{solicitud.RutaPdf}|{rangos}|{sufijo}";
    }

    /// <summary>
    /// Cuantas preguntas pedir por peticion, buscando la menor cantidad de peticiones.
    ///
    /// Se calcula al reves de lo que parece natural: primero cuantos lotes hacen falta como
    /// minimo (30 preguntas / 15 por lote = 2), y recien despues se reparte el total entre
    /// esos lotes (30 / 2 = 15 por lote). Repartir asi evita el reparto desparejo al que
    /// llevaba usar el valor configurado como tamanio: con 12, un examen de 30 salia en tres
    /// peticiones de 12, 12 y 6 en vez de dos de 15, y esa tercera peticion es cuota diaria
    /// tirada.
    ///
    /// El ajuste de la pestania Ajustes sigue siendo un MINIMO: puede pedir lotes mas
    /// grandes, nunca mas chicos, porque achicarlos solo multiplica las peticiones.
    /// </summary>
    private static int CalcularPreguntasPorLote(int cantidadTotal, int configurado)
    {
        int total = Math.Max(1, cantidadTotal);

        int lotes = Math.Clamp(
            (int)Math.Ceiling(total / (double)MaxPreguntasPorLote), 1, MaxLotesPorExamen);

        int repartido = (int)Math.Ceiling(total / (double)lotes);

        // Nunca por debajo del reparto, ni por encima del tope ni del examen entero.
        int porLote = Math.Max(repartido, Math.Min(configurado, MaxPreguntasPorLote));

        return Math.Clamp(Math.Min(porLote, total), 1, MaxPreguntasPorLote);
    }

    /// <summary>
    /// Intenta un lote, con un unico reintento de rescate.
    ///
    /// Antes habia una cascada de hasta tres variantes por lote. Con tres lotes por examen
    /// eso daba hasta 9 peticiones, sobre una cuota diaria de 20: dos examenes con mala
    /// suerte y no quedaba nada para el resto del dia. Ahora el esquema JSON estricto hace
    /// que una respuesta inservible sea rara, asi que queda un solo rescate, y solo para el
    /// caso que de verdad lo justifica: las figuras adjuntas, que son la causa mas comun de
    /// que el modelo devuelva basura. Las paginas escaneadas nunca se sueltan: son el
    /// material, no un adorno.
    /// </summary>
    private async Task<List<Pregunta>> GenerarLoteConReintentosAsync(
        SolicitudGeneracion solicitud,
        AnilloDeClaves claves,
        ArchivoRemoto? pdfRemoto,
        List<FragmentoTexto> fragmentos,
        List<ImagenExtraida> figuras,
        List<ImagenExtraida> paginas,
        int cantidad,
        int numeroLote,
        DiagnosticoGeneracion diagnostico,
        IProgress<string>? progreso,
        CancellationToken ct)
    {
        string baseNombre = pdfRemoto is not null
            ? "PDF subido"
            : paginas.Count > 0 ? "paginas escaneadas" : "solo texto";

        var intentos = new List<(string nombre, List<ImagenExtraida> figs, int pedir)>();

        if (figuras.Count > 0)
        {
            intentos.Add(("multimodal", figuras, cantidad));
            intentos.Add(($"{baseNombre} (fallback de figuras)", new List<ImagenExtraida>(), cantidad));
        }
        else
        {
            intentos.Add((baseNombre, new List<ImagenExtraida>(), cantidad));
        }

        Exception? ultimoError = null;

        for (int i = 0; i < intentos.Count; i++)
        {
            var (nombre, figs, pedir) = intentos[i];
            ct.ThrowIfCancellationRequested();

            try
            {
                var generadas = await GenerarLoteAsync(
                    solicitud, claves, pdfRemoto, fragmentos, figs, paginas, pedir, diagnostico, progreso, ct)
                    .ConfigureAwait(false);

                if (generadas.Count > 0)
                {
                    if (i > 0)
                    {
                        progreso?.Report($"Lote {numeroLote}: recuperado con \"{nombre}\" ({generadas.Count} preguntas).");
                    }

                    return generadas;
                }

                diagnostico.Registrar($"Lote {numeroLote} [{nombre}]: la respuesta no contenia preguntas validas.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (GeminiException ex) when (ex.EsCuota)
            {
                // Con la cuota agotada, probar el mismo lote con otro prompt no cambia
                // nada y consume el poco margen que queda. Se corta la cascada aca.
                diagnostico.Registrar($"Lote {numeroLote} [{nombre}]: cuota agotada, no se reintenta con otras variantes.");
                RutasApp.RegistrarError($"GenerarLote {numeroLote} [{nombre}] (cuota)", ex);
                throw;
            }
            catch (Exception ex)
            {
                ultimoError = ex;
                diagnostico.Registrar($"Lote {numeroLote} [{nombre}]: {ex.Message}");
                RutasApp.RegistrarError($"GenerarLote {numeroLote} [{nombre}]", ex);
            }

            if (i < intentos.Count - 1)
            {
                progreso?.Report($"Lote {numeroLote}: reintentando con \"{intentos[i + 1].nombre}\"...");
            }
        }

        // Todos los intentos agotados: si hubo excepcion se propaga, si no se devuelve vacio.
        if (ultimoError is not null)
        {
            throw ultimoError;
        }

        return new List<Pregunta>();
    }

    /// <summary>Mensaje de error accionable cuando ningun lote produjo preguntas.</summary>
    private static string ArmarMensajeSinPreguntas(SolicitudGeneracion solicitud, DiagnosticoGeneracion diagnostico)
    {
        var sb = new StringBuilder();
        sb.Append("Gemini no devolvio ninguna pregunta valida con el modelo \"")
          .Append(solicitud.Modelo)
          .AppendLine("\".");
        sb.AppendLine();
        sb.AppendLine("Detalle de los intentos:");
        sb.AppendLine(diagnostico.Resumen());
        sb.AppendLine();
        sb.AppendLine("Que probar:");
        sb.AppendLine("· Ajustes → \"Detectar modelos de mi clave\" y elegir un modelo flash reciente.");
        sb.AppendLine("· Bajar \"Preguntas por peticion\" a 8 o menos.");
        sb.AppendLine("· Destildar \"Incluir preguntas sobre graficos e imagenes\".");
        sb.AppendLine("· Elegir un alcance con mas texto real (evitar PDFs escaneados).");
        sb.Append("El detalle tecnico completo quedo en ").Append(RutasApp.ArchivoLog).Append('.');

        return sb.ToString();
    }

    private async Task<List<Pregunta>> GenerarLoteAsync(
        SolicitudGeneracion solicitud,
        AnilloDeClaves claves,
        ArchivoRemoto? pdfRemoto,
        List<FragmentoTexto> fragmentos,
        List<ImagenExtraida> figuras,
        List<ImagenExtraida> paginas,
        int cantidad,
        DiagnosticoGeneracion diagnostico,
        IProgress<string>? progreso,
        CancellationToken ct)
    {
        var partes = new JsonArray
        {
            new JsonObject { ["text"] = ConstruirPrompt(solicitud, pdfRemoto, fragmentos, figuras, paginas, cantidad) }
        };

        // El PDF entero va como referencia, no como bytes: una linea de JSON en lugar de
        // los megabytes de Base64 que antes obligaban a partir el examen en varios lotes.
        if (pdfRemoto is not null)
        {
            partes.Add(new JsonObject
            {
                ["file_data"] = new JsonObject
                {
                    ["mime_type"] = pdfRemoto.MimeType,
                    ["file_uri"] = pdfRemoto.Uri
                }
            });
        }

        // El orden importa: el prompt enumera primero las figuras y despues las paginas,
        // asi que los adjuntos tienen que ir en ese mismo orden.
        foreach (var img in figuras.Concat(paginas))
        {
            var inline = LeerImagenBase64(img);
            if (inline is null)
            {
                continue;
            }

            partes.Add(new JsonObject
            {
                ["inline_data"] = new JsonObject
                {
                    ["mime_type"] = inline.Value.mime,
                    ["data"] = inline.Value.base64
                }
            });
        }

        var cuerpo = new JsonObject
        {
            // Lo que no cambia entre lotes va aca y no en el prompt: el estilo de la salida
            // es la misma instruccion en las 4 peticiones de un examen, y repetirla en cada
            // una solo gasta tokens de entrada.
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = InstruccionDeSistema }
                }
            },

            ["contents"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["parts"] = partes }
            },
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.35,
                ["topP"] = 0.95,

                // Los modelos actuales razonan antes de responder y ese razonamiento consume
                // tokens de salida: con un techo bajo la respuesta llega vacia o cortada.
                ["maxOutputTokens"] = CalcularTopeTokens(solicitud.Modelo),

                // Fuerza JSON puro: sin este campo el modelo suele envolver la salida en ```json.
                ["response_mime_type"] = "application/json",
                ["responseMimeType"] = "application/json",

                // El esquema es lo que permite bajar el examen a una sola peticion: con el,
                // la forma de la respuesta la impone la API y no la buena voluntad del
                // modelo, asi que desaparecen los reintentos por "devolvio algo que no era
                // un array de preguntas" (cada uno de esos costaba una peticion de la cuota).
                ["responseSchema"] = EsquemaPreguntas()
            },
            ["safetySettings"] = new JsonArray
            {
                Seguridad("HARM_CATEGORY_HARASSMENT"),
                Seguridad("HARM_CATEGORY_HATE_SPEECH"),
                Seguridad("HARM_CATEGORY_SEXUALLY_EXPLICIT"),
                Seguridad("HARM_CATEGORY_DANGEROUS_CONTENT")
            }
        };

        string respuesta;
        try
        {
            respuesta = await EnviarAsync(claves, solicitud.Modelo, cuerpo, progreso, ct).ConfigureAwait(false);
        }
        catch (GeminiException ex) when (_topeTokensVigente > TopeTokensCompatible && EsRechazoDelTecho(ex.Message))
        {
            // El modelo topea la salida mas abajo: se baja el techo para esta y las proximas
            // peticiones de la sesion, en vez de repetir el mismo rechazo en cada lote.
            _topeTokensVigente = TopeTokensCompatible;
            cuerpo["generationConfig"]!["maxOutputTokens"] = TopeTokensCompatible;

            diagnostico.Registrar(
                $"El modelo no acepta maxOutputTokens {CalcularTopeTokens(solicitud.Modelo)}; se reintenta con {TopeTokensCompatible}.");

            respuesta = await EnviarAsync(claves, solicitud.Modelo, cuerpo, progreso, ct).ConfigureAwait(false);
        }

        string textoJson = ExtraerTextoRespuesta(respuesta, diagnostico);

        var mapeadas = MapearPreguntas(textoJson, fragmentos, figuras, paginas, diagnostico);

        if (mapeadas.Count == 0 && textoJson.Length > 0)
        {
            // Guarda la respuesta cruda: es lo unico que permite entender por que no mapeo.
            RutasApp.RegistrarError(
                $"Respuesta sin preguntas mapeables (modelo {solicitud.Modelo}, " +
                $"{figuras.Count} figuras, {paginas.Count} paginas escaneadas)",
                new InvalidOperationException(Recortar(textoJson, 4000)));
        }

        return mapeadas;
    }

    /// <summary>
    /// Instruccion de sistema: rol y estilo de salida, lo unico identico en todos los lotes.
    ///
    /// Aprieta el tamanio de la respuesta a proposito. Con lotes de 15 preguntas, cada una
    /// con 4 analisis mas la justificacion, el texto largo es lo que hace que el JSON se corte
    /// a la mitad y el lote entero se pierda. Una oracion por campo alcanza para estudiar y
    /// deja margen de sobra dentro del techo de tokens.
    /// </summary>
    private const string InstruccionDeSistema =
        "Sos un profesor universitario argentino (UBA) que redacta examenes multiple choice exigentes. " +
        "Escribis en español rioplatense academico, claro y sin faltas.\n" +
        "\n" +
        "REGLA NUMERO UNO: entregas EXACTAMENTE la cantidad de preguntas que se te pide, ni una " +
        "menos, en UN SOLO array JSON valido y cerrado. Un examen incompleto no sirve. Si el " +
        "material te parece escaso, igual completas la cantidad variando el enfoque de las " +
        "preguntas (aplicacion, comparacion, caso clinico, interpretacion de un dato).\n" +
        "\n" +
        "PRESUPUESTO DE ESPACIO: la respuesta entera tiene un limite de tokens y si se corta se " +
        "pierde el lote completo. Por eso escribis apretado. Es preferible una explicacion seca a " +
        "un examen truncado.\n" +
        "\n" +
        "LIMITES POR CAMPO, obligatorios:\n" +
        "· Devolves unicamente el JSON del esquema pedido. Nada de markdown ni texto alrededor.\n" +
        "· Cada opcion de \"Opciones\": 12 palabras como maximo. Frases nominales, sin oraciones " +
        "completas y sin repetir palabras del enunciado.\n" +
        "· Cada entrada de \"AnalisisPorOpcion\": UNA oracion de 18 palabras como maximo.\n" +
        "· \"ExplicacionCorrecta\" y \"JustificacionBibliografia\": UNA oracion de 20 palabras como maximo.\n" +
        "· Sin preambulos ni relleno del tipo \"esta opcion es incorrecta porque\": vas directo al " +
        "motivo. Nada de repetir el enunciado dentro de la explicacion.\n" +
        "· El enunciado va completo y autosuficiente, pero sin contexto de adorno: la brevedad se " +
        "aplica a todo, y a la pregunta tambien, aunque nunca a costa de que se entienda.";

    /// <summary>
    /// Esquema de la respuesta. Con esto la API deja de aceptar cualquier cosa: garantiza el
    /// array de objetos, los cuatro campos obligatorios y el tipo entero de
    /// <c>IndiceRespuestaCorrecta</c> y <c>PaginaOrigen</c>, que eran justo los que el modelo
    /// devolvia como texto ("2", "pagina 47") y obligaban a descartar la pregunta.
    ///
    /// <c>propertyOrdering</c> no es cosmetico: fija el orden en que el modelo emite los
    /// campos, y emitir el enunciado y las opciones antes que el analisis largo hace que una
    /// respuesta truncada igual traiga preguntas utilizables.
    /// </summary>
    private static JsonObject EsquemaPreguntas()
    {
        static JsonObject Texto(string descripcion) => new()
        {
            ["type"] = "STRING",
            ["description"] = descripcion
        };

        var pregunta = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["TextoPregunta"] = Texto("Enunciado completo de la pregunta."),
                ["Opciones"] = new JsonObject
                {
                    ["type"] = "ARRAY",
                    ["description"] = "Exactamente 4 opciones, sin numerar.",
                    ["minItems"] = 4,
                    ["maxItems"] = 4,
                    ["items"] = new JsonObject { ["type"] = "STRING" }
                },
                ["IndiceRespuestaCorrecta"] = new JsonObject
                {
                    ["type"] = "INTEGER",
                    ["description"] = "Indice 0-3 de la opcion correcta."
                },
                ["PaginaOrigen"] = new JsonObject
                {
                    ["type"] = "INTEGER",
                    ["description"] = "Numero de pagina del PDF del que sale la respuesta."
                },
                ["JustificacionBibliografia"] = Texto(
                    "Cita concreta del material, empezando por \"Pagina N:\"."),
                ["ImagenReferencia"] = Texto(
                    "Identificador exacto de la figura adjunta, o cadena vacia si la pregunta no es sobre una figura."),
                ["AnalisisOpciones"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new JsonObject
                    {
                        ["ExplicacionCorrecta"] = Texto("Por que la opcion correcta lo es."),
                        ["AnalisisPorOpcion"] = new JsonObject
                        {
                            ["type"] = "ARRAY",
                            ["description"] = "Exactamente 4 entradas, una por opcion en el mismo orden.",
                            ["minItems"] = 4,
                            ["maxItems"] = 4,
                            ["items"] = new JsonObject { ["type"] = "STRING" }
                        }
                    },
                    ["required"] = new JsonArray { "ExplicacionCorrecta", "AnalisisPorOpcion" },
                    ["propertyOrdering"] = new JsonArray { "ExplicacionCorrecta", "AnalisisPorOpcion" }
                }
            },
            ["required"] = new JsonArray
            {
                "TextoPregunta", "Opciones", "IndiceRespuestaCorrecta", "PaginaOrigen", "JustificacionBibliografia"
            },
            ["propertyOrdering"] = new JsonArray
            {
                "TextoPregunta", "Opciones", "IndiceRespuestaCorrecta", "ImagenReferencia",
                "PaginaOrigen", "JustificacionBibliografia", "AnalisisOpciones"
            }
        };

        return new JsonObject
        {
            ["type"] = "ARRAY",
            ["items"] = pregunta
        };
    }

    /// <summary>
    /// Techo de salida de la peticion: el que el modelo realmente admite.
    ///
    /// Antes se calculaba a ojo (900 tokens por pregunta) y podia pedir 17.500 a un modelo
    /// que topea en 8.192. Pedir por encima del limite no amplia nada: el modelo corta donde
    /// tiene su limite y el JSON llega partido a mitad de un objeto, del que solo se rescatan
    /// las preguntas completas. De ahi salian los lotes que rendian 3 de 15.
    ///
    /// El techo real lo informa ListModels (<c>outputTokenLimit</c>). Mientras no se haya
    /// consultado se asume el minimo comun, que es preferible a pasarse.
    /// </summary>
    private static int CalcularTopeTokens(string modelo)
    {
        int delModelo = TechoDeSalidaConocido(modelo);

        int techo = delModelo > 0
            ? Math.Min(delModelo, TopeTokensMaximo)
            : TopeTokensPorDefecto;

        // _topeTokensVigente solo baja, y solo si el modelo rechazo un pedido por el techo.
        return Math.Min(techo, _topeTokensVigente);
    }

    private static string Recortar(string texto, int max)
        => texto.Length <= max ? texto : texto[..max] + "... [truncado]";

    /// <summary>True si el 400 se debe al techo de tokens pedido y no al contenido del prompt.</summary>
    private static bool EsRechazoDelTecho(string mensaje)
        => mensaje.Contains("(400)", StringComparison.Ordinal)
           && (mensaje.Contains("maxOutputTokens", StringComparison.OrdinalIgnoreCase)
               || mensaje.Contains("max_output_tokens", StringComparison.OrdinalIgnoreCase)
               || mensaje.Contains("output token", StringComparison.OrdinalIgnoreCase));

    private static JsonObject Seguridad(string categoria) => new()
    {
        ["category"] = categoria,
        ["threshold"] = "BLOCK_ONLY_HIGH"
    };

    // ------------------------------------------------------------------
    // Prompt
    // ------------------------------------------------------------------
    private static string ConstruirPrompt(
        SolicitudGeneracion s,
        ArchivoRemoto? pdfRemoto,
        List<FragmentoTexto> fragmentos,
        List<ImagenExtraida> figuras,
        List<ImagenExtraida> paginas,
        int cantidad)
    {
        var sb = new StringBuilder();

        // El rol y el estilo ya viajan en systemInstruction: repetirlos aca seria pagar dos
        // veces por la misma instruccion en cada uno de los lotes.
        sb.AppendLine($"MATERIA: {s.Materia}");
        sb.AppendLine($"BIBLIOGRAFIA: {s.TituloLibro}");
        if (!string.IsNullOrWhiteSpace(s.AlcanceDescripcion))
        {
            sb.AppendLine($"ALCANCE: {s.AlcanceDescripcion}");
        }

        if (!string.IsNullOrWhiteSpace(s.TemaLibre))
        {
            // El material que sigue ya viene filtrado por este eje, asi que la instruccion
            // puede ser terminante. Antes decia "si casi no aparece usa lo mas cercano", y
            // el modelo se agarraba de esa salida para ignorar el eje casi siempre.
            sb.AppendLine($"EJE TEMATICO OBLIGATORIO: \"{s.TemaLibre}\".");
            sb.AppendLine($"TODAS las preguntas tienen que ser sobre \"{s.TemaLibre}\". El material de abajo ya fue filtrado para dejar las paginas que tratan este eje.");
            sb.AppendLine($"Si un fragmento no habla de \"{s.TemaLibre}\", ignoralo entero en vez de sacar preguntas de ahi.");
            sb.AppendLine($"Si con el material entregado no llegas a la cantidad pedida, devolvé MENOS preguntas: es preferible un examen corto y sobre \"{s.TemaLibre}\" que uno completo sobre otra cosa.");
        }

        sb.AppendLine();
        sb.AppendLine($"TAREA: generá EXACTAMENTE {cantidad} preguntas de opcion multiple, con 4 opciones cada una y UNA sola correcta.");
        sb.AppendLine();
        sb.AppendLine("REGLAS OBLIGATORIAS:");
        sb.AppendLine(pdfRemoto is not null
            ? "1. Basate unicamente en el PDF adjunto, que contiene exactamente las paginas del alcance. Prohibido inventar contenido que no este en ese PDF."
            : paginas.Count > 0
                ? "1. Basate unicamente en el MATERIAL entregado: el texto de mas abajo y las paginas escaneadas adjuntas. Prohibido inventar contenido que no este en el material."
                : "1. Basate unicamente en el MATERIAL entregado mas abajo. Prohibido inventar contenido que no este en el material.");
        sb.AppendLine("2. Nivel universitario: evaluá comprension, aplicacion y analisis, no memorizacion literal de una frase.");
        sb.AppendLine("3. Los 4 distractores tienen que ser plausibles y del mismo largo aproximado. Prohibido usar \"todas las anteriores\", \"ninguna de las anteriores\" o pistas gramaticales.");
        sb.AppendLine("4. Distribuí la posicion de la respuesta correcta entre los indices 0, 1, 2 y 3.");
        sb.AppendLine("5. Cubrí temas distintos entre las preguntas; nada de reformular la misma idea dos veces.");
        sb.AppendLine("7. \"JustificacionBibliografia\": UNA sola oracion que empieza por la pagina. Formato \"Pagina N: <razon>\". El material trae cada pagina marcada con [Pagina N]; usa ese numero, nunca uno inventado ni el numero impreso en el pie de pagina.");
        sb.AppendLine("7b. \"PaginaOrigen\" es OBLIGATORIO y va como numero entero: la pagina [Pagina N] de la que sacaste la respuesta. Si la respuesta se apoya en varias paginas, poné la principal. Nunca dejes 0 ni un numero que no aparezca en el material entregado.");
        sb.AppendLine("8. \"AnalisisPorOpcion\" lleva exactamente 4 entradas, una por opcion en el mismo orden: por que la correcta lo es, y para cada incorrecta el error puntual.");

        if (figuras.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("FIGURAS ADJUNTAS:");
            sb.AppendLine($"Las primeras {figuras.Count} imagenes adjuntas son figuras extraidas del PDF, en este orden:");
            for (int i = 0; i < figuras.Count; i++)
            {
                var img = figuras[i];
                sb.AppendLine($"  - Figura #{i + 1}: identificador \"{img.Identificador}\" (pagina {img.Pagina} del PDF).");
            }

            // Sin una cuota explicita el modelo casi nunca completa "ImagenReferencia" y el
            // examen sale sin una sola imagen, aunque el PDF este lleno de esquemas.
            int conFigura = Math.Max(1, Math.Min(figuras.Count, cantidad / 3));

            sb.AppendLine($"OBLIGATORIO: de las {cantidad} preguntas, AL MENOS {conFigura} tienen que ser sobre estas figuras.");
            sb.AppendLine("En cada una de esas preguntas poné el identificador EXACTO de la figura en el campo \"ImagenReferencia\" (por ejemplo \"" + figuras[0].Identificador + "\"). Sin ese campo la figura no se le muestra al alumno y la pregunta queda incompleta.");
            sb.AppendLine("Usá una figura distinta en cada una; no repitas la misma.");
            sb.AppendLine("Las preguntas sobre figuras tienen que ser autosuficientes viendo la figura (ej. \"Segun el esquema, que estructura...\"). Nunca digas \"la imagen adjunta numero 2\".");
            sb.AppendLine("Solo si una figura es un logo, una foto decorativa o esta ilegible, salteala y usá otra de la lista.");
            sb.AppendLine("En las preguntas que NO son sobre figuras, dejá \"ImagenReferencia\" vacio.");
        }

        if (paginas.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("MATERIAL EN IMAGENES (PAGINAS ESCANEADAS):");
            sb.AppendLine(
                $"Estas paginas del PDF no tienen texto extraible, asi que se adjuntan como imagen. " +
                $"Son {paginas.Count} y vienen despues de las figuras, en este orden:");

            foreach (var pag in paginas)
            {
                sb.AppendLine($"  - Pagina {pag.Pagina} del PDF.");
            }

            sb.AppendLine("Leé el texto de esas imagenes y tratalo como bibliografia, igual que el material escrito.");
            sb.AppendLine("NO son figuras: no generes preguntas sobre el aspecto de la pagina, la calidad del escaneo ni la maquetacion, y dejá \"ImagenReferencia\" vacio en las preguntas que salgan de ellas.");
            sb.AppendLine("En \"PaginaOrigen\" y en la justificacion usá el numero de pagina indicado arriba para cada imagen.");
            sb.AppendLine("Si una pagina esta ilegible o en blanco, ignorala y trabajá con las demas.");
        }

        // La forma de la respuesta ya la impone responseSchema, asi que aca solo queda lo
        // que un esquema no puede expresar: que el array traiga la cantidad pedida.
        sb.AppendLine();
        sb.AppendLine($"SALIDA: un array JSON con EXACTAMENTE {cantidad} preguntas, ni una menos, siguiendo el esquema pedido.");

        sb.AppendLine();
        sb.AppendLine("===== MATERIAL DE ESTUDIO =====");

        if (pdfRemoto is not null)
        {
            // Con el PDF subido no se vuelca el texto: duplicaria el material y el request.
            sb.AppendLine();
            sb.AppendLine("El material es el PDF adjunto. Ya viene recortado al alcance pedido, asi que");
            sb.AppendLine("todas sus paginas son material valido y no hay que descartar ninguna.");
            sb.AppendLine("Para \"PaginaOrigen\" usá el numero de pagina que aparece impreso en la pagina del PDF.");
        }
        else
        {
            if (fragmentos.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("(Este alcance no tiene texto extraible: todo el material son las paginas escaneadas adjuntas.)");
            }

            foreach (var f in fragmentos)
            {
                sb.AppendLine();
                sb.AppendLine($"--- {f.Referencia} ---");
                sb.AppendLine(f.Texto);
            }
        }

        sb.AppendLine();
        sb.AppendLine("===== FIN DEL MATERIAL =====");

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // HTTP
    // ------------------------------------------------------------------

    /// <summary>
    /// Unico lugar donde se arma un request a Gemini. La API Key SIEMPRE viaja en la cabecera
    /// x-goog-api-key y nunca como query parameter: las claves nuevas (formato AQ.Ab8...)
    /// son rechazadas con 401 si se mandan en la URL.
    /// </summary>
    private static HttpRequestMessage CrearRequest(HttpMethod metodo, string url, string apiKey)
    {
        var request = new HttpRequestMessage(metodo, url);

        // TryAddWithoutValidation evita que un caracter raro pegado desde el navegador
        // reviente con FormatException en vez de dar un error entendible.
        if (!request.Headers.TryAddWithoutValidation("x-goog-api-key", NormalizarApiKey(apiKey)))
        {
            request.Dispose();
            throw new GeminiException(
                "La API Key tiene caracteres que no se pueden enviar en una cabecera HTTP. " +
                "Copiala de nuevo desde Google AI Studio, sin espacios ni saltos de linea.");
        }

        return request;
    }

    /// <summary>
    /// Limpia la clave antes de mandarla: al copiar desde el navegador suelen colarse espacios,
    /// saltos de linea o caracteres invisibles (BOM, zero-width) que provocan un 401 enganioso.
    /// </summary>
    public static string NormalizarApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(apiKey.Length);
        foreach (char c in apiKey)
        {
            // Se descartan espacios, saltos, controles y los invisibles tipo BOM o
            // zero-width que se cuelan al copiar la clave desde el navegador.
            var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            bool invisible = char.IsWhiteSpace(c)
                             || char.IsControl(c)
                             || categoria == System.Globalization.UnicodeCategory.Format
                             || categoria == System.Globalization.UnicodeCategory.OtherNotAssigned;

            if (!invisible)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static Task<string> EnviarAsync(
        AnilloDeClaves claves, string modelo, JsonObject cuerpo, CancellationToken ct)
        => EnviarAsync(claves, modelo, cuerpo, null, ct);

    /// <summary>
    /// Manda una peticion de generacion y devuelve el cuerpo crudo de la respuesta.
    ///
    /// Ante un 429 hay dos salidas y la rotacion siempre gana a la espera:
    ///  · si queda otra clave, se cambia y se reintenta al instante (cada clave tiene su
    ///    propia cuota, asi que esperar los 40 s que pide Google no aportaria nada);
    ///  · recien cuando no queda ninguna se aplica el backoff, y solo si el limite era por
    ///    minuto. Contra una cuota diaria agotada en todas las claves, esperar no sirve.
    /// </summary>
    private static async Task<string> EnviarAsync(
        AnilloDeClaves claves, string modelo, JsonObject cuerpo, IProgress<string>? progreso, CancellationToken ct)
    {
        string url = $"{BaseUrl}/{modelo}:generateContent";

        const int maxIntentos = 3;

        // Bajo de 5 a 3: cada reintento cuenta tambien para la cuota DIARIA, asi que
        // insistir cinco veces contra un limite por minuto se comia la cuarta parte del
        // presupuesto del dia para un examen que igual podia terminar fallando.
        const int maxIntentosCuota = 3;

        // La rotacion se cuenta aparte de los reintentos: pasar por N claves no tiene que
        // gastar el presupuesto de backoff, que existe para otra cosa.
        int intento = 1;
        int rotaciones = 0;
        int maxRotaciones = Math.Max(0, claves.Cantidad - 1);

        while (true)
        {
            string claveEnUso = claves.Actual;
            HttpResponseMessage respuesta;

            try
            {
                respuesta = await EnviarConTurnoAsync(claveEnUso, url, cuerpo, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                if (intento >= maxIntentos)
                {
                    throw new GeminiException("La API de Gemini no respondio a tiempo (timeout). Probá con menos preguntas por lote.");
                }

                await Task.Delay(1500 * intento, ct).ConfigureAwait(false);
                intento++;
                continue;
            }
            catch (HttpRequestException ex)
            {
                if (intento >= maxIntentos)
                {
                    throw new GeminiException($"No se pudo contactar a la API de Gemini: {ex.Message}", ex);
                }

                await Task.Delay(1500 * intento, ct).ConfigureAwait(false);
                intento++;
                continue;
            }

            using (respuesta)
            {
                string contenido = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (respuesta.IsSuccessStatusCode)
                {
                    return contenido;
                }

                bool esCuota = respuesta.StatusCode == HttpStatusCode.TooManyRequests;
                bool cuotaDiaria = esCuota && EsCuotaDiaria(contenido);

                // Primera opcion ante cualquier 429: cambiar de clave y seguir de largo.
                if (esCuota && rotaciones < maxRotaciones && claves.Rotar(quemarActual: cuotaDiaria))
                {
                    rotaciones++;

                    progreso?.Report(
                        cuotaDiaria
                            ? $"La clave {rotaciones} agoto su cuota diaria. Continuando con la clave " +
                              $"{claves.NumeroActual} de {claves.Cantidad}..."
                            : $"Cuota por minuto alcanzada (429). Continuando con la clave " +
                              $"{claves.NumeroActual} de {claves.Cantidad}...");

                    continue;
                }

                bool reintentable = respuesta.StatusCode is HttpStatusCode.InternalServerError
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout
                    || (esCuota && !cuotaDiaria);

                int tope = esCuota ? maxIntentosCuota : maxIntentos;

                if (reintentable && intento < tope)
                {
                    var espera = CalcularEspera(respuesta, contenido, intento, esCuota);

                    if (esCuota)
                    {
                        progreso?.Report(
                            $"Cuota por minuto alcanzada (429). Esperando {espera.TotalSeconds:0} s y " +
                            $"reintentando (intento {intento + 1} de {tope})...");
                    }

                    await Task.Delay(espera, ct).ConfigureAwait(false);
                    intento++;
                    continue;
                }

                throw new GeminiException(DescribirError(respuesta.StatusCode, contenido, claves))
                {
                    Codigo = respuesta.StatusCode,
                    EsCuotaDiaria = cuotaDiaria
                };
            }
        }
    }

    private static Task<HttpResponseMessage> EnviarConTurnoAsync(
        string apiKey, string url, JsonObject cuerpo, CancellationToken ct)
        => PedirConTurnoAsync(
            () =>
            {
                var request = CrearRequest(HttpMethod.Post, url, apiKey);
                request.Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");
                return request;
            },
            ct,
            apiKey);

    /// <summary>
    /// Manda una peticion respetando el turno y la separacion minima. El semaforo se
    /// mantiene tomado durante todo el envio: asi nunca hay dos peticiones en vuelo, que
    /// es lo que en la practica hace saltar el limite por minuto.
    /// </summary>
    private static async Task<HttpResponseMessage> PedirConTurnoAsync(
        Func<HttpRequestMessage> armarRequest, CancellationToken ct, string? clave = null)
    {
        await Turno.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            // La separacion se lleva POR CLAVE, porque el limite de Google tambien es por
            // clave. Cobrarle a una clave recien estrenada la espera que genero otra seria
            // regalar segundos justo cuando se acaba de rotar para no perderlos.
            var ultima = clave is not null && _ultimaPorClave.TryGetValue(clave, out var marca)
                ? marca
                : _ultimaPeticion;

            var desdeLaUltima = DateTime.UtcNow - ultima;
            if (desdeLaUltima < SeparacionEntrePeticiones)
            {
                await Task.Delay(SeparacionEntrePeticiones - desdeLaUltima, ct).ConfigureAwait(false);
            }

            using var request = armarRequest();

            return await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            // Se marca al terminar y no al empezar: la separacion cuenta entre el fin de
            // una peticion y el inicio de la siguiente, que es el caso conservador.
            _ultimaPeticion = DateTime.UtcNow;

            if (clave is not null)
            {
                _ultimaPorClave[clave] = _ultimaPeticion;
            }

            Turno.Release();
        }
    }

    /// <summary>
    /// Cuanto esperar antes de reintentar. Google manda el tiempo exacto en el cuerpo del
    /// error (RetryInfo.retryDelay) o en la cabecera Retry-After; hacerle caso es mas
    /// certero que cualquier backoff inventado.
    /// </summary>
    private static TimeSpan CalcularEspera(
        HttpResponseMessage respuesta, string contenido, int intento, bool esCuota)
    {
        var sugerida = LeerRetryAfter(respuesta) ?? LeerRetryDelay(contenido);

        // Backoff exponencial como piso, mas generoso para la cuota que para un 5xx.
        var propia = esCuota
            ? EsperaBaseReintento * Math.Pow(2, intento)
            : EsperaBaseReintento * Math.Pow(2, intento) * 0.4;

        var espera = sugerida is null ? propia : Sugerida(sugerida.Value, propia);

        // Un tope: mejor fallar con un mensaje claro que dejar la app colgada 10 minutos.
        return espera > TimeSpan.FromSeconds(90) ? TimeSpan.FromSeconds(90) : espera;

        static TimeSpan Sugerida(TimeSpan delServidor, TimeSpan propia)
            => delServidor > propia ? delServidor : propia;
    }

    private static TimeSpan? LeerRetryAfter(HttpResponseMessage respuesta)
    {
        var retry = respuesta.Headers.RetryAfter;

        if (retry?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retry?.Date is DateTimeOffset fecha)
        {
            var falta = fecha - DateTimeOffset.UtcNow;
            return falta > TimeSpan.Zero ? falta : null;
        }

        return null;
    }

    /// <summary>Extrae el "retryDelay": "38s" que Gemini pone en error.details[].</summary>
    private static TimeSpan? LeerRetryDelay(string contenido)
    {
        try
        {
            var detalles = JsonNode.Parse(contenido)?["error"]?["details"]?.AsArray();
            if (detalles is null)
            {
                return null;
            }

            foreach (var detalle in detalles)
            {
                string? texto = detalle?["retryDelay"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(texto))
                {
                    continue;
                }

                string numero = texto.TrimEnd('s', 'S').Trim();
                if (double.TryParse(numero, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double segundos)
                    && segundos > 0)
                {
                    return TimeSpan.FromSeconds(segundos);
                }
            }
        }
        catch
        {
            // Sin dato del servidor se usa el backoff propio.
        }

        return null;
    }

    /// <summary>
    /// True si el 429 es por la cuota DIARIA. Esperar no la devuelve, asi que reintentar
    /// solo alarga la espera antes del mismo error.
    /// </summary>
    private static bool EsCuotaDiaria(string contenido)
        => contenido.Contains("PerDay", StringComparison.OrdinalIgnoreCase)
           || contenido.Contains("per day", StringComparison.OrdinalIgnoreCase);

    private static string DescribirError(HttpStatusCode codigo, string contenido, AnilloDeClaves? claves = null)
    {
        string detalle = contenido;
        try
        {
            var nodo = JsonNode.Parse(contenido);
            detalle = nodo?["error"]?["message"]?.GetValue<string>() ?? contenido;
        }
        catch
        {
            // Se usa el cuerpo crudo.
        }

        if (detalle.Length > 400)
        {
            detalle = detalle[..400] + "...";
        }

        return codigo switch
        {
            HttpStatusCode.BadRequest => $"Peticion rechazada por Gemini (400). {detalle}",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                $"API Key rechazada por Google ({(int)codigo}). La clave viaja en la cabecera " +
                "x-goog-api-key, asi que revisá que este completa y que el proyecto de Google AI Studio " +
                $"tenga habilitada la Generative Language API. {detalle}",
            HttpStatusCode.NotFound =>
                "El modelo no existe o ya fue retirado por Google (404). En Ajustes toca " +
                $"\"Detectar modelos\" para traer la lista real que habilita tu clave. {detalle}",
            HttpStatusCode.TooManyRequests when EsCuotaDiaria(contenido) =>
                "Se agoto la cuota DIARIA de Gemini (429) en " +
                (claves is null || claves.Cantidad <= 1
                    ? "tu clave"
                    : $"las {claves.Cantidad} claves configuradas") +
                ". No se arregla esperando unos minutos: se renueva al otro dia. " +
                (claves is null || claves.Cantidad <= 1
                    ? "Cargá una segunda clave en Ajustes y AutoExam va a rotar sola cuando la primera se agote. "
                    : "Agregá otra clave en Ajustes o seguí mañana. ") +
                detalle,
            HttpStatusCode.TooManyRequests =>
                "Se agoto la cuota por minuto de tu clave (429) y los reintentos automaticos tampoco alcanzaron. " +
                "Esperá un minuto y reintentá; si se repite, bajá la cantidad de preguntas del examen. " +
                $"{detalle}",
            _ => $"Error {(int)codigo} de la API de Gemini. {detalle}"
        };
    }

    private static string ExtraerTextoRespuesta(string respuestaJson, DiagnosticoGeneracion? diagnostico = null)
    {
        try
        {
            var raiz = JsonNode.Parse(respuestaJson);

            var bloqueo = raiz?["promptFeedback"]?["blockReason"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(bloqueo))
            {
                throw new GeminiException($"Gemini bloqueo el pedido por filtros de contenido ({bloqueo}).");
            }

            var candidato = raiz?["candidates"]?.AsArray().FirstOrDefault();
            if (candidato is null)
            {
                throw new GeminiException("Gemini devolvio una respuesta vacia (sin candidatos).");
            }

            string razon = candidato["finishReason"]?.GetValue<string>() ?? "desconocido";

            var partes = candidato["content"]?["parts"]?.AsArray();
            if (partes is null || partes.Count == 0)
            {
                throw new GeminiException(ExplicarFinishReason(razon));
            }

            var sb = new StringBuilder();
            foreach (var parte in partes)
            {
                // Los modelos con razonamiento devuelven partes de "pensamiento" sin texto util:
                // se ignoran y solo se concatena el contenido real.
                if (parte?["thought"]?.GetValue<bool>() == true)
                {
                    continue;
                }

                sb.Append(parte?["text"]?.GetValue<string>() ?? string.Empty);
            }

            string texto = sb.ToString();

            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new GeminiException(ExplicarFinishReason(razon));
            }

            if (!string.Equals(razon, "STOP", StringComparison.OrdinalIgnoreCase))
            {
                diagnostico?.Registrar($"respuesta incompleta (finishReason: {razon}); se rescatan las preguntas completas.");
            }

            return texto;
        }
        catch (GeminiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GeminiException("No se pudo interpretar la respuesta de Gemini.", ex);
        }
    }

    private static string ExplicarFinishReason(string razon) => razon.ToUpperInvariant() switch
    {
        "MAX_TOKENS" =>
            "Gemini agoto el limite de tokens antes de escribir una sola pregunta. " +
            "Baja \"Preguntas por peticion\" en Ajustes (proba con 6-8).",
        "SAFETY" =>
            "Gemini bloqueo la respuesta por filtros de seguridad sobre el contenido del PDF.",
        "RECITATION" =>
            "Gemini corto la respuesta por politica de recitacion: el material se parece demasiado " +
            "a texto protegido. Proba con otro rango de paginas.",
        _ => $"Gemini no genero contenido (finishReason: {razon})."
    };

    // ------------------------------------------------------------------
    // Parseo del JSON de preguntas
    // ------------------------------------------------------------------
    private class PreguntaDto
    {
        public string? TextoPregunta { get; set; }
        public string? ImagenReferencia { get; set; }
        public string? RutaImagenAdjunta { get; set; }
        public List<string>? Opciones { get; set; }
        public int IndiceRespuestaCorrecta { get; set; }
        public string? JustificacionBibliografia { get; set; }
        public AnalisisDto? AnalisisOpciones { get; set; }
        public int PaginaOrigen { get; set; }
    }

    private class AnalisisDto
    {
        public string? ExplicacionCorrecta { get; set; }
        public List<string>? AnalisisPorOpcion { get; set; }
    }

    private static List<Pregunta> MapearPreguntas(
        string textoJson,
        List<FragmentoTexto> fragmentos,
        List<ImagenExtraida> figuras,
        List<ImagenExtraida> paginas,
        DiagnosticoGeneracion? diagnostico = null)
    {
        var dtos = DeserializarArray(textoJson);
        var lista = new List<Pregunta>();
        int descartadas = 0;

        if (dtos.Count == 0 && textoJson.Trim().Length > 0)
        {
            diagnostico?.Registrar(
                "la respuesta no era un array JSON de preguntas interpretable " +
                $"(empieza con: \"{Recortar(textoJson.TrimStart(), 90).Replace('\n', ' ')}\").");
        }

        // Cuando el alcance no tiene texto, la referencia por defecto sale de las paginas
        // escaneadas: si no, la cita bibliografica de cada pregunta quedaria vacia.
        string referenciaFragmento = fragmentos.Count > 0
            ? fragmentos[0].Referencia
            : DescribirPaginas(paginas);

        string etiquetaModulo = fragmentos.FirstOrDefault()?.Etiqueta
                                ?? paginas.FirstOrDefault()?.Etiqueta
                                ?? string.Empty;

        // Paginas que realmente vio el modelo en este lote. Sirven para dos cosas:
        // descartar una pagina inventada fuera de rango, y poder decir "entre la X y
        // la Y" cuando el modelo no arriesga una pagina exacta.
        var paginasDelLote = fragmentos.Select(f => f.PaginaDesde)
            .Concat(fragmentos.Select(f => f.PaginaHasta))
            .Concat(paginas.Select(p => p.Pagina))
            .Where(p => p > 0)
            .ToList();

        int minimaPagina = paginasDelLote.Count > 0 ? paginasDelLote.Min() : 0;
        int maximaPagina = paginasDelLote.Count > 0 ? paginasDelLote.Max() : 0;

        string alcancePaginas = minimaPagina == 0
            ? string.Empty
            : minimaPagina == maximaPagina
                ? $"pagina {minimaPagina}"
                : $"paginas {minimaPagina} a {maximaPagina}";

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.TextoPregunta) || dto.Opciones is null || dto.Opciones.Count < 2)
            {
                descartadas++;
                continue;
            }

            var opciones = dto.Opciones
                .Select(o => (o ?? string.Empty).Trim())
                .Where(o => o.Length > 0)
                .ToList();

            int correcta = dto.IndiceRespuestaCorrecta;

            // Si el modelo devuelve mas de 4 opciones, recortar a las primeras 4 solo es
            // valido cuando la correcta esta entre ellas; si no, la pregunta quedaria mal.
            if (opciones.Count > 4)
            {
                if (correcta is < 0 or > 3)
                {
                    descartadas++;
                    continue;
                }

                opciones = opciones.Take(4).ToList();
            }

            if (opciones.Count != 4 || opciones.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
            {
                descartadas++;
                continue;
            }

            if (correcta is < 0 or > 3)
            {
                descartadas++;
                continue;
            }

            var analisis = new AnalisisOpciones
            {
                ExplicacionCorrecta = (dto.AnalisisOpciones?.ExplicacionCorrecta ?? string.Empty).Trim(),
                AnalisisPorOpcion = NormalizarAnalisis(dto.AnalisisOpciones?.AnalisisPorOpcion, correcta)
            };

            // Solo las figuras pueden quedar adjuntas a una pregunta: una pagina escaneada
            // es bibliografia, y mostrarla como ilustracion revelaria la respuesta.
            string? rutaImagen = ResolverImagen(dto.ImagenReferencia ?? dto.RutaImagenAdjunta, figuras, out int paginaImagen);

            lista.Add(new Pregunta
            {
                TextoPregunta = dto.TextoPregunta.Trim(),
                Opciones = opciones,
                IndiceRespuestaCorrecta = correcta,
                RutaImagenAdjunta = rutaImagen,
                JustificacionBibliografia = string.IsNullOrWhiteSpace(dto.JustificacionBibliografia)
                    ? referenciaFragmento
                    : dto.JustificacionBibliografia.Trim(),
                PaginaOrigen = ResolverPagina(dto.PaginaOrigen, paginaImagen, minimaPagina, maximaPagina),
                PaginasAlcance = alcancePaginas,
                ModuloOrigen = etiquetaModulo,
                AnalisisOpciones = analisis
            });
        }

        if (descartadas > 0)
        {
            diagnostico?.Registrar(
                $"se descartaron {descartadas} de {dtos.Count} preguntas por formato invalido " +
                "(no traian exactamente 4 opciones distintas o el indice correcto estaba fuera de rango).");
        }

        return lista;
    }

    private static List<string> NormalizarAnalisis(List<string>? origen, int indiceCorrecta)
    {
        var salida = new List<string>(4);
        for (int i = 0; i < 4; i++)
        {
            string valor = origen is not null && i < origen.Count ? (origen[i] ?? string.Empty).Trim() : string.Empty;

            if (valor.Length == 0)
            {
                valor = i == indiceCorrecta
                    ? "Es la opcion que se desprende del material citado."
                    : "No se corresponde con lo que plantea el material citado.";
            }

            salida.Add(valor);
        }

        return salida;
    }

    private static string? ResolverImagen(string? referencia, List<ImagenExtraida> imagenes, out int pagina)
    {
        pagina = 0;

        if (string.IsNullOrWhiteSpace(referencia) || imagenes.Count == 0)
        {
            return null;
        }

        string clave = Path.GetFileName(referencia.Trim().Trim('"'));

        var img = imagenes.FirstOrDefault(i =>
                      string.Equals(i.Identificador, clave, StringComparison.OrdinalIgnoreCase))
                  ?? imagenes.FirstOrDefault(i => referencia.Contains(i.Identificador, StringComparison.OrdinalIgnoreCase));

        if (img is null)
        {
            // Tolera respuestas del tipo "Imagen #2".
            var digitos = new string(referencia.Where(char.IsDigit).ToArray());
            if (int.TryParse(digitos, out int n) && n >= 1 && n <= imagenes.Count)
            {
                img = imagenes[n - 1];
            }
        }

        if (img is null || !File.Exists(img.Ruta))
        {
            return null;
        }

        pagina = img.Pagina;
        return img.Ruta;
    }

    /// <summary>
    /// Deserializa el array de preguntas tolerando envoltorios en markdown y respuestas
    /// truncadas por limite de tokens (rescata los objetos completos).
    /// </summary>
    private static List<PreguntaDto> DeserializarArray(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new List<PreguntaDto>();
        }

        string limpio = LimpiarMarkdown(texto);

        // El modelo puede envolver el array en un objeto {"preguntas": [...]}.
        int inicio = limpio.IndexOf('[');
        if (inicio < 0)
        {
            return new List<PreguntaDto>();
        }

        int fin = limpio.LastIndexOf(']');
        if (fin > inicio)
        {
            var directo = IntentarDeserializar(limpio[inicio..(fin + 1)]);
            if (directo is { Count: > 0 })
            {
                return directo;
            }
        }

        // Camino de rescate: se recorren los objetos de primer nivel del array y se
        // rearma uno nuevo solo con los completos. Cubre la respuesta cortada por
        // MAX_TOKENS, donde el ultimo objeto queda a medio escribir.
        var objetos = ExtraerObjetosDeNivelSuperior(limpio, inicio);
        if (objetos.Count > 0)
        {
            var rescatado = IntentarDeserializar("[" + string.Join(",", objetos) + "]");
            if (rescatado is not null)
            {
                return rescatado;
            }
        }

        return new List<PreguntaDto>();
    }

    /// <summary>
    /// Devuelve el texto de cada objeto {...} de primer nivel dentro del array que empieza
    /// en <paramref name="inicioArray"/>. Lleva la cuenta de llaves respetando strings y
    /// escapes, asi un "]" dentro de "Opciones" no corta el recorrido por error.
    /// </summary>
    public static List<string> ExtraerObjetosDeNivelSuperior(string json, int inicioArray)
    {
        var objetos = new List<string>();

        if (inicioArray < 0 || inicioArray >= json.Length)
        {
            return objetos;
        }

        int profundidad = 0;
        int inicioObjeto = -1;
        bool enString = false;
        bool escapado = false;

        for (int i = inicioArray + 1; i < json.Length; i++)
        {
            char c = json[i];

            if (enString)
            {
                if (escapado)
                {
                    escapado = false;
                }
                else if (c == '\\')
                {
                    escapado = true;
                }
                else if (c == '"')
                {
                    enString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    enString = true;
                    break;

                case '{':
                    if (profundidad == 0)
                    {
                        inicioObjeto = i;
                    }

                    profundidad++;
                    break;

                case '}':
                    profundidad--;
                    if (profundidad == 0 && inicioObjeto >= 0)
                    {
                        objetos.Add(json[inicioObjeto..(i + 1)]);
                        inicioObjeto = -1;
                    }

                    break;

                case ']':
                    if (profundidad == 0)
                    {
                        return objetos; // Cierre real del array de preguntas.
                    }

                    break;
            }
        }

        return objetos;
    }

    /// <summary>
    /// Saca el envoltorio Markdown que el modelo agrega a veces pese a pedirle JSON puro:
    /// vallas ```json ... ```, texto suelto antes del bloque, o comillas tipograficas.
    /// </summary>
    public static string LimpiarMarkdown(string texto)
    {
        string limpio = texto.Trim();

        // Cualquier valla de apertura, este o no al principio del texto.
        int apertura = limpio.IndexOf("```", StringComparison.Ordinal);
        if (apertura >= 0)
        {
            limpio = limpio[(apertura + 3)..];

            // La valla puede venir etiquetada: ```json, ```JSON, ```javascript...
            int finLinea = limpio.IndexOf('\n');
            if (finLinea >= 0)
            {
                string etiqueta = limpio[..finLinea].Trim();
                if (etiqueta.Length <= 12 && etiqueta.All(char.IsLetterOrDigit))
                {
                    limpio = limpio[(finLinea + 1)..];
                }
            }
            else if (limpio.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                limpio = limpio[4..];
            }

            int cierre = limpio.LastIndexOf("```", StringComparison.Ordinal);
            if (cierre >= 0)
            {
                limpio = limpio[..cierre];
            }
        }

        return limpio.Trim();
    }

    private static List<PreguntaDto>? IntentarDeserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<PreguntaDto>>(json, OpcionesLectura);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Auxiliares
    // ------------------------------------------------------------------
    /// <summary>
    /// Deja adelante los fragmentos que realmente hablan del eje tematico. Ordenar en vez
    /// de descartar es a proposito: si el eje aparece poco, el examen sale igual pero
    /// empezando por lo mas cercano, en lugar de fallar o de ignorar el eje.
    /// </summary>
    private static List<FragmentoTexto> FiltrarPorTema(
        List<FragmentoTexto> fragmentos, string tema, out int conTema, out int total)
    {
        total = fragmentos.Count;
        conTema = 0;

        var terminos = TerminosDelTema(tema);
        if (terminos.Count == 0 || fragmentos.Count == 0)
        {
            return fragmentos;
        }

        var puntuados = fragmentos
            .Select(f => (Fragmento: f, Puntos: PuntuarTema(f.Texto, terminos)))
            .ToList();

        conTema = puntuados.Count(p => p.Puntos > 0);

        if (conTema == 0)
        {
            return fragmentos;
        }

        // Si hay suficiente material del tema, se queda SOLO con ese: mezclarlo con
        // paginas ajenas es lo que hacia que el examen terminara hablando de otra cosa.
        var delTema = puntuados.Where(p => p.Puntos > 0)
            .OrderByDescending(p => p.Puntos)
            .Select(p => p.Fragmento)
            .ToList();

        if (delTema.Sum(f => f.Texto.Length) >= 6_000 || conTema >= 3)
        {
            return delTema;
        }

        // Con poco material del tema se completan con el resto, pero el tema va primero.
        var resto = puntuados.Where(p => p.Puntos == 0).Select(p => p.Fragmento);
        return delTema.Concat(resto).ToList();
    }

    /// <summary>Palabras del eje que valen para buscar: se descartan articulos y conectores.</summary>
    private static List<string> TerminosDelTema(string tema)
    {
        if (string.IsNullOrWhiteSpace(tema))
        {
            return new List<string>();
        }

        var vacias = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de", "del", "la", "las", "el", "los", "y", "e", "o", "u", "en", "con", "para",
            "por", "sobre", "un", "una", "al", "que", "the", "of", "and"
        };

        return SinAcentos(tema)
            .Split(new[] { ' ', ',', ';', '.', '/', '-', '(', ')', '"', '\'' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length >= 4 && !vacias.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    private static int PuntuarTema(string texto, List<string> terminos)
    {
        string plano = SinAcentos(texto);
        int puntos = 0;

        foreach (string termino in terminos)
        {
            // Se busca por raiz (los primeros caracteres) para que "arritmia" tambien
            // encuentre "arritmias" y "arritmico" sin armar un lematizador.
            string raiz = termino.Length > 6 ? termino[..^1] : termino;

            int desde = 0;
            while (true)
            {
                int i = plano.IndexOf(raiz, desde, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                {
                    break;
                }

                puntos++;
                desde = i + raiz.Length;
            }
        }

        return puntos;
    }

    /// <summary>Compara sin tildes: "linfocito" y "linfócito" tienen que valer lo mismo.</summary>
    private static string SinAcentos(string texto)
    {
        string normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);

        foreach (char c in normalizado)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>Paginas que como mucho ve un lote. Mas que esto satura la ventana de entrada.</summary>
    private const int MaxPaginasPorLoteTexto = 20;

    /// <summary>Piso de paginas por lote: con menos material las 15 preguntas salen repetitivas.</summary>
    private const int MinPaginasPorLoteTexto = 10;

    /// <summary>
    /// Ventana de material propia de cada lote.
    ///
    /// Reparte el alcance entre los lotes en vez de mandarles el libro entero a todos: asi el
    /// examen cubre todo el material, cada peticion pesa poco, y dos lotes no pueden preguntar
    /// lo mismo porque ni siquiera vieron las mismas paginas. La ventana se recorta a
    /// <see cref="MaxPaginasPorLoteTexto"/> paginas; si el reparto diera menos de
    /// <see cref="MinPaginasPorLoteTexto"/>, se estira, porque con dos paginas no salen 15
    /// preguntas distintas.
    /// </summary>
    private static List<FragmentoTexto> SeleccionarFragmentos(List<FragmentoTexto> todos, int lote, int totalLotes)
    {
        if (todos.Count == 0)
        {
            return todos;
        }

        if (totalLotes <= 1)
        {
            return RecortarAPaginas(todos, MaxPaginasPorLoteTexto);
        }

        int porLote = Math.Max(1, (int)Math.Ceiling(todos.Count / (double)totalLotes));

        // Un fragmento de solape para no perder continuidad entre lotes, pero SOLO si a cada
        // lote le toca mas de un fragmento. Con ventanas de un fragmento, retroceder uno hace
        // que el lote 2 arranque donde arranco el lote 1 y, tras el recorte por paginas, los
        // dos terminan viendo exactamente el mismo material: una peticion entera gastada en
        // repreguntar sobre las mismas paginas.
        int solape = porLote > 1 && lote > 0 ? 1 : 0;

        int inicio = Math.Max(0, lote * porLote - solape);
        int cantidad = Math.Min(porLote + solape, todos.Count - inicio);

        var ventana = cantidad <= 0
            ? todos.TakeLast(porLote).ToList()
            : todos.GetRange(inicio, cantidad);

        // Si al lote le toco muy poco material, se completa con lo que sigue: mejor solapar
        // con el lote vecino que pedirle 15 preguntas sobre tres paginas.
        if (ContarPaginas(ventana) < MinPaginasPorLoteTexto && ventana.Count < todos.Count)
        {
            int desde = todos.IndexOf(ventana[0]);

            for (int i = desde + ventana.Count; i < todos.Count; i++)
            {
                ventana.Add(todos[i]);

                if (ContarPaginas(ventana) >= MinPaginasPorLoteTexto)
                {
                    break;
                }
            }
        }

        return RecortarAPaginas(ventana, MaxPaginasPorLoteTexto);
    }

    /// <summary>Paginas distintas que cubre un conjunto de fragmentos.</summary>
    private static int ContarPaginas(List<FragmentoTexto> fragmentos)
        => fragmentos.Sum(f => Math.Max(1, f.PaginaHasta - f.PaginaDesde + 1));

    /// <summary>
    /// Corta la ventana en cuanto supera el tope de paginas. Se corta por fragmento entero:
    /// partir un fragmento al medio dejaria una frase colgada y una cita de pagina que no
    /// corresponde con el texto entregado.
    /// </summary>
    private static List<FragmentoTexto> RecortarAPaginas(List<FragmentoTexto> fragmentos, int maxPaginas)
    {
        var salida = new List<FragmentoTexto>();
        int paginas = 0;

        foreach (var f in fragmentos)
        {
            int suyas = Math.Max(1, f.PaginaHasta - f.PaginaDesde + 1);

            if (salida.Count > 0 && paginas + suyas > maxPaginas)
            {
                break;
            }

            salida.Add(f);
            paginas += suyas;
        }

        return salida.Count > 0 ? salida : fragmentos;
    }

    /// <summary>
    /// Pagina que se le muestra al usuario. Una pagina fuera del tramo que el modelo
    /// tuvo delante es una invencion, y mandarlo a leer una pagina equivocada es peor
    /// que no darle ninguna: en ese caso se prefiere el tramo del lote.
    /// </summary>
    private static int ResolverPagina(int declarada, int paginaImagen, int minima, int maxima)
    {
        bool dentroDelRango(int p) => p > 0 && (minima == 0 || (p >= minima && p <= maxima));

        if (dentroDelRango(declarada))
        {
            return declarada;
        }

        return dentroDelRango(paginaImagen) ? paginaImagen : 0;
    }

    /// <summary>Cita bibliografica cuando el material del lote son paginas escaneadas.</summary>
    private static string DescribirPaginas(List<ImagenExtraida> paginas)
    {
        if (paginas.Count == 0)
        {
            return string.Empty;
        }

        var numeros = paginas.Select(p => p.Pagina).Distinct().OrderBy(n => n).ToList();

        return numeros.Count == 1
            ? $"pag. {numeros[0]} (escaneada)"
            : $"pags. {string.Join(", ", numeros)} (escaneadas)";
    }

    /// <summary>
    /// Ventana de elementos que le toca a este lote, con el mismo criterio que
    /// <see cref="SeleccionarFragmentos"/>: cada lote mira una parte distinta del alcance.
    /// </summary>
    private static List<ImagenExtraida> SeleccionarVentana(
        List<ImagenExtraida> todas, int lote, int totalLotes, int maxPorLote)
    {
        if (todas.Count == 0)
        {
            return new List<ImagenExtraida>();
        }

        if (totalLotes <= 1)
        {
            return todas.Take(maxPorLote).ToList();
        }

        int porLote = Math.Max(1, (int)Math.Ceiling(todas.Count / (double)totalLotes));
        int inicio = Math.Min(lote * porLote, Math.Max(0, todas.Count - 1));
        int cantidad = Math.Min(Math.Min(porLote, maxPorLote), todas.Count - inicio);

        return todas.GetRange(inicio, Math.Max(1, cantidad));
    }

    private static List<ImagenExtraida> TomarImagenes(Queue<ImagenExtraida> cola, int cantidad)
    {
        var lista = new List<ImagenExtraida>();
        while (lista.Count < cantidad && cola.Count > 0)
        {
            lista.Add(cola.Dequeue());
        }

        return lista;
    }

    private static (string mime, string base64)? LeerImagenBase64(ImagenExtraida img)
    {
        try
        {
            if (!File.Exists(img.Ruta))
            {
                return null;
            }

            byte[] originales = File.ReadAllBytes(img.Ruta);

            byte[] bytes;
            string mime;

            if (img.YaPreparada)
            {
                // Las paginas escaneadas ya salieron del extractor con el tamanio justo para
                // que se lea el texto: volver a tocarlas solo les sacaria legibilidad.
                bytes = originales;
                mime = img.MimeType;
            }
            else
            {
                // Se reescala siempre: el Base64 de una figura de 3000 px consume tokens al pedo.
                bytes = ImagenUtil.RedimensionarSiHaceFalta(originales, 1024);
                mime = ReferenceEquals(bytes, originales) ? img.MimeType : "image/png";
            }

            if (bytes.Length > MaxBytesImagen)
            {
                return null;
            }

            return (mime, Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"LeerImagenBase64({img.Ruta})", ex);
            return null;
        }
    }

    private static string NormalizarClave(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (char c in texto.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
