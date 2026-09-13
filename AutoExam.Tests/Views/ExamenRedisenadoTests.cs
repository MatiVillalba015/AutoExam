using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-057 — rediseño visual de la pantalla de Examen, en curso y en corrección.
///
/// RN-65 lo deja claro: es exclusivamente visual. Por eso la mitad de estos tests verifica que
/// algo NO cambió —las opciones, los atajos, el modo revancha, compartir, la lógica de
/// corrección— y la otra mitad, que lo que sí cambió quedó como pide cada criterio.
/// </summary>
public class ExamenRedisenadoTests
{
    private const string Vista = "AutoExam/Views/ExamenView.xaml";
    private const string Plantillas = "AutoExam/Theme/Plantillas.xaml";

    private static XDocument Doc(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static IEnumerable<XElement> Elementos(string ruta = Vista) => Doc(ruta).Descendants();

    private static string Atributo(XElement e, string nombre) => e.Attribute(nombre)?.Value ?? string.Empty;

    private static string Nombre(XElement e) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value ?? string.Empty;

    private static XElement Estilo(string clave) => Doc("AutoExam/Theme/Estilos.xaml")
        .Descendants().First(e => e.Name.LocalName == "Style" &&
            e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave));

    // ------------------------------------------------------------------
    // AC 1 — el aviso de teclado deja de ser un banner
    // ------------------------------------------------------------------

    /// <summary>
    /// El aviso era una tarjeta arriba de la pregunta: lo primero que se veía al entrar a
    /// rendir era una ayuda, no el enunciado. Ahora es una línea al pie.
    /// </summary>
    [Fact]
    public void ElAvisoDeTeclado_EstaAlPie_YNoArriba_US057()
    {
        var doc = Doc(Vista);

        var aviso = doc.Descendants().First(
            e => Atributo(e, "Text").Contains("Podés usar el teclado", StringComparison.Ordinal));

        // Ya no vive dentro de una tarjeta: era un Border con estilo de tarjeta.
        var tarjeta = aviso.Ancestors().FirstOrDefault(
            a => a.Name.LocalName == "Border" && Atributo(a, "Style").Contains("Tarjeta", StringComparison.Ordinal));

        Assert.True(tarjeta is null,
            "El aviso de teclado volvió a ser una tarjeta destacada (US-057).");

        // Y está por debajo de la barra de botones, no por encima de la pregunta.
        var rindiendo = doc.Descendants().First(
            e => Atributo(e, "Visibility").Contains("Rindiendo", StringComparison.Ordinal));

        int filaDelAviso = int.Parse(Atributo(
            aviso.Ancestors().First(a => Atributo(a, "Grid.Row").Length > 0), "Grid.Row"));

        int filaDeLaPregunta = int.Parse(Atributo(
            rindiendo.Descendants().First(e => e.Name.LocalName == "ScrollViewer" &&
                e.Descendants().Any(d => Atributo(d, "Text").Contains("Actual.TextoPregunta", StringComparison.Ordinal))),
            "Grid.Row"));

        Assert.True(filaDelAviso > filaDeLaPregunta,
            "El aviso de teclado tiene que ir debajo de la pregunta, cerca del pie (US-057).");
    }

    /// <summary>
    /// US-036 sigue en pie: la referencia con las teclas y el "Entendido" que se recuerda entre
    /// reinicios no desaparecen, solo dejan de ser un banner.
    /// </summary>
    [Fact]
    public void LaReferenciaDeTeclas_YElEntendido_SiguenExistiendo_US036()
    {
        var lista = Elementos().First(e => e.Name.LocalName == "ItemsControl" &&
            Atributo(e, "ItemsSource").Contains("Atajos", StringComparison.Ordinal));

        Assert.Contains(lista.Descendants(), e => Atributo(e, "Text").Contains("Teclas", StringComparison.Ordinal));
        Assert.Contains(lista.Descendants(), e => Atributo(e, "Text").Contains("Que", StringComparison.Ordinal));

        Assert.Contains(Elementos(),
            e => Atributo(e, "Command").Contains("OcultarAtajosCommand", StringComparison.Ordinal));

        // Solo la primera vez, como pide US-036.
        var bloque = lista.Ancestors().First(a => Atributo(a, "Visibility").Length > 0);
        Assert.Contains("MostrarAtajos", Atributo(bloque, "Visibility"), StringComparison.Ordinal);
    }

    /// <summary>
    /// La referencia se armaba con una tupla con nombres, y los nombres de los elementos de una
    /// tupla no existen en runtime: el binding resolvía contra Item1/Item2 y las pastillas se
    /// dibujaban vacías. Un tipo propio es lo que hace que la ayuda muestre algo.
    /// </summary>
    [Fact]
    public void LaReferenciaDeAtajos_TienePropiedadesQueElBindingPuedeResolver_US036()
    {
        var fila = AtajosExamen.Referencia[0];
        var tipo = fila.GetType();

        Assert.False(tipo.IsGenericType && tipo.Name.StartsWith("ValueTuple", StringComparison.Ordinal),
            "La referencia de atajos volvió a ser una tupla: sus nombres no existen en runtime y " +
            "el binding de WPF no los encuentra.");

        Assert.NotNull(tipo.GetProperty(nameof(AtajoDelExamen.Teclas)));
        Assert.NotNull(tipo.GetProperty(nameof(AtajoDelExamen.Que)));

        Assert.All(AtajosExamen.Referencia, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Teclas));
            Assert.False(string.IsNullOrWhiteSpace(a.Que));
        });
    }

    // ------------------------------------------------------------------
    // AC 2 — el progreso, en una sola línea con su barra
    // ------------------------------------------------------------------

    [Fact]
    public void ElProgreso_VaEnUnaSolaLinea_ConLaBarraDebajo_US057()
    {
        var barra = Elementos().First(e => e.Name.LocalName == "ProgressBar");

        var contenedor = barra.Ancestors().First(a => a.Name.LocalName == "StackPanel");

        var linea = contenedor.Elements().First(e => e.Name.LocalName == "Grid" &&
            e.Descendants().Any(d => Atributo(d, "Text").Contains("ProgresoTexto", StringComparison.Ordinal)));

        // "Pregunta X de N" a la izquierda y los contadores a la derecha, en el mismo renglón.
        Assert.Contains(linea.Descendants(),
            e => Atributo(e, "Text").Contains("Contadores", StringComparison.Ordinal));

        // Y la barra viene después de ese renglón.
        var hijos = contenedor.Elements().ToList();
        Assert.True(hijos.IndexOf(linea) < hijos.FindIndex(h => h.DescendantsAndSelf().Any(d => d.Name.LocalName == "ProgressBar")),
            "La barra de progreso tiene que ir debajo de la línea de progreso (US-057).");
    }

    /// <summary>
    /// El porcentaje se fue: decía exactamente lo mismo que la barra que estaba justo debajo.
    /// </summary>
    [Fact]
    public void ElPorcentajeEncarado_YaNoSeRepiteAlLadoDeLaBarra_US057()
    {
        Assert.DoesNotContain(Elementos(),
            e => Atributo(e, "Text").Contains("encarado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LosContadores_DejanDeEstarJuntoAlReloj_US057()
    {
        var reloj = Elementos().First(e => Atributo(e, "Text").Contains("Binding Cronometro", StringComparison.Ordinal));

        var fila = reloj.Ancestors().First(a => a.Name.LocalName == "StackPanel" &&
            Atributo(a, "Orientation") == "Horizontal");

        Assert.DoesNotContain(fila.Descendants(),
            e => Atributo(e, "Text").Contains("Contadores", StringComparison.Ordinal));
    }

    /// <summary>US-030: la barra sigue fuera del área que scrollea, y sigue con el color de la materia (US-027).</summary>
    [Fact]
    public void LaBarra_SigueFijaYConElColorDeLaMateria_US030_US027()
    {
        var barra = Elementos().First(e => e.Name.LocalName == "ProgressBar");

        Assert.DoesNotContain(barra.Ancestors(), a => a.Name.LocalName == "ScrollViewer");
        Assert.Contains("ColorMateria", Atributo(barra, "Foreground"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC 3 — pastillas del navegador
    // ------------------------------------------------------------------

    /// <summary>
    /// Antes CADA baldosa llevaba el borde del color de su estado, así que diez preguntas sin
    /// visitar eran diez rectángulos con borde marcado y la actual apenas se distinguía.
    /// </summary>
    [Fact]
    public void LasPastillasDePregunta_NoLlevanBordeDeColorPorDefecto_US057()
    {
        var estilo = Elementos().First(e => e.Name.LocalName == "Style" &&
            Atributo(e, "TargetType") == "Button" &&
            Atributo(e, "BasedOn").Contains("BaldosaPregunta", StringComparison.Ordinal));

        var bordePorDefecto = estilo.Elements().First(e => e.Name.LocalName == "Setter" &&
            Atributo(e, "Property") == "BorderBrush");

        Assert.Contains("PincelBorde", Atributo(bordePorDefecto, "Value"), StringComparison.Ordinal);
        Assert.DoesNotContain("EstadoAPincel", Atributo(bordePorDefecto, "Value"), StringComparison.Ordinal);
    }

    [Fact]
    public void SoloLaPreguntaActual_LlevaColorDeAcento_US057()
    {
        var estilo = Elementos().First(e => e.Name.LocalName == "Style" &&
            Atributo(e, "BasedOn").Contains("BaldosaPregunta", StringComparison.Ordinal));

        var actual = estilo.Descendants().Single(e => e.Name.LocalName == "DataTrigger" &&
            Atributo(e, "Binding").Contains("EsActual", StringComparison.Ordinal));

        string valores = string.Join(" ", actual.Elements().Select(s => Atributo(s, "Value")));

        Assert.Contains("PincelMarca", valores, StringComparison.Ordinal);
    }

    /// <summary>
    /// El relleno sigue diciendo el estado: es lo que permite barrer la fila con la vista y
    /// encontrar una salteada sin leer los números.
    /// </summary>
    [Fact]
    public void ElRellenoDeLaPastilla_SigueDiciendoElEstado_US057()
    {
        var estilo = Elementos().First(e => e.Name.LocalName == "Style" &&
            Atributo(e, "BasedOn").Contains("BaldosaPregunta", StringComparison.Ordinal));

        var fondo = estilo.Elements().First(e => e.Name.LocalName == "Setter" &&
            Atributo(e, "Property") == "Background");

        Assert.Contains("EstadoAPincel", Atributo(fondo, "Value"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC 4 — las opciones
    // ------------------------------------------------------------------

    [Fact]
    public void LasOpciones_TienenMasEspacioEntreSi_YEsquinasMasSuaves_US057()
    {
        var estilo = Estilo("OpcionExamen");

        var margen = estilo.Elements().First(e => e.Name.LocalName == "Setter" &&
            Atributo(e, "Property") == "Margin");

        // Era "0,0,0,10".
        double abajo = double.Parse(Atributo(margen, "Value").Split(',')[3],
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(abajo > 10, $"Las opciones siguen a {abajo} px una de otra (US-057 pide más aire).");

        var fondo = estilo.Descendants().First(e => Nombre(e) == "Fondo");
        Assert.Contains("RadioTarjeta", Atributo(fondo, "CornerRadius"), StringComparison.Ordinal);
    }

    /// <summary>
    /// El criterio pide "un tono violeta apenas insinuado (relleno tenue + borde fino), no un
    /// marco grueso y saturado": el borde vuelve a 1 px y baja del tono más luminoso al violeta
    /// de marca.
    /// </summary>
    [Fact]
    public void LaOpcionElegida_NoLlevaMarcoGrueso_US057()
    {
        var elegida = Estilo("OpcionExamen").Descendants()
            .Single(e => e.Name.LocalName == "Trigger" &&
                         Atributo(e, "Property") == "IsChecked" &&
                         Atributo(e, "Value") == "True");

        var grosor = elegida.Elements().FirstOrDefault(s =>
            Atributo(s, "TargetName") == "Fondo" && Atributo(s, "Property") == "BorderThickness");

        Assert.True(grosor is null || Atributo(grosor!, "Value") == "1",
            "La opción elegida volvió a marcarse con un borde grueso (US-057).");

        var borde = elegida.Elements().First(s =>
            Atributo(s, "TargetName") == "Fondo" && Atributo(s, "Property") == "BorderBrush");

        Assert.Equal("{DynamicResource PincelMarca}", Atributo(borde, "Value"));

        // El relleno tenue se queda: es la mitad de "relleno tenue + borde fino".
        var relleno = elegida.Elements().First(s =>
            Atributo(s, "TargetName") == "Fondo" && Atributo(s, "Property") == "Background");

        Assert.Contains("PincelMarcaSuave", Atributo(relleno, "Value"), StringComparison.Ordinal);
    }

    /// <summary>
    /// US-030 pedía que la elegida se distinga "no con un cambio sutil". US-057 aflojó el marco,
    /// así que la barra de acento pasa a ser lo que sostiene esa garantía — más delgada, pero
    /// presente.
    /// </summary>
    [Fact]
    public void LaOpcionElegida_SigueEncendiendoSuBarraDeAcento_US030()
    {
        var elegida = Estilo("OpcionExamen").Descendants()
            .Single(e => e.Name.LocalName == "Trigger" &&
                         Atributo(e, "Property") == "IsChecked" &&
                         Atributo(e, "Value") == "True");

        Assert.Contains(elegida.Elements(), s => Atributo(s, "TargetName") == "Acento");

        var acento = Estilo("OpcionExamen").Descendants().Single(e => Nombre(e) == "Acento");

        double ancho = double.Parse(Atributo(acento, "Width"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(ancho, 1, 4);
    }

    [Fact]
    public void LaLetraDeLaOpcion_VaEnUnaPastillaQueSeLlenaAlElegir_US057()
    {
        var letra = Elementos().First(e => Atributo(e, "Text") == "{Binding Letra}");

        var pastilla = letra.Ancestors().First(a => a.Name.LocalName == "Border");

        var elegida = pastilla.Descendants().First(e => e.Name.LocalName == "DataTrigger" &&
            Atributo(e, "Binding").Contains("Elegida", StringComparison.Ordinal));

        Assert.Contains(elegida.Elements(),
            s => Atributo(s, "Value").Contains("PincelMarca}", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // AC 5 — el anillo de la nota
    // ------------------------------------------------------------------

    /// <summary>
    /// Era un disco de 86 px con borde de 3, fondo teñido y el número pintado del mismo rojo:
    /// al terminar un examen desaprobado lo primero que se veía era una mancha roja.
    /// </summary>
    [Fact]
    public void LaNota_SeDibujaComoAnilloDeProgreso_US057()
    {
        var arco = Elementos().FirstOrDefault(e => e.Name.LocalName == "Ellipse" &&
            Atributo(e, "StrokeDashArray").Contains("NotaFraccion", StringComparison.Ordinal));

        Assert.True(arco is not null, "La nota no se dibuja como anillo de progreso (US-057).");

        // Mismo lenguaje visual que el "Promedio" del Historial (US-044): riel apagado + arco.
        Assert.Contains("FraccionAAnillo", Atributo(arco!, "StrokeDashArray"), StringComparison.Ordinal);
        Assert.Equal("Round", Atributo(arco!, "StrokeStartLineCap"));

        var contenedor = arco.Parent!;

        var riel = contenedor.Elements().First(
            e => e.Name.LocalName == "Ellipse" &&
                 Atributo(e, "Stroke").Contains("PincelBorde", StringComparison.Ordinal));

        // El arco sale del MISMO circulo que el riel. Es lo que impide que se corra hacia
        // adentro y termine tapando la nota, que es el bug que tenia la version con un Path.
        foreach (string medida in new[] { "Width", "Height", "StrokeThickness" })
        {
            Assert.Equal(Atributo(riel, medida), Atributo(arco!, medida));
        }
    }

    [Fact]
    public void ElAnilloDeLaNota_UsaElMismoLenguajeQueElDelHistorial_US044()
    {
        var enExamen = Elementos().First(e => e.Name.LocalName == "Ellipse" &&
            Atributo(e, "StrokeDashArray").Contains("NotaFraccion", StringComparison.Ordinal));

        var enHistorial = Elementos("AutoExam/Views/HistorialView.xaml").First(e => e.Name.LocalName == "Ellipse" &&
            Atributo(e, "StrokeDashArray").Contains("AciertosFraccion", StringComparison.Ordinal));

        foreach (string propiedad in new[] { "StrokeStartLineCap", "StrokeEndLineCap" })
        {
            Assert.Equal(Atributo(enHistorial, propiedad), Atributo(enExamen, propiedad));
        }
    }

    /// <summary>
    /// El color se templa sin inventar un rojo nuevo: queda en el arco —un trazo fino, no un
    /// disco— y el número pasa al gris de texto.
    /// </summary>
    [Fact]
    public void ElNumeroDeLaNota_YaNoSePintaDelColorDeLaNota_US057()
    {
        var numero = Elementos().First(e => Atributo(e, "Text") == "{Binding Nota}");

        Assert.Contains("PincelTexto", Atributo(numero, "Foreground"), StringComparison.Ordinal);
        Assert.DoesNotContain("Aprobado", Atributo(numero, "Foreground"), StringComparison.Ordinal);
    }

    [Fact]
    public void ElArco_SigueDiciendoSiAprobo_US057()
    {
        var arco = Elementos().First(e => e.Name.LocalName == "Ellipse" &&
            Atributo(e, "StrokeDashArray").Contains("NotaFraccion", StringComparison.Ordinal));

        Assert.Contains("AprobadoAPincel", Atributo(arco, "Stroke"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0.4)]
    [InlineData(10, 1)]
    public void LaFraccionDeLaNota_VaDeCeroAUno(int nota, double esperada)
    {
        var vm = new AutoExam.ViewModels.ExamenViewModel(
            new AutoExam.Services.SesionUsuarioService(),
            new TestDoubles.DialogosDeSimulacion(),
            new TestDoubles.NavegacionDeSimulacion())
        {
            Nota = nota,
        };

        Assert.Equal(esperada, vm.NotaFraccion, 3);
    }

    // ------------------------------------------------------------------
    // AC 6 — la tarjeta de una pregunta corregida
    // ------------------------------------------------------------------

    [Fact]
    public void LaTarjetaCorregida_CambiaLaFranjaPorIconoYEtiqueta_US057()
    {
        var plantilla = Doc(Plantillas).Descendants().First(e => e.Name.LocalName == "DataTemplate");

        // La franja a todo lo alto era una columna de 4 px pintada del color del estado.
        Assert.DoesNotContain(plantilla.Descendants(),
            e => e.Name.LocalName == "ColumnDefinition" && Atributo(e, "Width") == "4");

        var etiqueta = plantilla.Descendants().First(
            e => Atributo(e, "Text").Contains("TituloResultado", StringComparison.Ordinal));

        var fila = etiqueta.Parent!;

        Assert.Contains(fila.Elements(), e => e.Name.LocalName == "SymbolIcon");
    }

    [Fact]
    public void ElIcono_DistingueCorrectaDeIncorrecta_US057()
    {
        var plantilla = Doc(Plantillas).Descendants().First(e => e.Name.LocalName == "DataTemplate");

        var estilo = plantilla.Descendants().First(e => e.Name.LocalName == "Style" &&
            Atributo(e, "TargetType").Contains("SymbolIcon", StringComparison.Ordinal));

        var porResultado = estilo.Descendants()
            .Where(e => e.Name.LocalName == "DataTrigger")
            .ToDictionary(e => Atributo(e, "Value"),
                          e => Atributo(e.Elements().First(), "Value"));

        Assert.Contains("Checkmark", porResultado["Correcta"], StringComparison.Ordinal);
        Assert.Contains("Dismiss", porResultado["Incorrecta"], StringComparison.Ordinal);
    }

    /// <summary>
    /// El tinte es un velo del color del estado, no el pincel "suave" usado de fondo. Con el
    /// fondo directo, una pregunta salteada quedaba como un bloque ámbar pleno con el texto
    /// encima ilegible: los dos tonos del pendiente están invertidos respecto de acierto y
    /// error en los dos temas.
    /// </summary>
    [Fact]
    public void ElFondoDeLaTarjeta_QuedaApenasTenido_US057()
    {
        var plantilla = Doc(Plantillas).Descendants().First(e => e.Name.LocalName == "DataTemplate");

        var tarjeta = plantilla.Elements().First(e => e.Name.LocalName == "Border");

        Assert.Contains("PincelTarjeta", Atributo(tarjeta, "Background"), StringComparison.Ordinal);

        var velo = tarjeta.Descendants().First(e => e.Name.LocalName == "Border" &&
            Atributo(e, "Opacity").Length > 0 &&
            Atributo(e, "Background").Contains("EstadoAPincel", StringComparison.Ordinal));

        double opacidad = double.Parse(Atributo(velo, "Opacity"), System.Globalization.CultureInfo.InvariantCulture);

        Assert.InRange(opacidad, 0.01, 0.15);
        Assert.Equal("False", Atributo(velo, "IsHitTestVisible"));
    }

    /// <summary>
    /// El rótulo del estado usa el tono legible sobre la tarjeta. Para pendiente/salteada eso
    /// NO es "PincelPendiente": en los dos temas el par está invertido respecto de acierto y
    /// error, y con el tono fuerte la palabra "Salteada" quedaba prácticamente invisible.
    /// </summary>
    [Theory]
    [InlineData(ResultadoPreguntaEnum.Correcta, "PincelAcierto")]
    [InlineData(ResultadoPreguntaEnum.Incorrecta, "PincelError")]
    [InlineData(ResultadoPreguntaEnum.Salteada, "PincelPendienteSuave")]
    public void ElRotuloDelEstado_UsaElTonoLegible_US057(ResultadoPreguntaEnum resultado, string claveEsperada)
    {
        TestSupport.WpfHost.Invocar(() =>
        {
            AsegurarTokens();

            var conversor = new AutoExam.EstadoAPincelConverter();

            object rotulo = conversor.Convert(
                resultado, typeof(object), "rotulo", System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(System.Windows.Application.Current.TryFindResource(claveEsperada), rotulo);
        });
    }

    /// <summary>
    /// El conversor resuelve los pinceles desde los recursos de la Application. El host de la
    /// suite mergea estilos y plantillas, pero no el juego de tokens (los estilos los consumen
    /// con DynamicResource y no lo necesitan para construirse). Sin tokens, el conversor cae en
    /// gris para todos los estados y cualquier comparación entre dos de ellos pasaría sin
    /// probar nada. Debe llamarse ya dentro del hilo STA del host.
    /// </summary>
    private static void AsegurarTokens()
    {
        TestSupport.WpfHost.AsegurarRecursos();

        var recursos = System.Windows.Application.Current.Resources;

        if (recursos["PincelAcierto"] is null)
        {
            recursos.MergedDictionaries.Add(new System.Windows.ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/AutoExam;component/Theme/Tokens.Oscuro.xaml", UriKind.Absolute),
            });
        }
    }

    /// <summary>
    /// El complemento del test anterior: para pendiente/salteada, el tono del rótulo tiene que
    /// ser distinto del "fuerte" que usa el resto de los estados. Es lo que hace visible la
    /// inversión de la paleta en vez de dejarla como un detalle escondido en el conversor.
    /// </summary>
    [Fact]
    public void ParaSalteada_ElRotulo_NoEsElMismoTonoQueElBorde_US057()
    {
        TestSupport.WpfHost.Invocar(() =>
        {
            AsegurarTokens();

            var conversor = new AutoExam.EstadoAPincelConverter();

            object Con(string? modo) => conversor.Convert(
                ResultadoPreguntaEnum.Salteada, typeof(object), modo, System.Globalization.CultureInfo.InvariantCulture);

            Assert.NotEqual(Con("borde"), Con("rotulo"));

            // Y para los otros dos sí coinciden: la excepción es solo el pendiente.
            object Correcta(string modo) => conversor.Convert(
                ResultadoPreguntaEnum.Correcta, typeof(object), modo, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(Correcta("borde"), Correcta("rotulo"));
        });
    }

    // ------------------------------------------------------------------
    // AC 7 — los controles de la esquina
    // ------------------------------------------------------------------

    [Fact]
    public void Timer_TamanioDeLetra_YCerrar_VanEnUnaSolaFila_US057()
    {
        var reloj = Elementos().First(e => Atributo(e, "Text").Contains("Binding Cronometro", StringComparison.Ordinal));

        var fila = reloj.Ancestors().First(a => a.Name.LocalName == "StackPanel" &&
            Atributo(a, "Orientation") == "Horizontal");

        foreach (string comando in new[]
                 {
                     "DisminuirTextoExamenCommand", "AumentarTextoExamenCommand", "SalirCommand",
                 })
        {
            Assert.Contains(fila.Descendants(),
                e => Atributo(e, "Command").Contains(comando, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ElReloj_SiguePintandoseDeAlerta_US034()
    {
        var reloj = Elementos().First(e => Atributo(e, "Text").Contains("Binding Cronometro", StringComparison.Ordinal));

        var alerta = reloj.Descendants().First(e => e.Name.LocalName == "DataTrigger" &&
            Atributo(e, "Binding").Contains("TiempoCritico", StringComparison.Ordinal));

        Assert.Contains(alerta.Elements(),
            s => Atributo(s, "Value").Contains("PincelError", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // AC 8 / RN-65 — nada más cambia
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("ResponderCommand")]
    [InlineData("SiguienteCommand")]
    [InlineData("AnteriorCommand")]
    [InlineData("SaltearCommand")]
    [InlineData("FinalizarCommand")]
    [InlineData("IrAPreguntaCommand")]
    [InlineData("RevanchaCommand")]
    [InlineData("ExportarCommand")]
    [InlineData("ArmarOtroCommand")]
    [InlineData("SalirCommand")]
    public void TodoLoQueSePodiaHacer_SeSiguePudiendo_RN65(string comando)
    {
        Assert.Contains(Elementos(),
            e => Atributo(e, "Command").Contains(comando, StringComparison.Ordinal));
    }

    [Fact]
    public void LasOpcionesSiguenSaliendoDeLaMismaLista_RN65()
    {
        var lista = Elementos().First(e => e.Name.LocalName == "ItemsControl" &&
            Atributo(e, "ItemsSource") == "{Binding Opciones}");

        Assert.Contains(lista.Descendants(),
            e => e.Name.LocalName == "RadioButton" &&
                 Atributo(e, "Style").Contains("OpcionExamen", StringComparison.Ordinal));
    }

    [Fact]
    public void LaPregunta_SigueEnSuPropiaTarjeta_ConMasPesoQueLasOpciones_US030()
    {
        Assert.Contains(Elementos(),
            e => e.Name.LocalName == "Border" &&
                 Atributo(e, "Style").Contains("TarjetaResumen", StringComparison.Ordinal) &&
                 e.Descendants().Any(d => Atributo(d, "Text").Contains("TextoPregunta", StringComparison.Ordinal)));
    }

    [Fact]
    public void ElTamanioDeLetraDelExamen_SigueSaliendoDelViewModel_US005()
    {
        Assert.Contains(Elementos(),
            e => Atributo(e, "FontSize").Contains("TamanioTextoPregunta", StringComparison.Ordinal));

        Assert.Contains(Elementos(),
            e => Atributo(e, "FontSize").Contains("TamanioTextoOpciones", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // RN-66 — el fondo estándar, pero atenuado
    // ------------------------------------------------------------------

    [Fact]
    public void ElExamen_LlevaElFondoDeLuzVioleta_RN66()
    {
        var canvas = Doc(Vista).Descendants().FirstOrDefault(e => e.Name.LocalName == "Canvas");

        Assert.True(canvas is not null, "La pantalla de examen no tiene el fondo con luz violeta (RN-66).");
        Assert.Equal("False", Atributo(canvas!, "IsHitTestVisible"));
        Assert.Equal(2, canvas!.Descendants().Count(e => e.Name.LocalName == "RadialGradientBrush"));
    }

    /// <summary>
    /// RN-66 es explícita: acá el fondo va atenuado respecto del resto de la app, porque es la
    /// pantalla donde el alumno necesita concentrarse.
    /// </summary>
    [Fact]
    public void EseFondo_EsMasTenueQueEnElRestoDeLaApp_RN66()
    {
        double MayorOpacidad(string vista) => Doc(vista).Descendants()
            .Where(e => e.Name.LocalName == "Ellipse" && Atributo(e, "Opacity").Length > 0)
            .Select(e => double.Parse(Atributo(e, "Opacity"), System.Globalization.CultureInfo.InvariantCulture))
            .Max();

        double enExamen = MayorOpacidad(Vista);

        foreach (string otra in new[]
                 {
                     "AutoExam/Views/HistorialView.xaml",
                     "AutoExam/Views/BibliotecaView.xaml",
                     "AutoExam/Views/AjustesView.xaml",
                 })
        {
            Assert.True(enExamen < MayorOpacidad(otra),
                $"El fondo del examen ({enExamen}) no es más tenue que el de {otra} (RN-66).");
        }
    }
}
