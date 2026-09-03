using System.IO;
using System.Reflection;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-026 (entrada por el asistente), US-028 (tipografía), US-029 (microinteracciones) y
/// US-030 (layout) en la interfaz.
///
/// Son verificaciones estructurales sobre el XAML y sobre los ViewModels: no dicen si algo se
/// ve lindo —eso hay que mirarlo—, pero sí fijan las decisiones que se pueden deshacer sin
/// querer al tocar una vista, y que no dejarían ningún error visible cuando pasara.
/// </summary>
public class RediseñoDePantallasTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    // ------------------------------------------------------------------
    // US-026 / RN-29 — el punto de entrada principal es el asistente
    // ------------------------------------------------------------------

    [Fact]
    public void ElAsistente_OfreceArmarElExamenConExamenesAnteriores_RN29()
    {
        // RN-29 es explícita: la opción tiene que estar siempre disponible acá, sin depender
        // de si el alumno llegó desde el Historial.
        var comandos = Vista("AutoExam/Views/AsistenteView.xaml").Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty)
            .ToList();

        Assert.Contains(comandos, c => c.Contains("UsarExamenesAnterioresCommand", StringComparison.Ordinal));
        Assert.Contains(comandos, c => c.Contains("UsarMaterialCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void EnModoRepaso_SePuedenTildarExamenesSinSalirDelAsistente()
    {
        var casilla = Vista("AutoExam/Views/AsistenteView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "CheckBox" &&
                                 (e.Attribute("IsChecked")?.Value ?? string.Empty).Contains("Seleccionado") &&
                                 e.Ancestors().Any(a =>
                                     (a.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("ExamenesParaRepaso")));

        Assert.True(casilla is not null,
            "El asistente no deja tildar exámenes anteriores: US-026 pide hacerlo sin salir de esta pantalla.");
    }

    [Fact]
    public void LaListaDeExamenes_TieneBuscador()
    {
        // El criterio lo pide explícitamente ("con buscador/filtro si la lista es larga"): el
        // historial puede tener cientos de intentos.
        Assert.Contains("FiltroExamenes", Fuente("AutoExam/Views/AsistenteView.xaml"), StringComparison.Ordinal);
    }

    [Fact]
    public void ElModoRepaso_SalteaElPasoDeAlcance()
    {
        // "Saltando los pasos que no aplican a este modo (no hay alcance de páginas/módulos
        // ni formato de generación con IA)". El salto vive en el ViewModel, no en la vista.
        string codigo = Fuente("AutoExam/ViewModels/AsistenteViewModel.cs");

        Assert.Contains("ModoRepaso && Paso == PrimerPaso", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElModoRepaso_NoOfreceLaOpcionDeImagenesDeIA()
    {
        var casilla = Vista("AutoExam/Views/AsistenteView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "CheckBox" &&
                                 (e.Attribute("IsChecked")?.Value ?? string.Empty).Contains("IncluirImagenes"));

        Assert.True(casilla is not null, "Desapareció la opción de imágenes del modo material.");

        Assert.Contains("ModoMaterial", casilla!.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ModoRepaso")]
    [InlineData("ModoMaterial")]
    [InlineData("ExamenesParaRepaso")]
    [InlineData("ExamenesElegidos")]
    [InlineData("HayExamenesParaRepaso")]
    [InlineData("ResumenRepaso")]
    [InlineData("FiltroExamenes")]
    [InlineData("UsarMaterialCommand")]
    [InlineData("UsarExamenesAnterioresCommand")]
    [InlineData("DestildarExamenesCommand")]
    public void ElAsistente_ExponeLoQueEnlazaElModoRepaso(string miembro)
    {
        bool existe = typeof(AsistenteViewModel)
            .GetProperty(miembro, BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.True(existe,
            $"AsistenteViewModel no expone \"{miembro}\", pero la vista lo enlaza: el control quedaría mudo.");
    }

    // ------------------------------------------------------------------
    // US-028 — tipografía centralizada y con respaldo
    // ------------------------------------------------------------------

    [Fact]
    public void CadaFamiliaTipografica_TerminaEnUnaFuenteDelSistema_US028()
    {
        // El criterio pide que si la fuente nueva no está instalada, la app caiga "de forma
        // prolija a una fuente del sistema similar". En WPF eso se resuelve con una cadena de
        // respaldo, y el último eslabón tiene que existir en todo Windows soportado.
        var familias = Vista("AutoExam/Theme/Estilos.xaml").Descendants()
            .Where(e => e.Name.LocalName == "FontFamily")
            .Select(e => e.Value)
            .ToList();

        Assert.NotEmpty(familias);

        var delSistema = new[] { "Segoe UI", "Consolas", "Courier New", "Arial", "Tahoma" };

        Assert.All(familias, f =>
        {
            string ultima = f.Split(',').Last().Trim();
            Assert.Contains(ultima, delSistema);
        });
    }

    [Fact]
    public void LosTamanios_VivenEnUnSoloLugar_RN32()
    {
        // RN-32: "variables/recursos centralizados de estilo, no valores sueltos repetidos por
        // pantalla". Cada estilo tipográfico tiene que tomar su tamaño del diccionario.
        var estilos = Vista("AutoExam/Theme/Estilos.xaml").Descendants()
            .Where(e => e.Name.LocalName == "Style" &&
                        (e.Attribute("TargetType")?.Value ?? string.Empty) == "TextBlock" &&
                        (e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key")?.Value ?? string.Empty)
                            .StartsWith("Txt", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(estilos);

        foreach (var estilo in estilos)
        {
            var tamanio = estilo.Elements()
                .FirstOrDefault(s => (s.Attribute("Property")?.Value ?? string.Empty) == "FontSize");

            if (tamanio is not null)
            {
                Assert.Contains("DynamicResource", tamanio.Attribute("Value")?.Value ?? string.Empty,
                    StringComparison.Ordinal);
            }
        }
    }

    // ------------------------------------------------------------------
    // US-029 — transición entre pantallas
    // ------------------------------------------------------------------

    [Fact]
    public void ElCambioDePantalla_Anima_US029()
    {
        // El criterio pide que navegar entre pantallas principales no sea un corte seco. La
        // transición ya existía desde US-007; lo que suma US-030 es que el inicio entre por
        // el mismo ContentControl, así que ir y volver del inicio también anima.
        var contenido = Vista("AutoExam/MainWindow.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ContentControl" &&
                                 (e.Attribute("Content")?.Value ?? string.Empty).Contains("Pagina"));

        Assert.True(contenido is not null, "No se encontró el ContentControl de la página activa.");

        bool anima = contenido!.Attributes()
            .Any(a => a.Name.LocalName.Contains("Activa", StringComparison.Ordinal) && a.Value == "True");

        Assert.True(anima, "El cambio de pantalla dejó de animar (US-029).");
    }

    [Fact]
    public void LaTransicionDePantalla_UsaLosParametrosCentralizados_RN33()
    {
        // RN-33: se reutilizan los parámetros ya definidos, no se arma un sistema aparte.
        string codigo = Fuente("AutoExam/Behaviors/TransicionContenido.cs");

        Assert.Contains("DuracionTransicionSeccion", codigo, StringComparison.Ordinal);
        Assert.Contains("SuavizadoSalida", codigo, StringComparison.Ordinal);
        Assert.Contains("Animaciones.Reducidas", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoZoomDeHover_EstaInterpoladoYEsLeve_US029()
    {
        // La primera pasada de US-029 prohibía escalar en el hover; el criterio nuevo lo pide
        // ("un zoom leve y su texto crece mínimamente, de forma suave"). Lo que molestaba era
        // el salto, no el zoom, así que la garantía pasa de "no escalar" a "escalar poco y
        // siempre interpolado con los parámetros centralizados" (RN-33).
        var doc = Vista("AutoExam/Theme/Estilos.xaml");

        var escalasDeHover = doc.Descendants()
            .Where(e => e.Name.LocalName == "MultiTrigger")
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).EndsWith("IsMouseOver", StringComparison.Ordinal)))
            .SelectMany(t => t.Descendants().Where(a => a.Name.LocalName == "DoubleAnimation"))
            .Where(a => (a.Attribute("Storyboard.TargetProperty")?.Value ?? string.Empty)
                .Contains("Scale", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var animacion in escalasDeHover)
        {
            Assert.Contains("StaticResource", animacion.Attribute("Duration")?.Value ?? string.Empty,
                StringComparison.Ordinal);
            Assert.Contains("StaticResource", animacion.Attribute("EasingFunction")?.Value ?? string.Empty,
                StringComparison.Ordinal);

            double destino = double.Parse(animacion.Attribute("To")!.Value,
                System.Globalization.CultureInfo.InvariantCulture);

            Assert.InRange(destino, 1.0, 1.05);
        }
    }

    // ------------------------------------------------------------------
    // US-030 — layout de las pantallas principales
    // ------------------------------------------------------------------

    [Fact]
    public void ElHistorial_MuestraLosExamenesComoTarjetasConFranjaDeMateria_RN34()
    {
        var franja = Vista("AutoExam/Views/HistorialView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("Background")?.Value ?? string.Empty).Contains("ColorMateria"));

        Assert.True(franja is not null,
            "Las tarjetas del historial no llevan la franja del color de la materia (US-030 / RN-34).");
    }

    [Fact]
    public void LaPantallaDeExamen_SeparaLaPreguntaDeLasOpciones_US030()
    {
        string xaml = Fuente("AutoExam/Views/ExamenView.xaml");

        // La pregunta y su imagen viven en una tarjeta propia; antes eran un bloque continuo
        // con las opciones sobre el mismo fondo.
        Assert.Contains("ELEGI UNA OPCION", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void LaBarraDeProgresoDelExamen_QuedaFueraDelAreaQueScrollea_US030()
    {
        var doc = Vista("AutoExam/Views/ExamenView.xaml");

        var barra = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ProgressBar");

        Assert.True(barra is not null, "No hay barra de progreso en la pantalla de examen (US-030).");

        // Si estuviera dentro del ScrollViewer, sería lo primero que desaparece al bajar por
        // un enunciado largo, que es justo lo que el criterio pide evitar.
        bool dentroDelScroll = barra!.Ancestors().Any(a => a.Name.LocalName == "ScrollViewer");

        Assert.False(dentroDelScroll,
            "La barra de progreso scrollea con el contenido: US-030 la pide fija arriba.");
    }

    [Fact]
    public void ElInicio_RespetaElAnchoMaximoYElCentrado_US017_RN35()
    {
        // RN-35: los layouts nuevos se construyen sobre US-017, no la reemplazan. Sin ancho
        // máximo, en un monitor ancho saldrían cuatro tarjetas gigantes estiradas.
        var grilla = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Grid" &&
                                 e.Attribute("MaxWidth") is not null);

        Assert.True(grilla is not null, "La grilla de inicio no tiene ancho máximo (US-017 / RN-35).");
        Assert.Equal("Center", grilla!.Attribute("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void ElInicio_MuestraLosCuatroAccesosEnGrilla_US030()
    {
        var panel = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "UniformGrid");

        Assert.True(panel is not null, "El inicio no usa una grilla para los accesos (US-030).");
        Assert.Equal("2", panel!.Attribute("Columns")?.Value);
    }

    [Fact]
    public void ElInicio_NoIncluyeElExamenComoAcceso()
    {
        // "Examen" no es un destino que uno elija: es donde la app te deja cuando hay un
        // examen para rendir. Sigue alcanzable con Ctrl+3 y se abre solo al generar uno.
        string codigo = Fuente("AutoExam/ViewModels/ShellViewModel.cs");

        int accesos = codigo.Split("new AccesoDeInicio(").Length - 1;

        Assert.Equal(4, accesos);
    }

    [Fact]
    public void DesdeUnaSeccion_SePuedeVolverAlInicio()
    {
        // El inicio no puede ser un callejón sin salida: la navegación pasó de "siempre
        // visible al costado" a "un click de ida y uno de vuelta".
        var comandos = Vista("AutoExam/MainWindow.xaml").Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty);

        Assert.Contains(comandos, c => c.Contains("IrAInicioCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void LosAtajosDeTecladoSiguenLlevandoDirectoACadaSeccion()
    {
        // Guarda de no-regresión de US-004: con el menú en una pantalla aparte, los atajos son
        // lo que evita que llegar a una sección cueste un click más siempre.
        var atajos = Vista("AutoExam/MainWindow.xaml").Descendants()
            .Where(e => e.Name.LocalName == "KeyBinding")
            .Select(e => e.Attribute("CommandParameter")?.Value ?? string.Empty)
            .ToList();

        foreach (string clave in new[] { "libros", "nuevo", "examen", "historial", "ajustes" })
        {
            Assert.Contains(clave, atajos);
        }
    }
}
