using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-058 — rediseño del asistente de Nuevo examen (Material, Alcance y Formato).
///
/// La historia va más allá del pulido de US-043: cambia cómo se agrupa el contenido (cada
/// grupo de opciones en su propia tarjeta), qué cuenta el riel de pasos (un resumen de lo
/// elegido, no solo el número) y agrega el panel "Tu examen", que acompaña los 3 pasos y
/// termina en el botón de generar.
///
/// Lo que estos tests cuidan sobre todo son las dos reglas que es fácil romper sin darse
/// cuenta al seguir tocando la pantalla: RN-67 (el panel no edita nada) y RN-68 (lo que el
/// panel muestra sale de las mismas selecciones que ya tiene el asistente, no de una copia
/// propia que se pueda desincronizar).
///
/// Lo que no cubren es si se ve bien. Eso hay que mirarlo.
/// </summary>
public class NuevoExamenRedisenadoTests
{
    private const string Vista = "AutoExam/Views/AsistenteView.xaml";

    private static XDocument Doc() => XDocument.Load(ArchivoFuenteHelper.RutaFuente(Vista));

    private static string Fuente() => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(Vista));

    /// <summary>
    /// Lee un atributo por su nombre corto. Una propiedad adjunta se escribe con su clase
    /// adelante (<c>Grid.Row</c>) y XLinq la guarda con ese nombre entero, así que
    /// "Row" tiene que encontrar igual a "Grid.Row".
    /// </summary>
    private static string Atributo(XElement e, string nombre) =>
        e.Attributes()
            .FirstOrDefault(a => a.Name.LocalName == nombre ||
                                 a.Name.LocalName.EndsWith("." + nombre, StringComparison.Ordinal))
            ?.Value ?? string.Empty;

    /// <summary>El Grid de más afuera de la vista: el que reparte columnas y filas.</summary>
    private static XElement GridRaiz() =>
        Doc().Root!.Elements().First(e => e.Name.LocalName == "Grid");

    /// <summary>La tarjeta "Tu examen": el Border que ocupa la columna de la derecha.</summary>
    private static XElement Panel() =>
        GridRaiz().Elements().First(e => e.Name.LocalName == "Border" &&
                                         Atributo(e, "Column") == "2");

    // ------------------------------------------------------------------
    // AC — cada grupo de opciones vive en su propia tarjeta con encabezado
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("¿De donde salen las preguntas?")]
    [InlineData("¿Qué querés repasar?")]
    [InlineData("Elegí el examen compartido")]
    [InlineData("Elegí los exámenes a combinar")]
    [InlineData("¿De qué materia?")]
    [InlineData("Elegí uno o más documentos")]
    [InlineData("Capítulos")]
    [InlineData("Páginas")]
    [InlineData("Eje temático")]
    [InlineData("¿Cuántas preguntas?")]
    [InlineData("Tiempo")]
    public void CadaGrupoDeOpciones_EsElEncabezadoDeSuPropiaTarjeta_US058(string encabezado)
    {
        var titulo = Doc().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBlock" &&
                                 Atributo(e, "Text") == encabezado);

        Assert.True(titulo is not null,
            $"No se encontró el encabezado {encabezado} en el asistente.");

        var tarjeta = titulo!.Ancestors()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 Atributo(e, "Style").Contains("Tarjeta", StringComparison.Ordinal));

        Assert.True(tarjeta is not null,
            $"El grupo {encabezado} quedó suelto sobre el fondo: no está dentro de una tarjeta.");
    }

    // ------------------------------------------------------------------
    // AC — el riel muestra un resumen de lo elegido en cada paso
    // ------------------------------------------------------------------

    [Fact]
    public void ElRielDePasos_MuestraElResumenDeLoElegido_NoSoloElNumero_US058()
    {
        var riel = Doc().Descendants()
            .First(e => e.Name.LocalName == "ItemsControl" &&
                        Atributo(e, "ItemsSource").Contains("Pasos", StringComparison.Ordinal));

        bool hayResumen = riel.Descendants()
            .Any(e => e.Name.LocalName == "TextBlock" &&
                      Atributo(e, "Text").Contains("Resumen", StringComparison.Ordinal));

        Assert.True(hayResumen, "El riel no muestra el resumen de cada paso.");
    }

    // ------------------------------------------------------------------
    // AC — el panel "Tu examen" acompaña los 3 pasos
    // ------------------------------------------------------------------

    [Fact]
    public void ElPanelTuExamen_CuelgaDelGridRaiz_YNoDeUnPaso_US058()
    {
        // Estar en una columna del Grid raíz —y no adentro del ScrollViewer del paso— es
        // exactamente lo que lo mantiene quieto al bajar por una lista larga de documentos:
        // no necesita ningún comportamiento de scroll propio.
        var panel = Panel();

        Assert.Contains("Tu examen", panel.ToString(), StringComparison.Ordinal);
        Assert.Equal("3", Atributo(panel, "Row"));
        Assert.Equal("Top", Atributo(panel, "VerticalAlignment"));
    }

    [Fact]
    public void ElPanel_SeVeEnLosTresPasos_NoSoloEnElUltimo_US058()
    {
        // Si el panel tuviera la visibilidad atada al paso dejaría de acompañar el
        // recorrido: sería la tarjeta de resumen final de siempre con otro nombre.
        var panel = Panel();

        Assert.Equal(string.Empty, Atributo(panel, "Visibility"));
        Assert.DoesNotContain("Paso}", Atributo(panel, "Style"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MATERIA")]
    [InlineData("MATERIAL")]
    [InlineData("ALCANCE")]
    [InlineData("FORMATO")]
    public void ElPanel_TieneUnaLineaPorCadaCosaQueSeElige_US058(string rotulo)
    {
        bool esta = Panel().Descendants()
            .Any(e => e.Name.LocalName == "TextBlock" && Atributo(e, "Text") == rotulo);

        Assert.True(esta, $"Al panel Tu examen le falta la línea {rotulo}.");
    }

    [Fact]
    public void LoQueTodaviaNoSeEligio_SeVeAtenuadoYDiceEnQuePasoSeDefine_US058()
    {
        var plantilla = Doc().Descendants()
            .First(e => e.Name.LocalName == "DataTemplate" &&
                        Atributo(e, "Key") == "LineaDelPanel");

        var aclaracion = plantilla.Descendants()
            .First(e => e.Name.LocalName == "TextBlock" &&
                        Atributo(e, "Style").Contains("TxtTenue", StringComparison.Ordinal));

        Assert.Contains("Se define en el paso", aclaracion.ToString(), StringComparison.Ordinal);

        // Y se turna con el resumen según si el paso está pendiente, no según el paso actual:
        // volver atrás no tiene por qué borrar lo que ya se había elegido más adelante.
        Assert.Contains("Pendiente", aclaracion.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// RN-68 — el panel no guarda un estado propio: muestra los mismos objetos
    /// <c>PasoAsistente</c> que dibuja el riel, así que no puede decir una cosa distinta de
    /// la que dice el riel ni de lo que el asistente tiene realmente configurado.
    /// </summary>
    [Theory]
    [InlineData("Pasos[0]")]
    [InlineData("Pasos[1]")]
    [InlineData("Pasos[2]")]
    public void ElPanel_SaleDeLasMismasSeleccionesQueElRiel_RN68(string paso)
    {
        bool esta = Panel().Descendants()
            .Any(e => Atributo(e, "Content").Contains(paso, StringComparison.Ordinal));

        Assert.True(esta, $"El panel no está leyendo {paso}: armó su propia copia del resumen.");
    }

    /// <summary>
    /// RN-67 — el panel es de solo lectura. Lo único accionable que puede tener es el botón
    /// de generar, que es el final del recorrido y no una forma alternativa de configurarlo.
    /// </summary>
    [Fact]
    public void ElPanel_NoEditaNada_SuUnicoControlEsElDeGenerar_RN67()
    {
        string[] editables = ["TextBox", "ComboBox", "CheckBox", "ToggleButton", "NumberBox", "Slider"];

        var editable = Panel().Descendants()
            .FirstOrDefault(e => editables.Contains(e.Name.LocalName, StringComparer.Ordinal));

        Assert.True(editable is null,
            $"El panel Tu examen tiene un {editable?.Name.LocalName}: dejó de ser de solo lectura.");

        var accionables = Panel().Descendants()
            .Where(e => e.Name.LocalName == "Button")
            .ToList();

        Assert.Single(accionables);
        Assert.Contains("GenerarCommand", Atributo(accionables[0], "Command"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — "Generar examen" pasa a vivir dentro del panel
    // ------------------------------------------------------------------

    [Fact]
    public void GenerarExamen_EsElRemateDelPanel_YSoloApareceEnElUltimoPaso_US058()
    {
        var boton = Panel().Descendants()
            .First(e => e.Name.LocalName == "Button" &&
                        Atributo(e, "Command").Contains("GenerarCommand", StringComparison.Ordinal));

        Assert.Equal("Generar examen", Atributo(boton, "Content"));
        Assert.Contains("EsUltimoPaso", Atributo(boton, "Visibility"), StringComparison.Ordinal);

        // Y es lo último del panel: el remate, no un control perdido en el medio.
        Assert.Same(boton, boton.Parent!.Elements().Last());
    }

    [Fact]
    public void ElPieDelRecorrido_YaNoRepiteElBotonDeGenerar_US058()
    {
        var pie = GridRaiz().Elements()
            .First(e => e.Name.LocalName == "Grid" && Atributo(e, "Row") == "5");

        bool repite = pie.Descendants()
            .Any(e => Atributo(e, "Command").Contains("GenerarCommand", StringComparison.Ordinal));

        Assert.False(repite, "El botón de generar quedó duplicado: está en el panel y en el pie.");
    }

    // ------------------------------------------------------------------
    // Regresión de layout
    // ------------------------------------------------------------------

    /// <summary>
    /// El ancho máximo de RN-13 se aplica con Stretch, no con Center: un Grid centrado se
    /// mide por su contenido —las columnas <c>*</c> no se estiran—, así que el riel y el
    /// panel se corrían de lugar al pasar a un paso con tarjetas más angostas. Con Stretch
    /// el Grid pide siempre los 1160 y WPF lo centra igual cuando sobra espacio.
    /// </summary>
    [Fact]
    public void ElAnchoDelAsistente_NoCambiaDeUnPasoAOtro_RN13()
    {
        var raiz = GridRaiz();

        Assert.Equal("1160", Atributo(raiz, "MaxWidth"));
        Assert.Equal("Stretch", Atributo(raiz, "HorizontalAlignment"));
    }

    /// <summary>
    /// AC — "ninguna opción, campo, validación ni comportamiento cambia". El asistente sigue
    /// generando y avanzando con los mismos comandos de siempre.
    /// </summary>
    [Theory]
    [InlineData("GenerarCommand")]
    [InlineData("SiguienteCommand")]
    [InlineData("AnteriorCommand")]
    public void LosComandosDelRecorrido_SiguenSiendoLosDeSiempre_US058(string comando)
    {
        Assert.Contains(comando, Fuente(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Estas dos guardas se escribían con <c>BoolToVis</c>, que es el
    /// <c>BooleanToVisibilityConverter</c> del framework y no mira el ConverterParameter: la
    /// aclaración de "este material se toma completo" salía también sobre un PDF, y el aviso
    /// de "este libro no tiene capítulos cargados" no salía nunca. Van con el convertidor
    /// propio, que sí entiende "invertir".
    /// </summary>
    [Theory]
    [InlineData("EsFuentePdf")]
    [InlineData("HayModulos")]
    public void LasGuardasInvertidasDelPasoAlcance_UsanElConvertidorQueEntiendeInvertir(string propiedad)
    {
        var conGuarda = Doc().Descendants()
            .Where(e => Atributo(e, "Visibility").Contains(propiedad, StringComparison.Ordinal) &&
                        Atributo(e, "Visibility").Contains("invertir", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(conGuarda);
        Assert.All(conGuarda, e =>
            Assert.Contains("BoolAVisibilidad", Atributo(e, "Visibility"), StringComparison.Ordinal));
    }
}
