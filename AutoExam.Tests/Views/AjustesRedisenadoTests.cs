using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-046..US-056 — el rediseño de Ajustes: la distribución del mockup y las reglas que ese
/// layout tiene que respetar.
///
/// Son tests estructurales sobre el XAML, como el resto de los rediseños del proyecto: fijan
/// que cada control nuevo exista, esté enganchado a su comando y viva en la tarjeta que le
/// corresponde. Lo que no pueden ver —que se vea lindo— queda para la revisión visual.
/// </summary>
public class AjustesRedisenadoTests
{
    private const string Vista = "AutoExam/Views/AjustesView.xaml";

    private static XDocument Doc(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static IEnumerable<XElement> Elementos() => Doc(Vista).Descendants();

    private static string Atributo(XElement e, string nombre) => e.Attribute(nombre)?.Value ?? string.Empty;

    /// <summary>La tarjeta (Border o CardExpander) que contiene un texto dado.</summary>
    private static XElement TarjetaCon(string texto)
    {
        var ancla = Elementos().First(e => Atributo(e, "Text").Contains(texto, StringComparison.Ordinal));

        return ancla.Ancestors().First(a => a.Name.LocalName is "Border" or "CardExpander");
    }

    private static bool HayComando(string comando) =>
        Elementos().Any(e => Atributo(e, "Command").Contains(comando, StringComparison.Ordinal));

    // ------------------------------------------------------------------
    // AC — cada sección nueva está, y en su tarjeta
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Clave de Gemini")]
    [InlineData("Tema")]
    [InlineData("Tamaño de la interfaz")]
    [InlineData("Notificaciones")]
    [InlineData("Reducir movimiento")]
    [InlineData("Datos y almacenamiento")]
    [InlineData("Copia de seguridad")]
    [InlineData("Avanzado")]
    [InlineData("Restaurar valores de fábrica")]
    public void CadaSeccionDelMockup_TieneSuTitulo(string titulo)
    {
        Assert.Contains(Elementos(),
            e => Atributo(e, "Text").Contains(titulo, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("BuscarActualizacionCommand")]
    [InlineData("AbrirCarpetaCommand")]
    [InlineData("VaciarCacheCommand")]
    [InlineData("ExportarCopiaCommand")]
    [InlineData("ImportarCopiaCommand")]
    [InlineData("RestaurarFabricaCommand")]
    [InlineData("ElegirTemaCommand")]
    [InlineData("AumentarZoomCommand")]
    [InlineData("ReducirZoomCommand")]
    public void CadaAccionNueva_EstaEnganchadaASuComando(string comando)
    {
        Assert.True(HayComando(comando), $"Ningún control de Ajustes ejecuta {comando}.");
    }

    // ------------------------------------------------------------------
    // US-052 / RN-60 — lista cerrada de modelos
    // ------------------------------------------------------------------

    /// <summary>
    /// El selector era editable: se podía escribir cualquier cosa. Un nombre de modelo mal
    /// escrito no falla al escribirlo sino recién al generar, y ahí el 404 de Google no dice
    /// que el problema fue un typo. RN-60 lo cierra a la lista que mantiene la app.
    /// </summary>
    [Fact]
    public void ElModelo_SeEligeDeUnaLista_NoSeEscribe_RN60()
    {
        var selector = Elementos().First(e => e.Name.LocalName == "ComboBox" &&
            Atributo(e, "ItemsSource").Contains("Modelos", StringComparison.Ordinal));

        Assert.Equal("False", Atributo(selector, "IsEditable"));
        Assert.Contains("Modelo", Atributo(selector, "SelectedItem"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Atributo(selector, "Text"));
    }

    [Fact]
    public void ElSelectorDeModelo_EstaArribaDeLasPestaniasDeClave_US052()
    {
        // Es la distribución del mockup, y tiene una razón: el modelo es lo que se prueba con
        // "Probar conexión", así que se elige antes de mirar las claves.
        var tarjeta = TarjetaCon("Clave de Gemini");
        var hijos = tarjeta.Descendants().ToList();

        int modelo = hijos.FindIndex(e => Atributo(e, "Text").Contains("MODELO DE GEMINI", StringComparison.Ordinal));
        int pestanias = hijos.FindIndex(e => e.Name.LocalName == "CamposDeClaves");

        Assert.True(modelo >= 0 && pestanias >= 0, "Falta el selector de modelo o las pestañas de clave.");
        Assert.True(modelo < pestanias, "El selector de modelo tiene que ir arriba de las pestañas (US-052).");
    }

    [Fact]
    public void ElBotonDetectar_Sigue_ParaAmpliarLaLista_US052()
    {
        Assert.True(HayComando("DetectarCommand"),
            "Sin \"Detectar\" la lista cerrada no se puede ampliar con los modelos reales de la clave.");
    }

    // ------------------------------------------------------------------
    // US-048 — el porcentaje siempre visible
    // ------------------------------------------------------------------

    [Fact]
    public void ElPorcentajeDeZoom_EstaSiempreALaVista_US048()
    {
        var tarjeta = TarjetaCon("Tamaño de la interfaz");

        var porcentaje = tarjeta.Descendants().FirstOrDefault(
            e => Atributo(e, "Text").Contains("ZoomTexto", StringComparison.Ordinal));

        Assert.True(porcentaje is not null, "El porcentaje de zoom no se muestra (US-048).");

        // Sin Visibility condicional: "siempre visible" es literal en el criterio.
        Assert.Equal(string.Empty, Atributo(porcentaje!, "Visibility"));
    }

    [Fact]
    public void ElZoom_TieneSliderYBotones_ConSusLimitesTomadosDelViewModel_US048()
    {
        var tarjeta = TarjetaCon("Tamaño de la interfaz");

        var slider = tarjeta.Descendants().FirstOrDefault(e => e.Name.LocalName == "Slider");

        Assert.True(slider is not null, "Falta el control de zoom (US-048).");
        Assert.Contains("ZoomMinimo", Atributo(slider!, "Minimum"), StringComparison.Ordinal);
        Assert.Contains("ZoomMaximo", Atributo(slider!, "Maximum"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // US-049 — muestras de tema con animación estándar
    // ------------------------------------------------------------------

    [Fact]
    public void CadaTema_SeMuestraConUnaVistaPreviaDeColor_US049()
    {
        var tarjeta = TarjetaCon("El color de acento general");

        var lista = tarjeta.Descendants().First(e => e.Name.LocalName == "ItemsControl" &&
            Atributo(e, "ItemsSource").Contains("Temas", StringComparison.Ordinal));

        var plantilla = lista.Descendants().First(e => e.Name.LocalName == "DataTemplate");

        // La muestra es un degradado entre el acento y su tono tenue: los dos colores que el
        // tema realmente cambia.
        Assert.Contains(plantilla.Descendants(),
            e => Atributo(e, "Color").Contains("ColorDesde", StringComparison.Ordinal));
        Assert.Contains(plantilla.Descendants(),
            e => Atributo(e, "Color").Contains("ColorHasta", StringComparison.Ordinal));
        Assert.Contains(plantilla.Descendants(),
            e => Atributo(e, "Text").Contains("Nombre", StringComparison.Ordinal));
    }

    /// <summary>
    /// RN-53 fija el hover como estándar de la app: zoom leve más borde de acento en las
    /// tarjetas que se eligen. Las muestras de tema reusan TarjetaDeOpcion (US-043) en vez de
    /// tener su propia animación, que es lo que hace que se comporten igual que las cuatro
    /// tarjetas del asistente.
    /// </summary>
    [Fact]
    public void LasMuestrasDeTema_UsanElEstiloDeTarjetaSeleccionable_RN53()
    {
        var tarjeta = TarjetaCon("El color de acento general");

        var opcion = tarjeta.Descendants().First(e => e.Name.LocalName == "RadioButton");

        Assert.Contains("TarjetaDeOpcion", Atributo(opcion, "Style"), StringComparison.Ordinal);
    }

    [Fact]
    public void LasDosAccionesDeCopiaDeSeguridad_VanEnGrillaDeDosColumnas_US051()
    {
        var tarjeta = TarjetaCon("Un respaldo manual y local");

        var grilla = tarjeta.Descendants().First(e => e.Name.LocalName == "Grid" &&
            e.Elements().Any(h => h.Name.LocalName == "Grid.ColumnDefinitions") &&
            e.Descendants().Any(d => Atributo(d, "Command").Contains("ExportarCopiaCommand", StringComparison.Ordinal)));

        int columnas = grilla.Elements()
            .First(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Count();

        // Dos acciones más el separador del medio.
        Assert.Equal(3, columnas);
    }

    // ------------------------------------------------------------------
    // US-046 — barra segmentada y sus tres referencias
    // ------------------------------------------------------------------

    [Fact]
    public void LaBarraDeUso_SeRepartePorLasTresFracciones_US046()
    {
        var tarjeta = TarjetaCon("Datos y almacenamiento");

        var anchos = tarjeta.Descendants()
            .Where(e => e.Name.LocalName == "ColumnDefinition")
            .Select(e => Atributo(e, "Width"))
            .Where(w => w.Contains("Fraccion", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, anchos.Count);
        Assert.All(anchos, w => Assert.Contains("FraccionAEstrella", w, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Usos[0].Texto")]
    [InlineData("Usos[1].Texto")]
    [InlineData("Usos[2].Texto")]
    public void CadaGrupo_MuestraSuTamanio(string binding)
    {
        Assert.Contains(Elementos(),
            e => Atributo(e, "Text").Contains(binding, StringComparison.Ordinal));
    }

    /// <summary>
    /// El criterio pide informar cuando no se pudo calcular un tamaño, en vez de mostrar un
    /// número incorrecto o quedar en blanco.
    /// </summary>
    [Fact]
    public void SiNoSePudoMedirAlgo_LaPantallaLoAvisa_US046()
    {
        Assert.Contains(Elementos(),
            e => Atributo(e, "IsOpen").Contains("HayMedicionIncompleta", StringComparison.Ordinal));
    }

    [Fact]
    public void VaciarCache_SeApagaCuandoNoHayNadaQueVaciar_US046()
    {
        var boton = Elementos().First(
            e => Atributo(e, "Command").Contains("VaciarCacheCommand", StringComparison.Ordinal));

        Assert.Contains("HayAlgoQueVaciar", Atributo(boton, "IsEnabled"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // US-047 — zona de riesgo, aparte de todo lo demás
    // ------------------------------------------------------------------

    [Fact]
    public void RestaurarValoresDeFabrica_TieneTratamientoDeZonaDeRiesgo_US047()
    {
        var boton = Elementos().First(
            e => Atributo(e, "Command").Contains("RestaurarFabricaCommand", StringComparison.Ordinal));

        Assert.Contains("BotonDestructivo", Atributo(boton, "Style"), StringComparison.Ordinal);

        var tarjeta = boton.Ancestors().First(a => a.Name.LocalName == "Border");

        Assert.Contains("ZonaDeRiesgo", Atributo(tarjeta, "Style"), StringComparison.Ordinal);
        Assert.Contains(tarjeta.Descendants(),
            e => e.Name.LocalName == "SymbolIcon" && Atributo(e, "Symbol").Contains("Warning", StringComparison.Ordinal));
    }

    /// <summary>
    /// El criterio pide que quede "claramente diferenciado, separado de Vaciar caché". Que no
    /// compartan tarjeta es lo que impide que se lean como dos variantes de lo mismo.
    /// </summary>
    [Fact]
    public void RestaurarFabrica_NoCompartTarjetaConVaciarCache_US047()
    {
        var restaurar = Elementos().First(
            e => Atributo(e, "Command").Contains("RestaurarFabricaCommand", StringComparison.Ordinal));

        var suTarjeta = restaurar.Ancestors().First(a => a.Name.LocalName == "Border");

        Assert.DoesNotContain(suTarjeta.Descendants(),
            e => Atributo(e, "Command").Contains("VaciarCacheCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void LaZonaDeRiesgo_DiceEnLaPropiaTarjetaQueNoBorraNadaDelUsuario_US047()
    {
        var boton = Elementos().First(
            e => Atributo(e, "Command").Contains("RestaurarFabricaCommand", StringComparison.Ordinal));

        var tarjeta = boton.Ancestors().First(a => a.Name.LocalName == "Border");

        string textos = string.Join(" ", tarjeta.Descendants().Select(e => Atributo(e, "Text")));

        // Quien duda si va a perder sus libros no debería tener que apretar el botón para
        // averiguarlo.
        Assert.Contains("libros", textos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("historial", textos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("claves", textos, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // US-056 — "Preguntas por lote" sigue, en Avanzado
    // ------------------------------------------------------------------

    [Fact]
    public void PreguntasPorLote_VivenEnLaSeccionAvanzado_US056()
    {
        var control = Elementos().First(e => e.Name.LocalName == "NumberBox" &&
            Atributo(e, "Value").Contains("PreguntasPorLote", StringComparison.Ordinal));

        var avanzado = control.Ancestors().FirstOrDefault(a => a.Name.LocalName == "CardExpander");

        Assert.True(avanzado is not null, "\"Preguntas por lote\" quedó fuera de una sección (US-056).");

        Assert.Contains(avanzado!.Descendants(),
            e => Atributo(e, "Text") == "Avanzado");
    }

    [Fact]
    public void PreguntasPorLote_ConservaSuRango_US056()
    {
        var control = Elementos().First(e => e.Name.LocalName == "NumberBox" &&
            Atributo(e, "Value").Contains("PreguntasPorLote", StringComparison.Ordinal));

        // RN-64: esta historia reubica el control, no cambia sus valores.
        Assert.Equal("5", Atributo(control, "Minimum"));
        Assert.Equal("15", Atributo(control, "Maximum"));
    }

    [Fact]
    public void LaAyudaDePreguntasPorLote_SigueDiciendoQueEsUnMinimo_US056()
    {
        string textos = string.Join(" ", Elementos().Select(e => Atributo(e, "Text")));

        Assert.Contains("Mínimo de preguntas", textos, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // RN-53 — el fondo estándar, y su única excepción
    // ------------------------------------------------------------------

    [Fact]
    public void Ajustes_LlevaElFondoDeLuzVioleta_RN53()
    {
        var canvas = Doc(Vista).Descendants().FirstOrDefault(e => e.Name.LocalName == "Canvas");

        Assert.True(canvas is not null, "Ajustes no tiene el fondo con luz violeta (RN-53).");
        Assert.Equal("False", Atributo(canvas!, "IsHitTestVisible"));
        Assert.Equal(2, canvas!.Descendants().Count(e => e.Name.LocalName == "RadialGradientBrush"));
    }

    [Fact]
    public void LaPantallaDeLaClave_SigueSinEseFondo_RN53()
    {
        // La excepción que RN-53 nombra: la configuración inicial (US-042) tiene su diseño
        // propio y no lleva la luz violeta.
        var onboarding = Doc("AutoExam/Views/OnboardingView.xaml");

        Assert.DoesNotContain(onboarding.Descendants(), e => e.Name.LocalName == "RadialGradientBrush");
    }

    // ------------------------------------------------------------------
    // RN-58 — las notificaciones no silencian errores ni confirmaciones
    // ------------------------------------------------------------------

    /// <summary>
    /// La garantía que hace que apagar los avisos sea seguro: el aviso de la ventana se enciende
    /// por <c>HayAviso</c>, que sale de <c>ShellViewModel.Notificar</c> y solo de ahí. Los
    /// errores viajan por las InfoBar de cada pantalla y por IDialogos, que no consultan la
    /// preferencia en ningún lado.
    /// </summary>
    [Fact]
    public void ElAvisoDeLaVentana_EsElUnicoControlAtadoALaPreferencia_RN58()
    {
        var ventana = Doc("AutoExam/MainWindow.xaml");

        var aviso = ventana.Descendants().First(
            e => Atributo(e, "Visibility").Contains("HayAviso", StringComparison.Ordinal));

        Assert.Contains(aviso.Descendants(),
            e => Atributo(e, "Text").Contains("Aviso", StringComparison.Ordinal));

        // Ninguna InfoBar de error de las pantallas mira la preferencia.
        string carpeta = Path.GetDirectoryName(ArchivoFuenteHelper.RutaFuente(Vista))!;

        foreach (string ruta in Directory.GetFiles(carpeta, "*.xaml"))
        {
            var vista = XDocument.Load(ruta);

            var atadas = vista.Descendants()
                .Where(e => e.Name.LocalName == "InfoBar")
                .Where(e => e.Attributes().Any(a => a.Value.Contains("Notificaciones", StringComparison.Ordinal)))
                .ToList();

            Assert.True(atadas.Count == 0,
                $"{Path.GetFileName(ruta)} tiene una InfoBar atada a la preferencia de notificaciones (RN-58).");
        }
    }

    [Fact]
    public void ElAviso_SePuedeCerrarAMano_YNoRobaClicksALoQueTapa_US050()
    {
        var ventana = Doc("AutoExam/MainWindow.xaml");

        var aviso = ventana.Descendants().First(
            e => Atributo(e, "Visibility").Contains("HayAviso", StringComparison.Ordinal));

        Assert.Contains(aviso.Descendants(),
            e => Atributo(e, "Command").Contains("CerrarAvisoCommand", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // US-048 / RN-56 — un solo punto de escalado
    // ------------------------------------------------------------------

    /// <summary>
    /// RN-56 pide el mecanismo centralizado: una sola transformación en la ventana y no un
    /// tamaño por pantalla. Va en LayoutTransform y no en RenderTransform porque el layout
    /// tiene que rehacerse con el tamaño nuevo; con RenderTransform el contenido se agranda
    /// por encima de la ventana y queda cortado.
    /// </summary>
    [Fact]
    public void ElZoom_SeAplicaUnaSolaVez_EnLaVentana_RN56()
    {
        var ventana = Doc("AutoExam/MainWindow.xaml");

        var escalas = ventana.Descendants()
            .Where(e => e.Name.LocalName == "ScaleTransform" &&
                        Atributo(e, "ScaleX").Contains("Zoom", StringComparison.Ordinal))
            .ToList();

        Assert.Single(escalas);
        Assert.Equal("LayoutTransform", escalas[0].Parent!.Name.LocalName.Split('.')[^1]);
    }

    [Fact]
    public void LaBarraDeTitulo_QuedaFueraDelZoom_US048()
    {
        var ventana = Doc("AutoExam/MainWindow.xaml");

        var barra = ventana.Descendants().First(e => e.Name.LocalName == "TitleBar");

        // Agrandar los botones de minimizar y cerrar los desalinearía del borde de la ventana:
        // son cromo del sistema, no contenido de la app.
        Assert.DoesNotContain(barra.Ancestors(),
            a => a.Elements().Any(h => h.Name.LocalName.EndsWith("LayoutTransform", StringComparison.Ordinal)));
    }
}
