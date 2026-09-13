using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-044 — rediseño visual del Historial.
///
/// Es un pulido de apariencia y animaciones con una sola excepción, que el criterio nombra:
/// desaparece la sección "Escala UBA y datos guardados" y su botón de borrado pasa a estar a
/// la vista, en rojo. Todo lo demás —buscador, resumen, lista, evolución, repaso combinado—
/// tiene que seguir enlazado exactamente a lo mismo, y buena parte de estos tests no verifica
/// lo que se agregó sino eso.
///
/// Lo que no cubren es si se ve bien. Eso hay que mirarlo.
/// </summary>
public class HistorialRedisenadoTests
{
    private const string Vista = "AutoExam/Views/HistorialView.xaml";

    private static XDocument Doc(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    private static XElement Estilo(string clave)
    {
        var estilo = Doc("AutoExam/Theme/Estilos.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                 e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave));

        Assert.True(estilo is not null, $"No se encontró el estilo {clave} en Estilos.xaml.");
        return estilo!;
    }

    private static XElement ListaDeExamenes() => Doc(Vista).Descendants()
        .First(e => e.Name.LocalName == "ListBox" &&
                    (e.Attribute("ItemsSource")?.Value ?? string.Empty)
                        .Contains("ExamenesFiltrados", StringComparison.Ordinal));

    // ------------------------------------------------------------------
    // AC — dos columnas: la lista a la izquierda, el resumen a la derecha
    // ------------------------------------------------------------------

    [Fact]
    public void ElHistorial_SeParteEnDosColumnas_ListaYResumen_US044()
    {
        var lista = ListaDeExamenes();

        // La lista vive en la columna 0 del cuerpo; el ancho fijo de la derecha es el que
        // reserva el lugar del resumen. Antes iba todo apilado en una sola columna.
        var cuerpo = lista.Ancestors()
            .First(a => a.Name.LocalName == "Grid" &&
                        a.Elements().Any(e => e.Name.LocalName == "Grid.ColumnDefinitions"));

        var columnas = cuerpo.Elements()
            .First(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .ToList();

        Assert.Equal(3, columnas.Count);
        Assert.Equal("*", columnas[0].Attribute("Width")?.Value);
        Assert.Equal("380", columnas[2].Attribute("Width")?.Value);
    }

    /// <summary>El arco del anillo: la elipse cuyo trazo se dibuja a guiones.</summary>
    private static XElement ArcoDelAnillo(string vista = Vista) => Doc(vista).Descendants()
        .First(e => e.Name.LocalName == "Ellipse" &&
                    (e.Attribute("StrokeDashArray")?.Value ?? string.Empty)
                        .Contains("FraccionAAnillo", StringComparison.Ordinal));

    [Fact]
    public void ElResumen_TieneElAnilloDeProgresoConElPorcentajeDeAciertos_US044()
    {
        Assert.Contains("AciertosFraccion", ArcoDelAnillo().Attribute("StrokeDashArray")!.Value,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// El arco tiene que salir del MISMO circulo que el riel: mismo tamanio, mismo grosor.
    ///
    /// Antes eran dos cosas distintas —una elipse para el riel y un Path con un ArcSegment para
    /// el arco— metidas en la misma grilla y centradas cada una por su cuenta. Un Path se mide
    /// por el rectangulo de su geometria, y el de un arco corto es un cuadrante: centrarlo lo
    /// corria hacia adentro hasta montarse sobre el numero del medio, y solo coincidia con el
    /// riel al 100%, cuando el rectangulo vuelve a ser el circulo entero. Dibujando el arco como
    /// un guion del trazo de una elipse igual al riel, no hay dos circulos que desalinear.
    /// </summary>
    [Fact]
    public void ElArco_SeDibujaSobreElMismoCirculoQueElRiel_US044()
    {
        var arco = ArcoDelAnillo();

        var riel = arco.Parent!.Elements()
            .First(e => e.Name.LocalName == "Ellipse" && e.Attribute("StrokeDashArray") is null);

        foreach (string medida in new[] { "Width", "Height", "StrokeThickness" })
        {
            Assert.Equal(riel.Attribute(medida)?.Value, arco.Attribute(medida)?.Value);
        }

        // Y ninguno de los dos se alinea por su cuenta: los dos llenan la misma celda.
        Assert.Null(arco.Attribute("HorizontalAlignment"));
        Assert.Null(arco.Attribute("VerticalAlignment"));
    }

    [Fact]
    public void ElArco_ArrancaArriba_NoALasTres_US044()
    {
        var arco = ArcoDelAnillo();

        var giro = arco.Descendants().First(e => e.Name.LocalName == "RotateTransform");

        Assert.Equal("-90", giro.Attribute("Angle")?.Value);
        Assert.Equal("0.5,0.5", arco.Attribute("RenderTransformOrigin")?.Value);
    }

    /// <summary>
    /// El parametro del conversor tiene que describir el anillo que se esta dibujando: si el
    /// radio o el grosor no son los de la elipse, el largo del guion deja de corresponder a la
    /// fraccion y el arco se pasa o se queda corto.
    /// </summary>
    [Fact]
    public void ElParametroDelConversor_CoincideConElAnilloQueDibuja_US044()
    {
        var arco = ArcoDelAnillo();

        double diametro = double.Parse(arco.Attribute("Width")!.Value, CultureInfo.InvariantCulture);
        double grosor = double.Parse(arco.Attribute("StrokeThickness")!.Value, CultureInfo.InvariantCulture);

        string enlace = arco.Attribute("StrokeDashArray")!.Value;

        // Las comillas simples son obligatorias en el XAML: sin ellas, la coma del parametro
        // parte el markup extension en dos y el binding ni siquiera compila.
        var parametro = System.Text.RegularExpressions.Regex
            .Match(enlace, @"ConverterParameter='([\d.]+),([\d.]+)'");

        Assert.True(parametro.Success, $"No se pudo leer el ConverterParameter del anillo: {enlace}");

        Assert.Equal(diametro / 2, double.Parse(parametro.Groups[1].Value, CultureInfo.InvariantCulture), 3);
        Assert.Equal(grosor, double.Parse(parametro.Groups[2].Value, CultureInfo.InvariantCulture), 3);
    }

    [Theory]
    [InlineData("Promedio")]
    [InlineData("Aciertos")]
    [InlineData("MejorNota")]
    [InlineData("Total")]
    public void ElResumen_MuestraLosCuatroNumerosDelCriterio_US044(string dato)
    {
        var resumen = ArcoDelAnillo().Ancestors().First(a => a.Name.LocalName == "Border");

        Assert.Contains(resumen.Descendants(),
            e => e.Name.LocalName == "TextBlock" &&
                 (e.Attribute("Text")?.Value ?? string.Empty)
                     .Contains($"Binding {dato}", StringComparison.Ordinal));
    }

    [Fact]
    public void LaEvolucionPorMateria_QuedaDebajoDelResumen_EnLaMismaColumna_US044()
    {
        var encabezado = Doc(Vista).Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ToggleButton" &&
                                 (e.Attribute("Name")?.Value ?? string.Empty) == "AbrirEvolucion" ||
                                 e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "AbrirEvolucion"));

        Assert.True(encabezado is not null, "No se encontró el encabezado de la evolución (US-033).");

        // Comparte el ScrollViewer de la columna derecha con la tarjeta de resumen.
        var columna = encabezado!.Ancestors()
            .First(a => a.Name.LocalName == "ScrollViewer");

        Assert.Equal("2", columna.Attribute("Grid.Column")?.Value);

        Assert.Contains(columna.Descendants(),
            e => e.Name.LocalName == "Ellipse" &&
                 (e.Attribute("StrokeDashArray")?.Value ?? string.Empty)
                     .Contains("FraccionAAnillo", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // AC — correctas / incorrectas / salteadas como pastillas de color
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Correctas", "PincelAciertoSuave", "PincelAcierto")]
    [InlineData("Incorrectas", "PincelErrorSuave", "PincelError")]
    public void CadaDatoDelExamen_VaEnUnaPastillaDeSuColor_US044(string dato, string fondo, string texto)
    {
        var pastilla = ListaDeExamenes().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("Background")?.Value ?? string.Empty)
                                     .Contains(fondo, StringComparison.Ordinal));

        Assert.True(pastilla is not null, $"Falta la pastilla de {dato} (US-044).");
        Assert.Contains("Pastilla", pastilla!.Attribute("Style")?.Value ?? string.Empty, StringComparison.Ordinal);

        var contenido = pastilla.Descendants().First(e => e.Name.LocalName == "TextBlock");

        Assert.Contains(texto, contenido.Attribute("Foreground")?.Value ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(contenido.Descendants(),
            r => r.Name.LocalName == "Run" &&
                 (r.Attribute("Text")?.Value ?? string.Empty).Contains(dato, StringComparison.Ordinal));
    }

    [Fact]
    public void LasPastillas_NoDependenSoloDelColor_DicenQueSon_US044()
    {
        // Verde y rojo no informan a quien no los distingue. Cada pastilla lleva su palabra.
        string xaml = Fuente(Vista);

        foreach (string palabra in new[] { " correctas", " incorrectas", " salteadas" })
        {
            Assert.Contains(palabra, xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LasPastillas_ReemplazanAlTextoPlano_NoSeSumanAEl_US044()
    {
        // DetalleTexto era la línea "N correctas · N incorrectas · N salteadas · tiempo".
        // Si siguiera enlazada, el mismo dato aparecería dos veces en cada fila. Se miran los
        // enlaces y no el archivo entero: el comentario que explica de dónde viene la pastilla
        // sí lo nombra.
        var enlaces = Doc(Vista).Descendants()
            .SelectMany(e => e.Attributes())
            .Select(a => a.Value)
            .ToList();

        Assert.DoesNotContain(enlaces, v => v.Contains("DetalleTexto", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // AC — se va "Escala UBA y datos guardados"; queda un solo borrado total
    // ------------------------------------------------------------------

    [Fact]
    public void LaSeccionDeEscalaUBA_YaNoEsta_US044()
    {
        // Ningún texto de la pantalla la nombra ya (el comentario que cuenta de dónde salió el
        // botón de borrado sí, y por eso se miran los textos y no el archivo).
        var textos = Doc(Vista).Descendants()
            .Select(e => e.Attribute("Text")?.Value ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(textos, t => t.Contains("Escala UBA", StringComparison.OrdinalIgnoreCase));

        // Y su colección deja de existir en el ViewModel: era su único consumidor.
        Assert.Null(typeof(AutoExam.ViewModels.HistorialViewModel).GetProperty("Escala"));
    }

    [Fact]
    public void BorrarTodoElHistorial_EsElUnicoPuntoDeBorradoTotal_YSeVeDestructivo_US044()
    {
        var botones = Doc(Vista).Descendants()
            .Where(e => e.Name.LocalName == "Button" &&
                        (e.Attribute("Command")?.Value ?? string.Empty)
                            .Contains("BorrarCommand", StringComparison.Ordinal) &&
                        !(e.Attribute("Command")?.Value ?? string.Empty)
                            .Contains("BorrarExamenCommand", StringComparison.Ordinal))
            .ToList();

        var boton = Assert.Single(botones);

        Assert.Contains("BotonDestructivo", boton.Attribute("Style")?.Value ?? string.Empty,
            StringComparison.Ordinal);

        string estilo = Estilo("BotonDestructivo").ToString();

        Assert.Contains("PincelError", estilo, StringComparison.Ordinal);
    }

    [Fact]
    public void BorrarTodoElHistorial_SigueConfirmandoAntesDeBorrar_RN6()
    {
        // El color avisa, no reemplaza al "estás seguro". RN-6 no se toca.
        string codigo = Fuente("AutoExam/ViewModels/HistorialViewModel.cs");

        int comando = codigo.IndexOf("private void Borrar()", StringComparison.Ordinal);
        int confirma = codigo.IndexOf("_dialogos.Confirmar", comando, StringComparison.Ordinal);
        int borra = codigo.IndexOf("_sesion.BorrarHistorial()", comando, StringComparison.Ordinal);
        int recalcula = codigo.IndexOf("Refrescar();", borra, StringComparison.Ordinal);

        Assert.InRange(confirma, comando, borra);
        Assert.True(recalcula > borra, "Después de borrar hay que recalcular las estadísticas (RN-6).");
    }

    // ------------------------------------------------------------------
    // AC — animaciones nuevas
    // ------------------------------------------------------------------

    [Fact]
    public void AlPasarElMousePorUnExamen_SuContenidoSeCorreALaIzquierda_US044()
    {
        var contenido = ListaDeExamenes().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Grid" &&
                                 e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Contenido"));

        Assert.True(contenido is not null, "La tarjeta del examen no agrupa su contenido para desplazarlo (US-044).");

        var desplazamientos = contenido!.Descendants()
            .Where(e => e.Name.LocalName == "DoubleAnimation" &&
                        (e.Attribute("Storyboard.TargetProperty")?.Value ?? string.Empty)
                            .Contains("TranslateTransform.X", StringComparison.Ordinal))
            .Select(e => double.Parse(e.Attribute("To")!.Value, CultureInfo.InvariantCulture))
            .ToList();

        // Se corre hacia la izquierda (negativo) y vuelve a 0 al salir. "Chico y suave": no
        // puede irse más de unos pocos píxeles o la fila se desarma.
        Assert.Contains(desplazamientos, v => v < 0 && v >= -12);
        Assert.Contains(desplazamientos, v => v == 0);

        foreach (var animacion in contenido.Descendants()
                     .Where(e => e.Name.LocalName == "DoubleAnimation"))
        {
            Assert.Contains("StaticResource", animacion.Attribute("Duration")?.Value ?? string.Empty,
                StringComparison.Ordinal);
            Assert.Contains("StaticResource", animacion.Attribute("EasingFunction")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ElDesplazamientoDelExamen_RespetaReducirMovimiento_RN33()
    {
        var contenido = ListaDeExamenes().Descendants()
            .First(e => e.Name.LocalName == "Grid" &&
                        e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Contenido"));

        Assert.Contains(contenido.Descendants(),
            c => c.Name.LocalName == "Condition" &&
                 (c.Attribute("Binding")?.Value ?? string.Empty)
                     .Contains("Animaciones.Reducidas", StringComparison.Ordinal) &&
                 (c.Attribute("Value")?.Value ?? string.Empty) == "False");
    }

    [Fact]
    public void LosBotonesDeLaPantalla_SeTinenSuaveAlPasarElMouse_US044()
    {
        var estilo = Estilo("IconoDeAccion");

        var tinte = estilo.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("Name")?.Value ?? string.Empty) == "Tinte" ||
                                 e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Tinte"));

        Assert.True(tinte is not null, "El botón no tiene capa de tinte que el hover pueda encender (US-044).");

        // El color lo pone cada uso: violeta para navegar, rojo para borrar. El template sólo
        // sabe encenderlo.
        Assert.Contains("TemplateBinding Background", tinte!.Attribute("Background")?.Value ?? string.Empty,
            StringComparison.Ordinal);

        var animaciones = estilo.Descendants()
            .Where(e => e.Name.LocalName == "DoubleAnimation" &&
                        (e.Attribute("Storyboard.TargetName")?.Value ?? string.Empty) == "Tinte")
            .ToList();

        // Aparece gradual y se apaga gradual: sin la salida, el tinte quedaría encendido.
        Assert.Contains(animaciones, a => a.Attribute("To")?.Value == "1");
        Assert.Contains(animaciones, a => a.Attribute("To")?.Value == "0");

        foreach (var animacion in animaciones)
        {
            Assert.Contains("StaticResource", animacion.Attribute("Duration")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LosDosIconosDeCadaExamen_UsanEseBotonConSuColorDeAccion_US044()
    {
        var iconos = ListaDeExamenes().Descendants()
            .Where(e => e.Name.LocalName == "Button" &&
                        (e.Attribute("Style")?.Value ?? string.Empty)
                            .Contains("IconoDeAccion", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, iconos.Count);

        // El de borrar se tiñe de rojo; el de ver el detalle se queda con el violeta del estilo.
        Assert.Contains(iconos, b => (b.Attribute("Background")?.Value ?? string.Empty)
            .Contains("PincelErrorSuave", StringComparison.Ordinal));
    }

    [Fact]
    public void LaEvolucion_SeAbreYSeCierraConTransicion_NoDeGolpe_US044()
    {
        var panel = Doc(Vista).Descendants()
            .First(e => e.Name.LocalName == "DataTrigger" &&
                        (e.Attribute("Binding")?.Value ?? string.Empty)
                            .Contains("AbrirEvolucion", StringComparison.Ordinal));

        var animadas = panel.Descendants()
            .Where(e => e.Name.LocalName == "DoubleAnimation")
            .Select(e => e.Attribute("Storyboard.TargetProperty")!.Value)
            .Distinct()
            .ToList();

        Assert.Contains("MaxHeight", animadas);
        Assert.Contains("Opacity", animadas);

        // Los dos sentidos: abrir y cerrar. Con sólo EnterActions, cerrar volvería a ser un salto.
        Assert.Contains(panel.Elements(), e => e.Name.LocalName.EndsWith("EnterActions", StringComparison.Ordinal));
        Assert.Contains(panel.Elements(), e => e.Name.LocalName.EndsWith("ExitActions", StringComparison.Ordinal));

        foreach (var animacion in panel.Descendants().Where(e => e.Name.LocalName == "DoubleAnimation"))
        {
            Assert.Contains("StaticResource", animacion.Attribute("Duration")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------
    // AC — la luz violeta lejana del fondo
    // ------------------------------------------------------------------

    [Fact]
    public void ElFondo_TieneManchasSuavesDeLuzVioleta_QueNoTapanNada_US044()
    {
        var manchas = Doc(Vista).Descendants()
            .Where(e => e.Name.LocalName == "Ellipse" &&
                        e.Descendants().Any(g => g.Name.LocalName == "RadialGradientBrush"))
            .ToList();

        Assert.NotEmpty(manchas);

        foreach (var mancha in manchas)
        {
            // "Muy sutil": opacidad baja. Y el degradado termina en transparente, así que la
            // mancha no tiene borde ni forma reconocible.
            double opacidad = double.Parse(mancha.Attribute("Opacity")!.Value, CultureInfo.InvariantCulture);

            Assert.InRange(opacidad, 0.01, 0.4);

            var paradas = mancha.Descendants().Where(g => g.Name.LocalName == "GradientStop").ToList();

            Assert.Equal(8, paradas.Last().Attribute("Color")!.Value.TrimStart('#').Length);
        }

        // No reciben mouse: están detrás y no pueden robarle un click a nada.
        var lienzo = manchas[0].Ancestors().First(a => a.Name.LocalName == "Canvas");

        Assert.Equal("False", lienzo.Attribute("IsHitTestVisible")?.Value);
    }

    // ------------------------------------------------------------------
    // AC — el resto del comportamiento no cambia
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Filtro")]
    [InlineData("LimpiarFiltroCommand")]
    [InlineData("ExamenesFiltrados")]
    [InlineData("AvisoSinResultados")]
    [InlineData("MateriasConExamenes")]
    [InlineData("VerEvolucionDeCommand")]
    [InlineData("IrAlRepasoCommand")]
    [InlineData("DestildarExamenesCommand")]
    [InlineData("VerDetalleCommand")]
    [InlineData("BorrarExamenCommand")]
    [InlineData("CompartirExamenCommand")]
    public void ElRedisenio_NoDesengancholNadaDeLoQueYaFuncionaba_US044(string enlace)
    {
        Assert.Contains(enlace, Fuente(Vista), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // El anillo: la fracción y su arco
    // ------------------------------------------------------------------

    private static DoubleCollection Patron(double fraccion, string medidas = "44,9") =>
        (DoubleCollection)new AutoExam.FraccionAAnilloConverter()
            .Convert(fraccion, typeof(DoubleCollection), medidas, CultureInfo.InvariantCulture);

    /// <summary>
    /// El bug que motivo el arreglo: con 23% de aciertos el arco cubria bastante mas que un
    /// 23% del circulo. Lo que se verifica es la cuenta entera, en las unidades reales de
    /// StrokeDashArray —multiplos del grosor del trazo, no pixeles—: el largo visible tiene
    /// que ser exactamente la fraccion de la circunferencia.
    /// </summary>
    [Theory]
    [InlineData(0d)]
    [InlineData(0.23d)]
    [InlineData(0.5d)]
    [InlineData(1d)]
    public void ElLargoDelArco_EsExactamenteLaFraccionDeLaVuelta_US044(double fraccion)
    {
        const double radio = 44;
        const double grosor = 9;

        var patron = Patron(fraccion);

        double vuelta = 2 * Math.PI * radio / grosor;

        Assert.Equal(2, patron.Count);
        Assert.Equal(fraccion * vuelta, patron[0], 6);

        // El hueco es una vuelta entera: sin eso el patron se repite y el anillo se llena de
        // guiones en vez de mostrar un solo arco.
        Assert.Equal(vuelta, patron[1], 6);
    }

    [Fact]
    public void ElArcoDeCeroAciertos_NoDibujaNada_US044()
    {
        Assert.Equal(0d, Patron(0d)[0]);

        // Y la vista lo esconde: con el trazo en cero, las puntas redondeadas igual dibujarian
        // un punto arriba de todo.
        var arco = ArcoDelAnillo();

        var apagado = arco.Descendants().First(e => e.Name.LocalName == "DataTrigger" &&
            (e.Attribute("Binding")?.Value ?? string.Empty).Contains("AciertosFraccion", StringComparison.Ordinal));

        Assert.Equal("0", apagado.Attribute("Value")?.Value);
        Assert.Contains(apagado.Elements(), s => s.Attribute("Value")?.Value == "Collapsed");
    }

    [Theory]
    [InlineData(-0.5d)]
    [InlineData(1.4d)]
    [InlineData(double.NaN)]
    public void UnaFraccionFueraDeRango_NoDesbordaElAnillo_US044(double fraccion)
    {
        var patron = Patron(fraccion);

        Assert.InRange(patron[0], 0, patron[1]);
    }

    /// <summary>
    /// El parametro es "radio,grosor" y se parte antes de parsear: con la coma como separador
    /// decimal de la cultura, parsear "44,9" entero daria 449 y el arco quedaria diez veces
    /// mas largo de lo que corresponde.
    /// </summary>
    [Fact]
    public void ElParametro_SeLeeIgualEnCualquierCultura_US044()
    {
        var enArgentina = new CultureInfo("es-AR");
        var enIngles = CultureInfo.InvariantCulture;

        var uno = (DoubleCollection)new AutoExam.FraccionAAnilloConverter()
            .Convert(0.5d, typeof(DoubleCollection), "44,9", enArgentina);

        var otro = (DoubleCollection)new AutoExam.FraccionAAnilloConverter()
            .Convert(0.5d, typeof(DoubleCollection), "44,9", enIngles);

        Assert.Equal(uno[0], otro[0], 6);
        Assert.Equal(2 * Math.PI * 44 / 9, uno[1], 6);
    }

    /// <summary>
    /// El grosor no puede caer del parametro: las unidades de StrokeDashArray son multiplos del
    /// trazo, asi que un anillo mas fino con el mismo radio necesita un numero mas grande para
    /// cubrir la misma fraccion. Con el grosor fijo, el anillo del examen (37,6) se llenaria
    /// distinto que el del historial (44,9) para la misma fraccion.
    /// </summary>
    [Fact]
    public void ElGrosor_CambiaElLargoDelGuion_ParaLaMismaFraccion_US044()
    {
        var ancho = Patron(0.5d, "44,9");
        var fino = Patron(0.5d, "44,4");

        Assert.True(fino[0] > ancho[0],
            "Con el mismo radio, un trazo mas fino necesita mas unidades para cubrir media vuelta.");
    }
}
