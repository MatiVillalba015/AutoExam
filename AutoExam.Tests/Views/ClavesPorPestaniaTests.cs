using System.IO;
using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-042 — la clave de Gemini pasa de un campo único con todas separadas por coma a una
/// pestaña por clave, y la pantalla de configuración inicial se rediseña según el mockup.
///
/// El cambio de fondo no es la pantalla sino cómo se cargan las claves: lo que estos tests
/// protegen es que el fallback de cuota siga viendo lo mismo que antes (las claves en orden),
/// que borrar una no arrastre a las demás, y que quien ya tenía claves guardadas en el formato
/// viejo no tenga que volver a cargarlas (RN-52).
/// </summary>
public class ClavesPorPestaniaTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    // ------------------------------------------------------------------
    // AC — una pestaña por clave
    // ------------------------------------------------------------------

    [Fact]
    public void ConUnaSolaClaveGuardada_HayUnaPestaniaClave1YSeOfreceAgregarLaSegunda_US042()
    {
        var claves = new ClavesDeGemini();

        claves.Cargar(new[] { "AQ.primera" });

        var unica = Assert.Single(claves.Claves);
        Assert.Equal("clave 1", unica.Etiqueta);
        Assert.Equal("AQ.primera", unica.Valor);
        Assert.Same(unica, claves.Seleccionada);
        Assert.Equal("+ clave 2", claves.EtiquetaDeAgregar);
    }

    [Fact]
    public void SinNingunaClave_QuedaUnaPestaniaVaciaDondeEscribir_US042()
    {
        // La configuración inicial arranca justamente así. Sin ninguna pestaña no habría
        // dónde pegar la primera clave.
        var claves = new ClavesDeGemini();

        var unica = Assert.Single(claves.Claves);
        Assert.Equal(string.Empty, unica.Valor);
        Assert.Same(unica, claves.Seleccionada);
    }

    [Fact]
    public void AlAgregarUnaClave_SeCreaSuPropiaPestaniaYQuedaSeleccionada_US042()
    {
        var claves = new ClavesDeGemini();
        claves.Cargar(new[] { "AQ.primera" });

        claves.AgregarCommand.Execute(null);

        Assert.Equal(2, claves.Claves.Count);
        Assert.Equal("clave 2", claves.Claves[1].Etiqueta);
        Assert.Same(claves.Claves[1], claves.Seleccionada);

        // La nueva nace vacía y la que ya estaba no se toca: son campos independientes.
        Assert.Equal(string.Empty, claves.Claves[1].Valor);
        Assert.Equal("AQ.primera", claves.Claves[0].Valor);
        Assert.Equal("+ clave 3", claves.EtiquetaDeAgregar);
    }

    // ------------------------------------------------------------------
    // AC — borrar una pestaña, sin afectar las demás
    // ------------------------------------------------------------------

    [Fact]
    public void ConUnaSolaPestania_NoSePuedeBorrar_US042()
    {
        // El criterio acota el borrado a "una que no sea la única que queda".
        var claves = new ClavesDeGemini();
        claves.Cargar(new[] { "AQ.unica" });

        Assert.False(claves.PuedeQuitar);

        claves.QuitarCommand.Execute(claves.Claves[0]);

        Assert.Single(claves.Claves);
        Assert.Equal("AQ.unica", claves.Claves[0].Valor);
    }

    [Fact]
    public void AlBorrarUnaPestania_LasDemasClavesQuedanIntactasYSeRenumeran_US042()
    {
        var claves = new ClavesDeGemini();
        claves.Cargar(new[] { "AQ.uno", "AQ.dos", "AQ.tres" });

        claves.QuitarCommand.Execute(claves.Claves[1]);

        Assert.Equal(new[] { "AQ.uno", "AQ.tres" }, claves.ComoLista());
        Assert.Equal(new[] { "clave 1", "clave 2" }, claves.Claves.Select(c => c.Etiqueta));

        // Queda abierta la que ocupó el lugar de la borrada: dejar la selección en nada
        // mostraría el panel vacío y parece que se borraron todas.
        Assert.Same(claves.Claves[1], claves.Seleccionada);
    }

    [Fact]
    public void AlBorrarLaUltimaPestania_QuedaAbiertaLaNuevaUltima_US042()
    {
        var claves = new ClavesDeGemini();
        claves.Cargar(new[] { "AQ.uno", "AQ.dos" });

        claves.QuitarCommand.Execute(claves.Claves[1]);

        Assert.Same(claves.Claves[0], claves.Seleccionada);
    }

    // ------------------------------------------------------------------
    // AC — el fallback de cuota no cambia: el orden de las pestañas es el orden de prueba
    // ------------------------------------------------------------------

    [Fact]
    public void ElOrdenDeLasPestanias_EsElOrdenDeFallback_US042()
    {
        var claves = new ClavesDeGemini();
        claves.Cargar(new[] { "AQ.uno", "AQ.dos", "AQ.tres" });

        // Es lo que consume AppConfig y, detrás, el anillo de claves: mismo comportamiento
        // que con el texto separado por comas, sólo cambia de dónde sale la lista.
        Assert.Equal(new[] { "AQ.uno", "AQ.dos", "AQ.tres" }, claves.ComoLista());
        Assert.Equal("AQ.uno", claves.Primera());

        var config = new AppConfig();
        config.EstablecerClaves(claves.ComoTexto());

        Assert.Equal(new[] { "AQ.uno", "AQ.dos", "AQ.tres" }, config.ClavesDisponibles);
    }

    [Fact]
    public void LasPestaniasVacias_NoCuentanComoClave_US042()
    {
        // Agregar una pestaña y no llenarla no puede dejar un hueco en la rotación.
        var claves = new ClavesDeGemini();
        claves.Cargar(new[] { "AQ.uno" });
        claves.AgregarCommand.Execute(null);

        Assert.Equal(new[] { "AQ.uno" }, claves.ComoLista());
    }

    [Fact]
    public void LasClavesSeUnenConSaltoDeLinea_ParaQueUnaComaPegadaDeMasNoPartaUnaClave_US042()
    {
        // Con el esquema viejo, una coma sobrante partía la clave en dos y ninguna de las
        // mitades servía. Ahora cada pestaña es una clave entera, así que unirlas con coma
        // volvería a introducir ese problema justo al guardar.
        var claves = new ClavesDeGemini();
        claves.Cargar(new[] { "AQ.uno", "AQ.dos" });

        Assert.DoesNotContain(",", claves.ComoTexto(), StringComparison.Ordinal);
        Assert.Equal(2, AppConfig.SepararClaves(claves.ComoTexto()).Count);
    }

    // ------------------------------------------------------------------
    // RN-52 — migración automática y silenciosa del formato viejo
    // ------------------------------------------------------------------

    [Fact]
    public void UnaClaveGuardadaEnElFormatoViejo_SeSeparaEnUnaPorPestania_RN52()
    {
        // Formato viejo: el texto entero, comas incluidas, en un solo campo. Sin migrar, se
        // leería como UNA clave y el fallback de cuota no tendría con qué rotar.
        var config = new AppConfig { ApiKey = "AQ.uno,AQ.dos,AQ.tres" };

        bool migro = config.MigrarClavesASeparadas();

        Assert.True(migro);
        Assert.Equal(new[] { "AQ.uno", "AQ.dos", "AQ.tres" }, config.ClavesDisponibles);

        var claves = new ClavesDeGemini();
        claves.Cargar(config.ClavesDisponibles);

        Assert.Equal(3, claves.Claves.Count);
        Assert.Equal(new[] { "clave 1", "clave 2", "clave 3" }, claves.Claves.Select(c => c.Etiqueta));
    }

    [Fact]
    public void LaMigracion_ConservaElOrdenOriginal_PorqueEsElOrdenDeFallback_RN52()
    {
        var config = new AppConfig { ApiKey = "AQ.tercera, AQ.primera ,AQ.segunda" };

        config.MigrarClavesASeparadas();

        Assert.Equal(new[] { "AQ.tercera", "AQ.primera", "AQ.segunda" }, config.ClavesDisponibles);
    }

    [Fact]
    public void LaMigracion_NoPierdeNingunaClave_NiLasQueYaEstabanSeparadas_RN52()
    {
        var config = new AppConfig
        {
            ApiKey = "AQ.uno,AQ.dos",
            ApiKeys = new List<string> { "AQ.uno,AQ.dos", "AQ.tres" },
        };

        config.MigrarClavesASeparadas();

        Assert.Equal(new[] { "AQ.uno", "AQ.dos", "AQ.tres" }, config.ClavesDisponibles);
    }

    [Fact]
    public void SinNadaQueSeparar_LaMigracionNoHaceNada_RN52()
    {
        // Es lo que pasa en todos los arranques después del primero: no puede reescribir
        // config.json cada vez ni tocar lo que ya está bien.
        var config = new AppConfig
        {
            ApiKey = "AQ.uno",
            ApiKeys = new List<string> { "AQ.uno", "AQ.dos" },
        };

        Assert.False(config.MigrarClavesASeparadas());
        Assert.Equal(new[] { "AQ.uno", "AQ.dos" }, config.ClavesDisponibles);
    }

    [Fact]
    public void LaMigracion_CorreSolaAlCargarLaSesion_SinPedirleNadaAlUsuario_RN52()
    {
        // "Sin que tenga que volver a cargarlas a mano": nadie llama a la migración desde la
        // interfaz, entra en el mismo camino de arranque que las demás.
        string carga = Fuente("AutoExam/Services/SesionUsuarioService.cs");

        int enCargar = carga.IndexOf("public void Cargar()", StringComparison.Ordinal);
        int llamada = carga.IndexOf("MigrarClavesDeGemini();", StringComparison.Ordinal);
        int finDeCargar = carga.IndexOf("RefrescarHistorial();", enCargar, StringComparison.Ordinal);

        Assert.InRange(llamada, enCargar, finDeCargar);
    }

    // ------------------------------------------------------------------
    // AC — el mismo esquema en las dos pantallas que cargan la clave
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("AutoExam/Views/OnboardingView.xaml")]
    [InlineData("AutoExam/Views/AjustesView.xaml")]
    public void LasDosPantallasQueCarganLaClave_UsanElMismoControlDePestanias_US042(string ruta)
    {
        var control = Vista(ruta).Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "CamposDeClaves");

        Assert.True(control is not null,
            $"{ruta} no usa CamposDeClaves: US-042 pide que la clave se cargue igual en toda la app.");

        Assert.Contains("Claves", control!.Attribute("DataContext")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AutoExam/Views/OnboardingView.xaml")]
    [InlineData("AutoExam/Views/AjustesView.xaml")]
    public void NingunaPantalla_ConservaElCampoUnicoDeClavesSeparadasPorComa_US042(string ruta)
    {
        string xaml = Fuente(ruta);

        // El campo viejo era un PasswordBox suelto en la pantalla. Si volviera, habría otra
        // vez dos formas de cargar la clave según dónde se esté parado.
        Assert.DoesNotContain("PasswordBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("separalas con comas", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CadaPestania_TieneSuPropioCampoConSuBotonDeMostrarUOcultar_US042()
    {
        // El campo vive en el ContentTemplate del TabControl, así que el control se
        // reconstruye por pestaña: cada clave tiene el suyo y revelar una no revela la de al
        // lado.
        var contenido = Vista("AutoExam/Views/CamposDeClaves.xaml").Descendants()
            .First(e => e.Name.LocalName == "TabControl.ContentTemplate");

        var campo = contenido.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "PasswordBox");

        Assert.True(campo is not null, "La pestaña no tiene campo de clave propio (US-042).");
        Assert.Equal("True", campo!.Attribute("RevealButtonEnabled")?.Value);
        Assert.Contains("Valor", campo.Attribute("Password")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void LaPestania_TraeSuPropioBotonDeBorrar_QueDesapareceSiEsLaUnica_US042()
    {
        var estilo = Vista("AutoExam/Theme/Estilos.xaml").Descendants()
            .First(e => e.Name.LocalName == "Style" &&
                        e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == "PestanaDeClave"));

        var borrar = estilo.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 (e.Attribute("Command")?.Value ?? string.Empty)
                                     .Contains("QuitarCommand", StringComparison.Ordinal));

        Assert.True(borrar is not null, "La pestaña no tiene con qué borrar su clave (US-042).");
        Assert.Contains("PuedeQuitar", borrar!.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LaFilaDePestanias_TieneElBotonDeAgregarConElNumeroQueSigue_US042()
    {
        var estilo = Vista("AutoExam/Theme/Estilos.xaml").Descendants()
            .First(e => e.Name.LocalName == "Style" &&
                        e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == "PestanasDeClave"));

        var agregar = estilo.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 (e.Attribute("Command")?.Value ?? string.Empty)
                                     .Contains("AgregarCommand", StringComparison.Ordinal));

        Assert.True(agregar is not null, "No hay botón para agregar otra clave (US-042).");
        Assert.Contains("EtiquetaDeAgregar", agregar!.Attribute("Content")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — el diseño de la pantalla de configuración inicial
    // ------------------------------------------------------------------

    [Fact]
    public void LaConfiguracionInicial_TieneElEmblemaConLaInicial_US042()
    {
        var vista = Vista("AutoExam/Views/OnboardingView.xaml");

        var emblema = vista.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("Background")?.Value ?? string.Empty)
                                     .Contains("PincelIconoAccion", StringComparison.Ordinal));

        Assert.True(emblema is not null,
            "Falta el ícono con el degradado violeta del encabezado (US-042).");

        // Reusa el mismo degradado que los íconos del menú (US-041), no un color nuevo.
        Assert.Contains(emblema!.Descendants(),
            e => e.Name.LocalName == "TextBlock" && e.Attribute("Text")?.Value == "A");
    }

    [Fact]
    public void LaTarjetaDeLaDerecha_TieneVerificarSuEstadoConBarraYElLinkAAIStudio_US042()
    {
        var vista = Vista("AutoExam/Views/OnboardingView.xaml");

        Assert.Contains(vista.Descendants(),
            e => e.Name.LocalName == "Button" && e.Attribute("Content")?.Value == "Verificar y empezar");

        Assert.Contains(vista.Descendants(), e => e.Name.LocalName == "ProgressBar");

        Assert.Contains(vista.Descendants(),
            e => e.Name.LocalName == "HyperlinkButton" &&
                 (e.Attribute("Content")?.Value ?? string.Empty)
                     .Contains("Google AI Studio", StringComparison.Ordinal));
    }

    [Fact]
    public void LaConfiguracionInicial_SigueTeniendoLaValvulaDeEscape_US042()
    {
        // El rediseño la mueve al pie, pero no la saca: nadie tiene que quedar trabado en la
        // primera pantalla porque su clave todavía no funciona.
        var salida = Vista("AutoExam/Views/OnboardingView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 e.Attribute("Content")?.Value == "Entrar sin verificar");

        Assert.True(salida is not null, "Desapareció \"Entrar sin verificar\".");
        Assert.Contains("OmitirCommand", salida!.Attribute("Command")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ElTextoDeAyuda_YaNoDiceQueSePeguenSeparadasPorComa_US042()
    {
        // La instrucción cambió con el esquema. Lo que no cambia es para qué sirve tener
        // varias, que es lo único que al alumno le importa.
        //
        // Se miran los textos visibles y no el archivo entero: los comentarios del XAML sí
        // nombran el formato viejo, porque explican de qué se viene.
        var textos = Vista("AutoExam/Views/OnboardingView.xaml").Descendants()
            .Select(e => e.Attribute("Text")?.Value ?? string.Empty)
            .Where(t => t.Length > 0)
            .ToList();

        Assert.DoesNotContain(textos, t => t.Contains("separadas por coma", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(textos, t => t.Contains("cuota diaria", StringComparison.OrdinalIgnoreCase));
    }
}
