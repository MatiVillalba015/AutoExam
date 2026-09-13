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

    /// <summary>
    /// Se conserva por compatibilidad con los config.json ya escritos y porque es lo que
    /// <c>TemaService</c> necesita para elegir el juego de tokens. Desde US-049 lo manda
    /// <see cref="TemaDeColor"/>: el tema "claro" es el unico que lo pone en false, y
    /// <see cref="TemaDeColor"/> se sincroniza con este campo al cargar.
    /// </summary>
    public bool TemaOscuro { get; set; } = true;

    // ------------------------------------------------------------------
    // Apariencia (US-048 / US-049 / US-054)
    // ------------------------------------------------------------------

    /// <summary>
    /// Tema de color general de la app (US-049). Es la clave de una paleta de
    /// <see cref="PaletaDeApp"/>, no un color: RN-57 acota la eleccion a paletas
    /// predefinidas y con contraste verificado, igual que el color por materia (RN-31).
    /// </summary>
    public string TemaDeColor { get; set; } = PaletaDeApp.PorDefecto;

    /// <summary>
    /// Escala de toda la interfaz (US-048). 1.0 = 100%. Los limites viven en
    /// <see cref="ZoomDeLaApp"/>; aca solo se guarda la preferencia entre sesiones.
    /// </summary>
    public double Zoom { get; set; } = ZoomDeLaApp.Normal;

    /// <summary>
    /// Avisos informativos dentro de la ventana (US-050). Nunca afecta errores ni
    /// confirmaciones: eso lo fija RN-58 y lo garantiza que ni <c>IDialogos</c> ni las
    /// InfoBar de error consulten este campo.
    /// </summary>
    public bool Notificaciones { get; set; } = true;

    /// <summary>
    /// "Reducir movimiento" propio de la app (US-054). Se combina con OR con la preferencia
    /// del sistema operativo (RN-62): apagar este toggle no vuelve a encender las animaciones
    /// si Windows ya las pidio reducidas.
    /// </summary>
    public bool ReducirMovimiento { get; set; }

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

    /// <summary>
    /// Separadores admitidos entre claves en el formato viejo (US-042 los deja de usar para
    /// cargar, pero siguen valiendo para leer lo que ya estaba guardado).
    /// </summary>
    private static readonly char[] SeparadoresDeClaves = { ',', ';', '\n', '\r' };

    /// <summary>
    /// Migracion de RN-52: pasa del formato viejo —un solo texto con las claves separadas por
    /// coma— a una clave por entrada, que es lo que US-042 muestra como pestanias.
    ///
    /// Hace falta porque hasta ahora nada garantizaba que lo guardado estuviera separado: una
    /// version anterior escribia en <see cref="ApiKey"/> el texto entero tal como se habia
    /// pegado, comas incluidas. Ese valor compuesto se leia como UNA sola clave, asi que al
    /// abrir las pestanias el usuario habria visto "clave 1" con las tres adentro y el
    /// fallback de cuota no habria tenido con que rotar.
    ///
    /// Es silenciosa y de una sola vez: si no hay ninguna entrada compuesta devuelve false y
    /// no se toca ni se reescribe nada. El orden original se conserva, que es de lo que
    /// depende el orden de fallback (RN-3).
    /// </summary>
    public bool MigrarClavesASeparadas()
    {
        var guardadas = new[] { ApiKey ?? string.Empty }
            .Concat(ApiKeys ?? new List<string>())
            .ToList();

        bool hayCompuestas = guardadas.Any(c => c.IndexOfAny(SeparadoresDeClaves) >= 0);

        if (!hayCompuestas)
        {
            return false;
        }

        var separadas = SepararClaves(string.Join(",", guardadas));

        ApiKeys = separadas;
        ApiKey = separadas.FirstOrDefault() ?? string.Empty;

        return true;
    }

    /// <summary>Separa un texto en claves. Publico porque la UI lo usa para contarlas mientras se escribe.</summary>
    public static List<string> SepararClaves(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new List<string>();
        }

        var salida = new List<string>();

        foreach (string trozo in texto.Split(SeparadoresDeClaves, StringSplitOptions.RemoveEmptyEntries))
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

    /// <summary>
    /// Si ya se le mostro al alumno la referencia de atajos de teclado del examen (US-036).
    ///
    /// El criterio pide mostrarla "la primera vez que se entra a un examen", asi que hace
    /// falta recordar entre reinicios que ya se mostro. Guardarlo aca y no en memoria es lo
    /// que evita que la misma ayuda reaparezca en cada arranque, que es como una ayuda util
    /// se convierte en un estorbo.
    /// </summary>
    public bool AtajosExamenVistos { get; set; }

    // ------------------------------------------------------------------
    // Fuentes-imagen (US-010)
    // ------------------------------------------------------------------

    /// <summary>
    /// Tope de imagenes que se envian por material cuando la fuente es un set de fotos
    /// (apuntes manuscritos). Superado el limite, <c>ImagenExtractor</c> recorta al valor
    /// respetando el orden de alta y lo informa (NFR-43). Default 12, alineado con
    /// <c>OpcionesExtraccion.MaxImagenes</c> y con <see cref="MaxImagenesPorExamen"/>.
    /// </summary>
    public int MaxImagenesPorMaterial { get; set; } = 12;

    // ------------------------------------------------------------------
    // Restaurar valores de fabrica (US-047)
    // ------------------------------------------------------------------

    /// <summary>
    /// Devuelve las preferencias de interfaz a como vienen de fabrica: tema (US-049), zoom
    /// (US-048), notificaciones (US-050), reducir movimiento (US-054) y la geometria de
    /// ventana, que RN-63 pide resetear junto con el resto por ser una preferencia mas
    /// aunque no tenga control visible.
    ///
    /// Lo importante es lo que NO toca, y por eso esto vive en el modelo y no en la pantalla:
    /// las claves de Gemini, el modelo elegido, el historial y los libros quedan intactos
    /// (RN-55). Escrito como una lista explicita de asignaciones y no como <c>= new
    /// AppConfig()</c> justamente por eso: reemplazar el objeto entero borraria las claves,
    /// y el dia que se agregue un campo nuevo el default seria borrarlo tambien.
    /// </summary>
    public void RestaurarValoresDeFabrica()
    {
        var fabrica = new AppConfig();

        TemaDeColor = fabrica.TemaDeColor;
        TemaOscuro = fabrica.TemaOscuro;
        Zoom = fabrica.Zoom;
        Notificaciones = fabrica.Notificaciones;
        ReducirMovimiento = fabrica.ReducirMovimiento;

        VentanaAncho = fabrica.VentanaAncho;
        VentanaAlto = fabrica.VentanaAlto;
        VentanaX = fabrica.VentanaX;
        VentanaY = fabrica.VentanaY;
        VentanaEstado = fabrica.VentanaEstado;
    }
}
