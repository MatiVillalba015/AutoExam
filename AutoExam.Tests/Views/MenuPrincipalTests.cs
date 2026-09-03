using System.IO;
using System.Reflection;
using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;
using AutoExam.Views;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-031 — menú principal con accesos a las acciones más usadas y resumen de actividad.
///
/// El spec pedía los 4 botones de navegación MÁS 4 accesos directos, pero tres de esos
/// cuatro atajos ("generar examen", "ver exámenes anteriores", "ajustes") llevan exactamente
/// a la misma pantalla que el botón de navegación homónimo, y el último criterio de la misma
/// historia pide que "no quede duplicado ni confuso". Resuelto con el usuario a favor de las
/// acciones: las cuatro tarjetas del menú SON las acciones, y la navegación por sección sigue
/// disponible en la barra de arriba y en Ctrl+1..5.
///
/// Estos tests fijan esa decisión y los criterios que sí son verificables sin mirar la
/// pantalla. La parte que no cubren —si se ve bien— hay que mirarla.
/// </summary>
public class MenuPrincipalTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    private static InicioViewModel Menu() => new(Array.Empty<AccesoDeInicio>());

    private static ExamenRendido Rendido(string titulo, int nota, DateTime fecha, string materia = "Fisiologia") =>
        new()
        {
            LibroTitulo = titulo,
            Materia = materia,
            Fecha = fecha,
            NotaUBA = nota,
            Aprobado = nota >= 4,
            TotalPreguntas = 10,
            Correctas = nota,
        };

    // ------------------------------------------------------------------
    // AC — las cuatro acciones existen y son atajos, no lógica nueva
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("AtajoGenerarExamenCommand")]
    [InlineData("AtajoSubirMaterialCommand")]
    [InlineData("AtajoVerHistorialCommand")]
    [InlineData("AtajoAjustesCommand")]
    public void ElMenu_TieneUnAtajoPorCadaAccionPedida_US031(string comando)
    {
        bool existe = typeof(ShellViewModel)
            .GetProperty(comando, BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.True(existe, $"Falta el atajo \"{comando}\" que pide US-031.");
    }

    [Fact]
    public void CadaAtajo_DelegaEnUnFlujoQueYaExiste_RN36()
    {
        // RN-36: "son atajos de navegación a pantallas/flujos ya existentes: no crean lógica
        // de negocio nueva ni una copia paralela de esas pantallas". Si alguno empezara a
        // resolver algo por su cuenta, la pantalla real y el atajo se separarían en silencio.
        string shell = Fuente("AutoExam/ViewModels/ShellViewModel.cs");

        // Generar examen: deja el asistente en el paso Material y navega. El reseteo vive en
        // el asistente, no acá.
        Assert.Contains("Asistente.EmpezarDesdeCero();", shell, StringComparison.Ordinal);
        Assert.Contains("IrA(Asistente.Clave);", shell, StringComparison.Ordinal);

        // Subir material: reusa el mismo comando que usa Biblioteca para abrir el selector.
        Assert.Contains("Libros.ElegirArchivoCommand", shell, StringComparison.Ordinal);

        Assert.Contains("IrA(Historial.Clave)", shell, StringComparison.Ordinal);
        Assert.Contains("IrA(Ajustes.Clave)", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void SubirMaterial_NavegaAntesDeAbrirElSelector()
    {
        // El criterio pide llegar al alta "sin pasos intermedios extra", pero el archivo que
        // se elija aparece en la lista de Biblioteca: si el diálogo se abriera con el menú
        // todavía en pantalla, el alta ocurriría sin que se vea nada.
        string shell = Fuente("AutoExam/ViewModels/ShellViewModel.cs");

        int navega = shell.IndexOf("IrA(Libros.Clave);", StringComparison.Ordinal);
        int abre = shell.IndexOf("Libros.ElegirArchivoCommand", StringComparison.Ordinal);

        Assert.True(navega >= 0 && abre > navega,
            "El atajo abre el selector de archivos antes de navegar a Biblioteca.");
    }

    [Fact]
    public void GenerarExamen_DejaAlAsistenteEnElPasoMaterial()
    {
        // El asistente conserva su estado entre visitas: sin este reseteo, quien lo dejó en
        // Formato y volvió al menú caería de nuevo en Formato, y el criterio pide el paso
        // Material.
        string asistente = Fuente("AutoExam/ViewModels/AsistenteViewModel.cs");

        int inicio = asistente.IndexOf("public void EmpezarDesdeCero()", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "AsistenteViewModel no expone EmpezarDesdeCero.");

        string cuerpo = asistente.Substring(inicio, 220);

        Assert.Contains("ModoRepaso = false", cuerpo, StringComparison.Ordinal);
        Assert.Contains("Paso = PrimerPaso", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public void LasTarjetasDelMenu_LlevanSuPropioComando_NoLaNavegacionGenerica()
    {
        // Que cada acceso traiga su comando es lo que permite que "subir material" haga algo
        // distinto de navegar sin que la vista tenga que conocer ninguna página.
        var boton = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 (e.Attribute("Style")?.Value ?? string.Empty).Contains("TarjetaAcceso"));

        Assert.True(boton is not null, "No se encontró la tarjeta de acceso del menú.");
        Assert.Contains("Comando", boton!.Attribute("Command")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ElMenu_NoDuplicaLosCuatroAccesosConCuatroBotonesDeNavegacion_US031()
    {
        // Ésta es la guarda de la decisión: si alguien volviera a agregar la fila de botones
        // de navegación al lado de las acciones, tres pares llevarían al mismo lugar. La
        // navegación por sección vive en la barra de MainWindow y en Ctrl+1..5.
        var listas = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .Select(e => e.Attribute("ItemsSource")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();

        Assert.DoesNotContain(listas, v => v.Contains("Paginas", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // AC — resumen de actividad reciente
    // ------------------------------------------------------------------

    [Fact]
    public void ConExamenesRendidos_ElMenuMuestraLosUltimos()
    {
        var menu = Menu();

        menu.Actualizar(2, new[]
        {
            Rendido("Tp1", 5, new DateTime(2026, 3, 1)),
            Rendido("Tp2", 8, new DateTime(2026, 3, 10)),
            Rendido("Tp3", 9, new DateTime(2026, 3, 20)),
            Rendido("Tp4", 4, new DateTime(2026, 3, 25)),
        });

        Assert.True(menu.HayActividad);
        Assert.False(menu.HaySinActividad);

        // Los más nuevos primero, y sólo tres: el resumen no es un segundo historial.
        Assert.Equal(3, menu.Actividad.Count);
        Assert.Equal(new[] { "Tp4", "Tp3", "Tp2" }, menu.Actividad.Select(a => a.Titulo));
    }

    [Fact]
    public void CadaLineaDelResumen_LlevaLaNotaYElColorDeSuMateria_RN34()
    {
        PaletaMaterias.Registrar(new[] { new Materia { Nombre = "Bioquimica", Color = "#3EB4C9" } });

        var menu = Menu();
        menu.Actualizar(1, new[] { Rendido("Parcial", 7, DateTime.Now, "Bioquimica") });

        var linea = Assert.Single(menu.Actividad);

        Assert.Equal("7", linea.Nota);
        Assert.True(linea.Aprobado);
        // RN-34: el color sale del mismo lugar que el de Historial y Biblioteca, no de un
        // esquema propio del menú.
        Assert.Equal(PaletaMaterias.ColorDe("Bioquimica"), linea.ColorMateria);
    }

    [Fact]
    public void ElResumen_EsDeSoloLectura_RN37()
    {
        // RN-37: "no permite corregir ni interactuar con el examen/material mostrado desde
        // ahí, sólo lleva a la pantalla correspondiente". La línea copia los datos que
        // muestra en vez de exponer el ExamenRendido, así que no hay ningún binding que pueda
        // terminar escribiendo sobre el historial.
        var escribibles = typeof(ActividadReciente)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(escribibles);

        Assert.Null(typeof(ActividadReciente)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.PropertyType == typeof(ExamenRendido)));
    }

    [Fact]
    public void ElResumen_SoloNavega_NoOfreceNingunaOtraAccion_RN37()
    {
        var comandos = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .Where(e => e.Ancestors().Any(a =>
                (a.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("Actividad")))
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();

        Assert.NotEmpty(comandos);
        Assert.All(comandos, c =>
            Assert.Contains("AtajoVerHistorialCommand", c, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // AC — el primer día no se ve roto
    // ------------------------------------------------------------------

    [Fact]
    public void SinNadaTodavia_ElMenuInvitaALaPrimeraAccion_EnVezDeQuedarVacio()
    {
        var menu = Menu();
        menu.Actualizar(0, Array.Empty<ExamenRendido>());

        Assert.False(menu.HayActividad);
        Assert.True(menu.HaySinActividad);
        Assert.NotEqual(string.Empty, menu.Invitacion);
        Assert.Contains("material", menu.Invitacion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConMaterialPeroSinExamenes_LaInvitacionCambia()
    {
        // Decirle "subí tu primer material" a quien ya subió tres es peor que no decir nada:
        // parece que la app no vio lo que hizo.
        var menu = Menu();
        menu.Actualizar(3, Array.Empty<ExamenRendido>());

        Assert.Contains("examen", menu.Invitacion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("primer material", menu.Invitacion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LaInvitacionYLaListaNoSeMuestranALaVez()
    {
        var doc = Vista("AutoExam/Views/InicioView.xaml");

        var lista = doc.Descendants().FirstOrDefault(e =>
            (e.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("Actividad"));

        var invitacion = doc.Descendants().FirstOrDefault(e =>
            (e.Attribute("Text")?.Value ?? string.Empty).Contains("Invitacion"));

        Assert.True(lista is not null && invitacion is not null);

        Assert.Contains("HayActividad", lista!.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
        Assert.Contains("HaySinActividad", invitacion!.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // US-031 — "¿Qué es AutoExam?"
    // ------------------------------------------------------------------

    [Fact]
    public void ElMenu_TieneUnAccesoQueExplicaQueEsLaApp_US031()
    {
        var comandos = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty);

        Assert.Contains(comandos, c => c.Contains("AlternarQueEsCommand", StringComparison.Ordinal));

        // Y el texto que se abre sale del ViewModel, no está escrito en la vista.
        Assert.Contains("Explicacion", Fuente("AutoExam/Views/InicioView.xaml"), StringComparison.Ordinal);
    }

    [Fact]
    public void LaExplicacion_EsTextoFijoYNoPasaPorGemini_RN39()
    {
        // RN-39: "es fijo y se define una sola vez; no depende de conexión a Gemini ni se
        // genera dinámicamente". Quien todavía no entiende qué hace la app es justamente quien
        // puede no tener la clave cargada: una explicación que a veces aparece y a veces no es
        // peor que ninguna.
        Assert.False(string.IsNullOrWhiteSpace(InicioViewModel.QueEsAutoExam));

        // Es una constante de compilación: por construcción no puede salir de una llamada a
        // nada, ni de Gemini ni de un archivo. Es la forma más fuerte de fijar RN-39.
        var campo = typeof(InicioViewModel).GetField(
            nameof(InicioViewModel.QueEsAutoExam),
            BindingFlags.Public | BindingFlags.Static);

        Assert.True(campo is not null, "QueEsAutoExam dejó de ser un miembro estático público.");
        Assert.True(campo!.IsLiteral && !campo.IsInitOnly,
            "QueEsAutoExam dejó de ser const: podría pasar a calcularse en tiempo de ejecución (RN-39).");

        // Y el menú no conoce ningún servicio del que pudiera pedirla.
        var dependencias = typeof(InicioViewModel)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ToList();

        Assert.DoesNotContain(dependencias, d => d.Contains("Gemini", StringComparison.Ordinal));
    }

    [Fact]
    public void LaExplicacion_HablaDeLoQueElAlumnoHace_NoDeLaArquitectura()
    {
        // "En lenguaje simple orientado a un estudiante nuevo". Sin esto es fácil que termine
        // describiendo el pipeline de extracción, que a un estudiante no le dice nada.
        string texto = InicioViewModel.QueEsAutoExam;

        foreach (string esperado in new[] { "Subís", "examen", "apuntes", "UBA" })
        {
            Assert.Contains(esperado, texto, StringComparison.OrdinalIgnoreCase);
        }

        // Vocabulario que sólo entiende quien ya conoce la app o el código.
        foreach (string jerga in new[] { "pipeline", "extractor", "alcance", "cuota", "API" })
        {
            Assert.DoesNotContain(jerga, texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LaExplicacion_ArrancaCerrada()
    {
        // El menú es la pantalla de todos los días: la explicación se abre cuando se la pide.
        Assert.False(Menu().MostrarQueEs);
    }

    [Fact]
    public void LaExplicacion_SeAbreYSeCierraConElMismoAcceso()
    {
        var menu = Menu();

        menu.AlternarQueEsCommand.Execute(null);
        Assert.True(menu.MostrarQueEs);

        menu.AlternarQueEsCommand.Execute(null);
        Assert.False(menu.MostrarQueEs);
    }

    // ------------------------------------------------------------------
    // El XAML del menú realmente se puede construir
    // ------------------------------------------------------------------

    [Fact]
    public void ElMenu_SeConstruyeSinRomperse()
    {
        // Los demás tests de esta suite leen el XAML como texto, así que un StaticResource
        // mal escrito o un estilo que no existe los pasaría todos y recién explotaría al
        // abrir la app. Construir la vista de verdad es lo único que resuelve los
        // StaticResource: si alguno falta, esto tira XamlParseException acá y no en la
        // computadora del usuario.
        TestSupport.WpfHost.Invocar(() =>
        {
            TestSupport.WpfHost.AsegurarRecursos();

            var vista = new InicioView { DataContext = Menu() };

            Assert.NotNull(vista);
        });
    }

    [Fact]
    public void ElMenu_EsLaPantallaConLaQueArrancaLaApp_US031()
    {
        // Los criterios de US-031 hablan de "cuando entro al menú principal por primera vez".
        // Con la app aterrizando en una sección, ese menú no se vería nunca al arrancar y la
        // invitación a la primera acción sería inalcanzable.
        string shell = Fuente("AutoExam/ViewModels/ShellViewModel.cs");

        Assert.Contains("Pagina = Inicio;", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Pagina = _biblioteca.Libros.Count == 0", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void LasAccionesEstanDisponiblesTambienSinActividad()
    {
        // "Los accesos directos igual están disponibles" el primer día: la grilla no está
        // atada a que haya actividad.
        var grilla = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .First(e => (e.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("Accesos"));

        Assert.Null(grilla.Attribute("Visibility"));
    }
}
