using System.Text.Json.Serialization;

namespace AutoExam.Models;

/// <summary>Historial y estadisticas acumuladas. Se persiste en perfil.json.</summary>
public class PerfilUsuario
{
    public string Nombre { get; set; } = "Estudiante";

    public List<ExamenRendido> Historial { get; set; } = new();

    [JsonIgnore]
    public int TotalExamenes => Historial.Count;

    [JsonIgnore]
    public int TotalPreguntas => Historial.Sum(e => e.TotalPreguntas);

    [JsonIgnore]
    public int TotalCorrectas => Historial.Sum(e => e.Correctas);

    [JsonIgnore]
    public int TotalSalteadas => Historial.Sum(e => e.Salteadas);

    [JsonIgnore]
    public double PromedioNota => Historial.Count == 0 ? 0 : Historial.Average(e => e.NotaUBA);

    [JsonIgnore]
    public double PromedioAciertos => Historial.Count == 0 ? 0 : Historial.Average(e => e.PorcentajeAciertos);

    [JsonIgnore]
    public int Aprobados => Historial.Count(e => e.Aprobado);

    [JsonIgnore]
    public int Aplazos => Historial.Count(e => !e.Aprobado);

    [JsonIgnore]
    public int MejorNota => Historial.Count == 0 ? 0 : Historial.Max(e => e.NotaUBA);
}

/// <summary>Configuracion de la app. Se persiste en config.json.</summary>
public class AppConfig
{
    /// <summary>
    /// Modelo por defecto. Se usa tambien para migrar configuraciones con modelos retirados.
    ///
    /// OJO: Google retiro la familia 1.5 para los proyectos nuevos, asi que en muchas claves
    /// este nombre contesta 404. No es un problema fatal: ante ese 404 la generacion consulta
    /// que modelos habilita la clave, se pasa al flash estable que encuentre y guarda el
    /// cambio (ver GeminiApiService.BuscarModeloVigenteAsync). Si eso pasa, queda anotado en
    /// errores.log y el modelo real se ve en Ajustes.
    /// </summary>
    public const string ModeloPorDefecto = "gemini-1.5-flash";

    /// <summary>
    /// Sugerencias que se muestran si todavia no se detectaron los modelos reales de la clave.
    /// La lista viva se obtiene con el boton "Detectar modelos" de la pestania Ajustes.
    /// </summary>
    public static readonly string[] ModelosSugeridos =
    {
        "gemini-1.5-flash",
        "gemini-3.7-flash",
        "gemini-3.6-flash",
        "gemini-3.5-flash",
        "gemini-3.5-flash-lite",
        "gemini-3.1-flash-lite",
        "gemini-3.1-pro-preview",
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.5-pro"
    };

    /// <summary>
    /// Familias que Google ya retiro: si aparecen en config.json se migran solas.
    ///
    /// "gemini-1.5" NO puede estar en esta lista mientras sea <see cref="ModeloPorDefecto"/>:
    /// la migracion lo reemplazaria por si mismo y mostraria el cartel de "se actualizo el
    /// modelo" en cada arranque, sin cambiar nada.
    /// </summary>
    public static readonly string[] PrefijosRetirados = { "gemini-1.0", "gemini-pro", "gemini-2.0" };

    /// <summary>
    /// Primera clave. Se conserva para no romper los config.json ya escritos y porque es
    /// la que ve el resto de la app; la lista completa vive en <see cref="ApiKeys"/>.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Todas las claves disponibles, en orden de uso. El nivel gratuito limita por clave
    /// (20 generaciones por dia en los flash), asi que tener varias es lo unico que
    /// permite seguir generando cuando una se agota.
    /// </summary>
    public List<string> ApiKeys { get; set; } = new();

    public string Modelo { get; set; } = ModeloPorDefecto;

    /// <summary>
    /// Sube el PDF con la Files API y deja que Gemini lo lea entero, en vez de mandarle el
    /// texto extraido en el propio request. Se usa solo cuando conviene (ver
    /// <c>GeminiApiService.ConvieneSubirElPdf</c>): para un alcance chico con texto
    /// limpio, mandar el texto es mas rapido y mas barato que hacerle leer el PDF.
    /// </summary>
    public bool UsarFilesApi { get; set; } = true;

    public bool TemaOscuro { get; set; } = true;

    /// <summary>Habilita la generacion multimodal (preguntas sobre graficos/esquemas).</summary>
    public bool IncluirImagenes { get; set; } = true;

    /// <summary>
    /// Preguntas por peticion HTTP a Gemini. 15 es el tope: por encima, la respuesta con el
    /// analisis opcion por opcion empieza a arriesgar truncarse.
    /// </summary>
    public int PreguntasPorLote { get; set; } = 15;

    /// <summary>Paginas que se leen por bloque con PdfPig, para no desbordar la RAM.</summary>
    public int PaginasPorBloque { get; set; } = 15;

    /// <summary>Presupuesto de caracteres de contexto por peticion (ventana de Gemini).</summary>
    public int MaxCaracteresContexto { get; set; } = 90_000;

    /// <summary>Tope de imagenes extraidas por examen.</summary>
    public int MaxImagenesPorExamen { get; set; } = 12;

    // ------------------------------------------------------------------
    // Actualizaciones
    // ------------------------------------------------------------------

    /// <summary>
    /// Ultima version que se intento instalar. Junto con <see cref="IntentosDeActualizacion"/>
    /// es lo que permite detectar un paquete publicado que no trae la version que anuncia.
    /// </summary>
    public string UltimaVersionIntentada { get; set; } = string.Empty;

    /// <summary>Cuantas veces se intento instalar <see cref="UltimaVersionIntentada"/>.</summary>
    public int IntentosDeActualizacion { get; set; }

    // ------------------------------------------------------------------
    // Claves
    // ------------------------------------------------------------------

    /// <summary>
    /// Las claves realmente utilizables, sin repetidos y sin vacios. Une el campo viejo
    /// <see cref="ApiKey"/> con la lista nueva, asi un config.json anterior sigue andando
    /// sin migracion explicita.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> ClavesDisponibles
    {
        get
        {
            var salida = new List<string>();

            foreach (string clave in new[] { ApiKey }.Concat(ApiKeys ?? new List<string>()))
            {
                string limpia = (clave ?? string.Empty).Trim();

                if (limpia.Length > 0 && !salida.Contains(limpia, StringComparer.Ordinal))
                {
                    salida.Add(limpia);
                }
            }

            return salida;
        }
    }

    /// <summary>
    /// Reemplaza el juego de claves a partir del texto que escribio el usuario, aceptando
    /// separacion por comas, punto y coma o saltos de linea. La primera queda tambien en
    /// <see cref="ApiKey"/> para que el resto de la app la siga viendo.
    /// </summary>
    public void EstablecerClaves(string texto)
    {
        var claves = SepararClaves(texto);

        ApiKeys = claves.ToList();
        ApiKey = claves.FirstOrDefault() ?? string.Empty;
    }

    /// <summary>Separa un texto en claves. Publico porque la UI lo usa para contarlas mientras se escribe.</summary>
    public static List<string> SepararClaves(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new List<string>();
        }

        var salida = new List<string>();

        foreach (string trozo in texto.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string limpia = trozo.Trim();

            if (limpia.Length > 0 && !salida.Contains(limpia, StringComparer.Ordinal))
            {
                salida.Add(limpia);
            }
        }

        return salida;
    }

    /// <summary>Texto editable con una clave por linea, para mostrar en Ajustes.</summary>
    [JsonIgnore]
    public string ClavesComoTexto => string.Join(Environment.NewLine, ClavesDisponibles);

    // ------------------------------------------------------------------
    // Ventana (US-003)
    // ------------------------------------------------------------------

    /// <summary>-1 = nunca se guardo: MainWindow usa el tamanio/posicion por defecto del XAML.</summary>
    public double VentanaAncho { get; set; } = -1;

    /// <summary>-1 = nunca se guardo: MainWindow usa el tamanio/posicion por defecto del XAML.</summary>
    public double VentanaAlto { get; set; } = -1;

    /// <summary>-1 = nunca se guardo: MainWindow usa el tamanio/posicion por defecto del XAML.</summary>
    public double VentanaX { get; set; } = -1;

    /// <summary>-1 = nunca se guardo: MainWindow usa el tamanio/posicion por defecto del XAML.</summary>
    public double VentanaY { get; set; } = -1;

    public System.Windows.WindowState VentanaEstado { get; set; } = System.Windows.WindowState.Normal;

    // ------------------------------------------------------------------
    // Examen (US-005)
    // ------------------------------------------------------------------

    /// <summary>
    /// Nivel de tamanio de texto al rendir un examen. 0..4, donde 2 (Normal) es el
    /// tamanio de siempre (17pt enunciado / 14pt opciones) para no romper el look
    /// por defecto. El mapeo nivel-&gt;puntos vive en ExamenViewModel, no aca: este
    /// campo solo guarda la preferencia del usuario entre reinicios.
    /// </summary>
    public int TamanioTextoExamen { get; set; } = 2;
}
