using System.Collections.ObjectModel;
using System.Globalization;
using AutoExam.Behaviors;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>
/// Un tema de color ofrecido en Ajustes (US-049). Envuelve al <see cref="TemaDeApp"/> del
/// catalogo solo para poder marcar cual esta elegido: el catalogo es una lista de records
/// inmutables y no puede notificar cambios a la pantalla.
/// </summary>
public partial class OpcionDeTema : ObservableObject
{
    public OpcionDeTema(TemaDeApp tema)
    {
        Tema = tema;
    }

    public TemaDeApp Tema { get; }

    public string Clave => Tema.Clave;

    public string Nombre => Tema.Nombre;

    /// <summary>
    /// Los dos colores de la muestra. Un tema que usa el acento de sus tokens (los violetas)
    /// no los tiene escritos en el catalogo, asi que la vista previa los trae de aca: es el
    /// unico lugar donde el violeta de marca se repite, y solo para dibujar el rectangulito.
    /// </summary>
    public string ColorDesde => Tema.UsaElAcentoDeLosTokens
        ? (Tema.Oscuro ? "#9B87F5" : "#F2F0F8")
        : Tema.Marca;

    public string ColorHasta => Tema.UsaElAcentoDeLosTokens
        ? (Tema.Oscuro ? "#6246C8" : "#C9C4DC")
        : Tema.MarcaSuave;

    [ObservableProperty]
    private bool _esElActual;
}

/// <summary>
/// Clave, modelo, apariencia, datos y las perillas finas de la generacion.
///
/// Desde US-046..US-056 esta pantalla dejo de ser solo "la clave de Gemini y dos numeros" y
/// pasa a juntar todo lo que se configura de la app. El orden de las tarjetas no es casual:
/// arriba lo que hay que tocar para que la app funcione (clave y modelo), despues lo que se
/// toca por gusto (tema, tamanio, avisos), despues lo que se toca cuando algo pasa (datos,
/// copia de seguridad) y al final, separado y en rojo, lo que no se puede deshacer.
/// </summary>
public partial class AjustesViewModel : PaginaViewModel
{
    private readonly BibliotecaService _biblioteca;
    private readonly SesionUsuarioService _sesion;
    private readonly GeminiApiService _gemini;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _nav;

    public AjustesViewModel(
        BibliotecaService biblioteca, SesionUsuarioService sesion, GeminiApiService gemini,
        IDialogos dialogos, INavegacion nav)
        : base("ajustes", "Ajustes", "Settings24")
    {
        _biblioteca = biblioteca;
        _sesion = sesion;
        _gemini = gemini;
        _dialogos = dialogos;
        _nav = nav;

        // El resumen ("2 claves. Si una agota su cuota...") se recalcula solo mientras se
        // escribe, igual que cuando era un unico campo de texto: ahora el cambio puede venir
        // de cualquiera de las pestanias, asi que lo avisa la coleccion y no un setter.
        Claves.Cambiaron += () => OnPropertyChanged(nameof(ResumenClaves));

        Temas = new ObservableCollection<OpcionDeTema>(
            PaletaDeApp.Temas.Select(t => new OpcionDeTema(t)));

        MarcarTemaElegido();
    }

    public ObservableCollection<string> Modelos { get; } = new();

    public string CarpetaDatos => RutasApp.Raiz;

    /// <summary>Se recalcula al entrar: el espacio en disco cambia sin que Ajustes se entere.</summary>
    public override void AlEntrar() => MedirAlmacenamiento();

    // ------------------------------------------------------------------
    // US-040 — notas de version
    // ------------------------------------------------------------------

    /// <summary>
    /// Todas las versiones con notas, de la mas nueva a la mas vieja. Salen del CHANGELOG.md
    /// embebido en el ejecutable (RN-51): estan disponibles sin conexion, que es justo cuando
    /// se las quiere leer — despues de que la app se actualizo sola.
    /// </summary>
    public IReadOnlyList<NotasDeUnaVersion> Versiones => NotasDeVersion.Todas;

    [ObservableProperty]
    private bool _mostrarNotas;

    /// <summary>
    /// True cuando la version instalada no tiene entrada en el archivo. El criterio pide
    /// decirlo con claridad en vez de dejar la seccion vacia: pasa en un build de prueba
    /// hecho entre dos releases, y una pantalla en blanco ahi se lee como una falla.
    /// </summary>
    public bool FaltanNotasDeEstaVersion => NotasDeVersion.FaltanLasDeLaInstalada;

    public string AvisoSinNotas => NotasDeVersion.AvisoSinNotas;

    /// <summary>Si hay al menos una version con notas para mostrar.</summary>
    public bool HayNotas => Versiones.Count > 0;

    [RelayCommand]
    private void AlternarNotas() => MostrarNotas = !MostrarNotas;

    /// <summary>Version instalada, para saber contra que se compara el manifiesto de GitHub.</summary>
    public string VersionActual => ActualizacionService.VersionActual;

    // ------------------------------------------------------------------
    // US-053 — buscar actualizaciones a mano
    // ------------------------------------------------------------------

    /// <summary>
    /// True mientras se consulta el manifiesto. La pantalla lo usa para el mismo tratamiento
    /// que "Probar conexion" (RN-16): un estado de espera visible y un resultado despues, en
    /// vez de un boton que parece no haber hecho nada.
    /// </summary>
    [ObservableProperty]
    private bool _buscandoActualizacion;

    /// <summary>
    /// Comprobacion a pedido. La automatica del arranque se calla si no hay nada nuevo;
    /// esta contesta siempre, porque el usuario apreto un boton y espera una respuesta.
    ///
    /// El resultado viene por callback en vez de por MessageBox: el criterio pide el mismo
    /// feedback que "Probar conexion", y eso es un estado en la propia pantalla.
    /// </summary>
    [RelayCommand]
    private void BuscarActualizacion()
    {
        BuscandoActualizacion = true;
        Avisar(0, "Buscando actualizaciones...", "Consultando si hay una version mas nueva publicada.");

        ActualizacionService.ComprobarAhora((resultado, mensaje) =>
        {
            BuscandoActualizacion = false;

            switch (resultado)
            {
                case ActualizacionService.ResultadoDeBusqueda.AlDia:
                    Avisar(1, "AutoExam está al día", mensaje);
                    break;

                case ActualizacionService.ResultadoDeBusqueda.HayVersionNueva:
                    Avisar(1, "Hay una versión nueva", mensaje +
                        " Se abrió la ventana de actualización: desde ahí se descarga y se reinicia sola.");
                    break;

                default:
                    Avisar(2, "No se pudo comprobar", mensaje);
                    break;
            }
        });
    }

    /// <summary>
    /// Las claves cargadas, una por pestania (US-042). Es el mismo tipo que usa la pantalla
    /// de configuracion inicial: hasta ahora cada pantalla tenia su propio campo de texto y
    /// el criterio pide que la clave se cargue igual en las dos.
    /// </summary>
    public ClavesDeGemini Claves { get; } = new();

    /// <summary>Cuenta las claves cargadas para que se vea que la rotacion tiene con que trabajar.</summary>
    public string ResumenClaves
    {
        get
        {
            int cuantas = Claves.ComoLista().Count;

            return cuantas switch
            {
                0 => "Sin claves cargadas.",
                1 => "1 clave. Con una sola, al agotarse la cuota diaria hay que esperar al otro dia.",
                _ => $"{cuantas} claves. Si una agota su cuota, AutoExam sigue con la siguiente sin cortar el examen."
            };
        }
    }

    [ObservableProperty]
    private string _modelo = AppConfig.ModeloPorDefecto;

    [ObservableProperty]
    private int _preguntasPorLote = 15;

    [ObservableProperty]
    private int _paginasPorBloque = 15;

    [ObservableProperty]
    private int _maxCaracteres = 90_000;

    [ObservableProperty]
    private int _maxImagenes = 12;

    [ObservableProperty]
    private bool _incluirImagenes = true;

    [ObservableProperty]
    private bool _ocupado;

    [ObservableProperty]
    private string _mensajeTitulo = string.Empty;

    [ObservableProperty]
    private string _mensaje = string.Empty;

    /// <summary>0 = sin mensaje · 1 = ok · 2 = aviso · 3 = error.</summary>
    [ObservableProperty]
    private int _severidad;

    public void CargarDesdeConfig()
    {
        var c = _sesion.Config;

        Claves.Cargar(c.ClavesDisponibles);
        PreguntasPorLote = c.PreguntasPorLote;
        PaginasPorBloque = c.PaginasPorBloque;
        MaxCaracteres = c.MaxCaracteresContexto;
        MaxImagenes = c.MaxImagenesPorExamen;
        IncluirImagenes = c.IncluirImagenes;

        // Las tres de apariencia se asignan con _sinGuardar puesto: los setters de estas
        // propiedades guardan y aplican al vuelo (nadie espera apretar "Guardar" despues de
        // cambiar de tema), y sin esta guarda cargar la config volveria a escribirla en disco
        // en cada arranque.
        _sinGuardar = true;
        TemaElegido = c.TemaDeColor;
        Zoom = ZoomDeLaApp.Acotar(c.Zoom);
        Notificaciones = c.Notificaciones;
        ReducirMovimiento = c.ReducirMovimiento;
        _sinGuardar = false;

        MarcarTemaElegido();
        PoblarModelos(AppConfig.ModelosSugeridos, c.Modelo);
        MedirAlmacenamiento();
    }

    /// <summary>
    /// True mientras se estan volcando valores desde config.json a las propiedades. Los
    /// setters de apariencia guardan solos, y sin esta bandera cargar seria escribir.
    /// </summary>
    private bool _sinGuardar;

    public void PoblarModelos(IEnumerable<string> modelos, string elegido)
    {
        var lista = modelos.ToList();

        if (!string.IsNullOrWhiteSpace(elegido) && !lista.Contains(elegido, StringComparer.OrdinalIgnoreCase))
        {
            lista.Insert(0, elegido);
        }

        Modelos.Clear();
        foreach (var m in lista)
        {
            Modelos.Add(m);
        }

        Modelo = elegido;
    }

    // ------------------------------------------------------------------
    // US-049 — tema de color de la app
    // ------------------------------------------------------------------

    public ObservableCollection<OpcionDeTema> Temas { get; }

    /// <summary>Clave del tema elegido. Cambiarla aplica y guarda: un tema no se "confirma".</summary>
    [ObservableProperty]
    private string _temaElegido = PaletaDeApp.PorDefecto;

    partial void OnTemaElegidoChanged(string value)
    {
        MarcarTemaElegido();

        if (_sinGuardar)
        {
            return;
        }

        var tema = PaletaDeApp.Resolver(value);

        TemaService.Aplicar(tema.Clave);

        _sesion.Config.TemaDeColor = tema.Clave;

        // TemaOscuro se mantiene en sincronia porque es lo que leen los config.json viejos y
        // el arranque de una version anterior: si alguien vuelve atras, la app abre al menos
        // con el fondo correcto en vez de en oscuro sobre una preferencia clara.
        _sesion.Config.TemaOscuro = tema.Oscuro;
        _sesion.GuardarConfig();
    }

    /// <summary>Elegir un tema es un click sobre su muestra, no un desplegable.</summary>
    [RelayCommand]
    private void ElegirTema(string? clave)
    {
        if (!string.IsNullOrWhiteSpace(clave))
        {
            TemaElegido = clave;
        }
    }

    private void MarcarTemaElegido()
    {
        foreach (var opcion in Temas)
        {
            opcion.EsElActual = string.Equals(opcion.Clave, TemaElegido, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------
    // US-048 — tamanio de la interfaz
    // ------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomTexto))]
    [NotifyCanExecuteChangedFor(nameof(AumentarZoomCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReducirZoomCommand))]
    private double _zoom = ZoomDeLaApp.Normal;

    /// <summary>El porcentaje al lado del control. Siempre visible, lo pide el criterio.</summary>
    public string ZoomTexto => ZoomDeLaApp.ComoPorcentaje(Zoom);

    public double ZoomMinimo => ZoomDeLaApp.Minimo;

    public double ZoomMaximo => ZoomDeLaApp.Maximo;

    public double ZoomPaso => ZoomDeLaApp.Paso;

    partial void OnZoomChanged(double value)
    {
        double acotado = ZoomDeLaApp.Acotar(value);

        if (Math.Abs(acotado - value) > 0.001)
        {
            // El slider puede entregar cualquier valor intermedio; se lo devuelve acotado y
            // redondeado al paso, y esta misma vuelta se encarga de aplicar y guardar.
            Zoom = acotado;
            return;
        }

        _nav.AplicarZoom(acotado);

        if (_sinGuardar)
        {
            return;
        }

        _sesion.Config.Zoom = acotado;
        _sesion.GuardarConfig();
    }

    /// <summary>
    /// En el limite el boton se apaga en vez de dejar seguir. El criterio admite deshabilitar
    /// o avisar; apagarlo dice lo mismo sin un cartel que hay que cerrar.
    /// </summary>
    private bool PuedeAumentarZoom() => ZoomDeLaApp.PuedeAumentar(Zoom);

    private bool PuedeReducirZoom() => ZoomDeLaApp.PuedeReducir(Zoom);

    [RelayCommand(CanExecute = nameof(PuedeAumentarZoom))]
    private void AumentarZoom() => Zoom = ZoomDeLaApp.Aumentar(Zoom);

    [RelayCommand(CanExecute = nameof(PuedeReducirZoom))]
    private void ReducirZoom() => Zoom = ZoomDeLaApp.Reducir(Zoom);

    // ------------------------------------------------------------------
    // US-050 — notificaciones · US-054 — reducir movimiento
    // ------------------------------------------------------------------

    [ObservableProperty]
    private bool _notificaciones = true;

    partial void OnNotificacionesChanged(bool value)
    {
        if (_sinGuardar)
        {
            return;
        }

        _sesion.Config.Notificaciones = value;
        _sesion.GuardarConfig();
    }

    [ObservableProperty]
    private bool _reducirMovimiento;

    partial void OnReducirMovimientoChanged(bool value)
    {
        Animaciones.Aplicar(value);

        if (_sinGuardar)
        {
            return;
        }

        _sesion.Config.ReducirMovimiento = value;
        _sesion.GuardarConfig();
    }

    /// <summary>
    /// True cuando Windows ya pide movimiento reducido. La pantalla lo dice al lado del
    /// toggle: sin eso, alguien con la preferencia del sistema activada ve el toggle apagado
    /// y las animaciones igual apagadas, que es exactamente la contradiccion que el ultimo
    /// criterio de US-054 pide evitar.
    /// </summary>
    public bool MovimientoReducidoPorElSistema => Animaciones.PorElSistema;

    public string AvisoDeMovimiento => MovimientoReducidoPorElSistema
        ? "Windows ya pide reducir el movimiento, así que las animaciones están apagadas aunque este control esté en no."
        : string.Empty;

    // ------------------------------------------------------------------
    // US-046 — datos y almacenamiento
    // ------------------------------------------------------------------

    /// <summary>Los tres grupos medidos, en el orden en que se muestran: libros, historial, temporales.</summary>
    public ObservableCollection<UsoDeDisco> Usos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAlgoQueVaciar))]
    private long _bytesTemporales;

    [ObservableProperty]
    private string _totalEnUso = string.Empty;

    /// <summary>
    /// True si alguno de los tres grupos no se pudo medir. La pantalla lo dice en vez de
    /// mostrar un cero que se lee como "no ocupa nada".
    /// </summary>
    [ObservableProperty]
    private bool _hayMedicionIncompleta;

    /// <summary>"Vaciar cache" se apaga cuando no hay nada temporal: no promete lo que no puede dar.</summary>
    public bool HayAlgoQueVaciar => BytesTemporales > 0;

    /// <summary>
    /// Las tres fracciones de la barra segmentada, de 0 a 1. Se calculan aca y no en un
    /// converter porque dependen del total de los tres juntos, y un converter solo ve un
    /// valor por vez.
    /// </summary>
    [ObservableProperty]
    private double _fraccionLibros;

    [ObservableProperty]
    private double _fraccionHistorial;

    [ObservableProperty]
    private double _fraccionTemporales;

    public void MedirAlmacenamiento()
    {
        var medidos = AlmacenamientoService.Medir(_sesion.Perfil.Historial.Select(e => e.Id));

        Usos.Clear();
        foreach (var uso in medidos)
        {
            Usos.Add(uso);
        }

        long total = medidos.Where(u => u.Medido).Sum(u => u.Bytes);

        BytesTemporales = medidos.Count > 2 && medidos[2].Medido ? medidos[2].Bytes : 0;
        HayMedicionIncompleta = medidos.Any(u => !u.Medido);
        TotalEnUso = total > 0 ? $"{AlmacenamientoService.Legible(total)} en uso" : "sin datos guardados todavía";

        // Con total cero las tres fracciones quedan en cero y la barra se ve vacia, que es lo
        // correcto: no hay nada que repartir. Dividir igual daria NaN y el ancho se rompe.
        FraccionLibros = Fraccion(medidos.ElementAtOrDefault(0), total);
        FraccionHistorial = Fraccion(medidos.ElementAtOrDefault(1), total);
        FraccionTemporales = Fraccion(medidos.ElementAtOrDefault(2), total);
    }

    private static double Fraccion(UsoDeDisco? uso, long total) =>
        uso is null || !uso.Medido || total <= 0 ? 0 : Math.Clamp(uso.Bytes / (double)total, 0, 1);

    [RelayCommand]
    private void VaciarCache()
    {
        // RN-6: confirmacion explicita, y diciendo exactamente que se borra y que no. Lo
        // segundo importa mas que lo primero: "vaciar cache" suena a que se puede llevar
        // puesto el material.
        bool sigue = _dialogos.Confirmar(
            $"Se van a borrar {AlmacenamientoService.Legible(BytesTemporales)} de archivos temporales.\n\n" +
            "Tus libros, tus exámenes del historial y tu configuración no se tocan.\n\n¿Vaciar la caché?",
            "Vaciar caché");

        if (!sigue)
        {
            return;
        }

        long liberados = AlmacenamientoService.VaciarCache(_sesion.Perfil.Historial.Select(e => e.Id));

        MedirAlmacenamiento();

        Avisar(1, "Caché vacía", $"Se liberaron {AlmacenamientoService.Legible(liberados)}.");
        _nav.Estado($"Se liberaron {AlmacenamientoService.Legible(liberados)} de archivos temporales.");
    }

    // ------------------------------------------------------------------
    // US-051 — copia de seguridad
    // ------------------------------------------------------------------

    [ObservableProperty]
    private bool _copiando;

    [RelayCommand]
    private async Task ExportarCopiaAsync()
    {
        string? destino = _dialogos.ElegirDondeGuardarCopia(CopiaDeSeguridadService.NombreSugerido());

        if (string.IsNullOrWhiteSpace(destino))
        {
            return;
        }

        Copiando = true;
        Avisar(0, "Armando la copia...", "Juntando tus libros, tu historial y tu configuración en un solo archivo.");

        try
        {
            int libros = _biblioteca.Libros.Count;
            int examenes = _sesion.Perfil.Historial.Count;

            // Fuera del hilo de UI: comprimir una biblioteca de varios cientos de MB deja la
            // ventana congelada si se hace aca.
            long tamanio = await Task.Run(() => CopiaDeSeguridadService.Exportar(destino!, libros, examenes));

            Avisar(1, "Copia guardada",
                $"{libros} libro(s) y {examenes} examen(es), {AlmacenamientoService.Legible(tamanio)}.\n{destino}");

            _nav.Estado("Copia de seguridad guardada.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("ExportarCopia", ex);
            Avisar(3, "No se pudo guardar la copia", ex.Message);
        }
        finally
        {
            Copiando = false;
        }
    }

    [RelayCommand]
    private async Task ImportarCopiaAsync()
    {
        string? origen = _dialogos.ElegirCopiaDeSeguridad();

        if (string.IsNullOrWhiteSpace(origen))
        {
            return;
        }

        ResumenDeCopia resumen;

        try
        {
            // Se inspecciona ANTES de preguntar: si el archivo no sirve, el usuario se entera
            // sin haber confirmado un reemplazo, y nada de lo suyo se toco.
            resumen = CopiaDeSeguridadService.Inspeccionar(origen!);
        }
        catch (CopiaInvalidaException ex)
        {
            Avisar(3, "Ese archivo no es una copia de AutoExam", ex.Message + "\n\nNo se modificó ningún dato tuyo.");
            return;
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("InspeccionarCopia", ex);
            Avisar(3, "No se pudo leer la copia", ex.Message + "\n\nNo se modificó ningún dato tuyo.");
            return;
        }

        // RN-6, y con el reemplazo dicho con todas las letras: importar no suma a lo que hay,
        // lo pisa.
        bool sigue = _dialogos.Confirmar(
            $"La copia es del {resumen.Fecha.ToString("d 'de' MMMM 'de' yyyy", CultureInfo.CurrentCulture)} " +
            $"(AutoExam {resumen.Version}) y trae {resumen.Libros} libro(s) y {resumen.Examenes} examen(es).\n\n" +
            "Importarla REEMPLAZA todos tus libros, tu historial y tu configuración actuales. " +
            "Lo que tengas ahora se pierde.\n\n¿Importar la copia?",
            "Importar copia de seguridad");

        if (!sigue)
        {
            return;
        }

        Copiando = true;
        Avisar(0, "Restaurando la copia...", "Reemplazando tus datos por los del archivo.");

        try
        {
            await Task.Run(() => CopiaDeSeguridadService.Importar(origen!));

            _nav.RecargarDatos();

            Avisar(1, "Copia restaurada",
                $"{resumen.Libros} libro(s) y {resumen.Examenes} examen(es) volvieron a su lugar.");

            _nav.Estado("Copia de seguridad restaurada.");
        }
        catch (CopiaInvalidaException ex)
        {
            Avisar(3, "La copia está dañada", ex.Message);
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("ImportarCopia", ex);
            Avisar(3, "No se pudo importar la copia", ex.Message);
        }
        finally
        {
            Copiando = false;
        }
    }

    // ------------------------------------------------------------------
    // US-047 — restaurar valores de fabrica
    // ------------------------------------------------------------------

    [RelayCommand]
    private void RestaurarFabrica()
    {
        // RN-6 otra vez, y de nuevo lo que mas importa es lo que NO se borra: sin esa linea,
        // "restaurar valores de fabrica" se lee como "empezar de cero", que es justo lo que
        // esta accion no hace.
        bool sigue = _dialogos.Confirmar(
            "Se van a restablecer el tema, el tamaño de la interfaz, las notificaciones, " +
            "reducir movimiento y los colores de tus materias.\n\n" +
            "Tus libros, tu historial y tus claves de Gemini NO se tocan.\n\n" +
            "¿Restaurar los valores de fábrica?",
            "Restaurar valores de fábrica");

        if (!sigue)
        {
            return;
        }

        _sesion.Config.RestaurarValoresDeFabrica();
        _sesion.GuardarConfig();

        int materias = _biblioteca.RestaurarColoresDeMaterias();

        // Se recargan las propiedades desde la config recien reseteada, con la guarda puesta
        // para no volver a escribir en disco lo que se acaba de guardar.
        _sinGuardar = true;
        TemaElegido = _sesion.Config.TemaDeColor;
        Zoom = _sesion.Config.Zoom;
        Notificaciones = _sesion.Config.Notificaciones;
        ReducirMovimiento = _sesion.Config.ReducirMovimiento;
        _sinGuardar = false;

        TemaService.Aplicar(_sesion.Config.TemaDeColor);
        Animaciones.Aplicar(_sesion.Config.ReducirMovimiento);
        _nav.AplicarZoom(_sesion.Config.Zoom);

        Avisar(1, "Valores de fábrica restaurados",
            materias > 0
                ? $"Se restableció la apariencia y el color de {materias} materia(s). Tus libros, tu historial y tus claves siguen intactos."
                : "Se restableció la apariencia. Tus libros, tu historial y tus claves siguen intactos.");

        _nav.Estado("Se restauraron los valores de fábrica.");
    }

    // ------------------------------------------------------------------
    // Guardar, detectar y probar
    // ------------------------------------------------------------------

    [RelayCommand]
    private void Guardar()
    {
        var c = _sesion.Config;

        c.EstablecerClaves(Claves.ComoTexto());
        c.Modelo = string.IsNullOrWhiteSpace(Modelo) ? AppConfig.ModeloPorDefecto : Modelo.Trim();
        c.PreguntasPorLote = Math.Clamp(PreguntasPorLote, 5, 15);
        c.PaginasPorBloque = Math.Clamp(PaginasPorBloque, 5, 40);
        c.MaxCaracteresContexto = Math.Clamp(MaxCaracteres, 10_000, 300_000);
        c.MaxImagenesPorExamen = Math.Clamp(MaxImagenes, 0, 30);
        c.IncluirImagenes = IncluirImagenes;

        _sesion.GuardarConfig();
        _nav.RefrescarEstadoApi();

        Avisar(1, "Ajustes guardados", RutasApp.ArchivoConfig);
        _nav.Estado("Ajustes guardados.");
    }

    /// <summary>
    /// Trae de Google la lista real de modelos habilitados para esta clave. Es la
    /// salida cuando Google retira una generacion y la app deja de generar examenes.
    /// </summary>
    [RelayCommand]
    private async Task DetectarAsync()
    {
        string clave = PrimeraClave();

        if (string.IsNullOrWhiteSpace(clave))
        {
            Avisar(2, "Falta la API Key", "Pega primero tu clave de Gemini.");
            return;
        }

        Ocupado = true;
        Avisar(0, "Consultando a Google...", "Pidiendo los modelos habilitados para tu clave.");

        try
        {
            var modelos = await _gemini.ListarModelosAsync(clave);

            if (modelos.Count == 0)
            {
                Avisar(2, "Sin modelos de texto",
                    "La clave es valida pero no expone ningun modelo con generateContent. " +
                    "Revisa en Google AI Studio que el proyecto tenga la API habilitada.");
                return;
            }

            string actual = Modelo.Trim();
            bool sigueVivo = modelos.Contains(actual, StringComparer.OrdinalIgnoreCase);
            string elegido = sigueVivo ? actual : ElegirRecomendado(modelos);

            PoblarModelos(modelos, elegido);

            _sesion.Config.Modelo = elegido;
            _sesion.GuardarConfig();
            _nav.RefrescarEstadoApi();

            Avisar(1, $"{modelos.Count} modelos disponibles",
                sigueVivo
                    ? $"Lista actualizada. Modelo en uso: {elegido}."
                    : $"El modelo anterior ya no existe. Se selecciono y guardo: {elegido}.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("DetectarModelos", ex);
            Avisar(3, "No se pudieron listar los modelos", ex.Message);
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// La primera clave del campo. "Detectar modelos" y "Probar conexion" hablan de una
    /// clave concreta, no del juego entero: consultarlas todas gastaria cuota de cada una
    /// para responder la misma pregunta.
    /// </summary>
    private string PrimeraClave() => Claves.Primera();

    /// <summary>
    /// Prefiere un flash estable. Delega en el servicio para que la eleccion de modelo sea
    /// una sola regla: la de Ajustes y la del rescate automatico ante un 404 tienen que
    /// terminar en el mismo modelo, o el usuario veria uno en pantalla y otro generando.
    /// </summary>
    public static string ElegirRecomendado(List<string> modelos) => GeminiApiService.ElegirFlash(modelos);

    /// <summary>
    /// Numero de la prueba de conexion en curso. Existe para US-019: si el usuario toca
    /// "Probar conexion" dos veces seguidas, la primera respuesta puede llegar despues de la
    /// segunda y dejar en pantalla un resultado que ya no corresponde al ultimo intento. Cada
    /// prueba se lleva su numero y solo escribe si sigue siendo la ultima.
    /// </summary>
    private int _pruebaEnCurso;

    [RelayCommand]
    private async Task ProbarAsync()
    {
        string clave = PrimeraClave();
        string modelo = string.IsNullOrWhiteSpace(Modelo) ? AppConfig.ModeloPorDefecto : Modelo.Trim();

        int miPrueba = ++_pruebaEnCurso;

        Ocupado = true;
        Avisar(0, "Probando la conexion...", $"Consultando {modelo}. Esto puede tardar unos segundos.");

        try
        {
            var (ok, mensaje) = await _gemini.ProbarConexionAsync(clave, modelo);

            if (miPrueba != _pruebaEnCurso)
            {
                // Llego tarde: ya hay otra prueba mas nueva mandando en pantalla.
                return;
            }

            if (ok)
            {
                Avisar(1, "Conexion exitosa", mensaje);
            }
            else
            {
                // El mensaje del servicio es exacto pero tecnico. El titulo dice en dos palabras
                // que paso —que es lo que el usuario necesita para saber si la clave sirve— y el
                // detalle queda abajo para cuando haga falta.
                var (titulo, motivo) = ClasificarFalla(mensaje);
                Avisar(3, titulo, motivo);
            }
        }
        finally
        {
            if (miPrueba == _pruebaEnCurso)
            {
                Ocupado = false;
            }
        }
    }

    /// <summary>
    /// Traduce el mensaje tecnico de una prueba fallida a un titular en lenguaje simple
    /// (US-019 / RN-16). Publica y estatica para poder probar la clasificacion sin levantar la
    /// pantalla de Ajustes ni tocar la red.
    /// </summary>
    /// <returns>El titulo corto y el detalle que se muestra debajo.</returns>
    public static (string Titulo, string Motivo) ClasificarFalla(string? mensaje)
    {
        string m = mensaje ?? string.Empty;

        bool Dice(params string[] fragmentos) =>
            fragmentos.Any(f => m.Contains(f, StringComparison.OrdinalIgnoreCase));

        // El orden importa: se va de la causa mas concreta a la mas general, porque varios de
        // estos mensajes comparten palabras.
        if (Dice("Falta la API Key", "No hay API Key"))
        {
            return ("Falta la clave", "Pega tu clave de Google Gemini en el campo de arriba y volve a probar.");
        }

        if (Dice("API Key rechazada", "API key not valid", "(401)", "(403)"))
        {
            return ("Clave invalida",
                "Google no acepto esta clave. Revisa que este completa y que el proyecto de " +
                "Google AI Studio tenga habilitada la Generative Language API.\n\n" + m);
        }

        if (Dice("cuota DIARIA"))
        {
            return ("Cuota agotada",
                "Se termino la cuota gratuita del dia para esta clave. Se renueva maniana, o " +
                "podes agregar una segunda clave y AutoExam va a rotar sola.\n\n" + m);
        }

        if (Dice("cuota por minuto", "(429)"))
        {
            return ("Demasiados pedidos seguidos",
                "La clave funciona, pero se paso del limite por minuto. Espera un momento y " +
                "volve a probar.\n\n" + m);
        }

        if (Dice("El modelo no existe", "(404)"))
        {
            return ("Modelo no disponible",
                "La clave anda, pero ese modelo no esta habilitado para ella. Toca \"Detectar\" " +
                "para traer la lista real de modelos que podes usar.\n\n" + m);
        }

        if (Dice("No se pudo contactar", "timeout", "no respondio a tiempo"))
        {
            return ("Sin conexion a internet",
                "No se pudo llegar a los servidores de Google. Revisa tu conexion y volve a probar.\n\n" + m);
        }

        if (Dice("filtros de contenido", "bloqueo"))
        {
            return ("Pedido bloqueado",
                "Google bloqueo hasta un pedido de prueba trivial con esta clave.\n\n" + m);
        }

        return ("No se pudo conectar", m);
    }

    [RelayCommand]
    private void AbrirCarpeta()
    {
        try
        {
            _dialogos.AbrirCarpeta(RutasApp.Raiz);
        }
        catch (Exception ex)
        {
            Avisar(3, "No se pudo abrir la carpeta", ex.Message);
        }
    }

    private void Avisar(int severidad, string titulo, string mensaje)
    {
        Severidad = severidad;
        MensajeTitulo = titulo;
        Mensaje = mensaje;
    }
}
