using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-030 — los criterios de layout que se sumaron después de la primera pasada: jerarquía de
/// profundidad entre tarjetas, peso visual de la pregunta frente a las opciones, estado de
/// "opción elegida" que se vea de verdad, centrado vertical cuando sobra alto, y espaciado del
/// texto de ayuda. Más el acento de color por materia en el examen (US-027).
///
/// Son verificaciones estructurales: fijan las decisiones, no dicen si se ven bien.
/// </summary>
public class JerarquiaVisualTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    private static XElement Estilo(string clave) =>
        Vista("AutoExam/Theme/Estilos.xaml").Descendants()
            .Single(e => e.Name.LocalName == "Style" &&
                         e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave));

    // ------------------------------------------------------------------
    // Jerarquía de tarjetas
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("TarjetaSeleccionable", "SombraTarjetaSuave")]
    [InlineData("Tarjeta", "SombraTarjeta")]
    [InlineData("TarjetaResumen", "SombraTarjetaElevada")]
    public void HayTresNivelesDeProfundidadDeTarjeta_US030(string estilo, string sombra)
    {
        var efecto = Estilo(estilo).Elements()
            .FirstOrDefault(s => (s.Attribute("Property")?.Value ?? string.Empty) == "Effect");

        Assert.True(efecto is not null, $"El estilo {estilo} no define profundidad propia.");
        Assert.Contains(sombra, efecto!.Attribute("Value")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AutoExam/Theme/Tokens.Claro.xaml")]
    [InlineData("AutoExam/Theme/Tokens.Oscuro.xaml")]
    public void LosTresNiveles_EstanDefinidosEnLosDosTemas(string tokens)
    {
        var claves = Vista(tokens).Descendants()
            .Where(e => e.Name.LocalName == "DropShadowEffect")
            .Select(e => e.Attributes().First(a => a.Name.LocalName == "Key").Value)
            .ToList();

        Assert.Contains("SombraTarjetaSuave", claves);
        Assert.Contains("SombraTarjeta", claves);
        Assert.Contains("SombraTarjetaElevada", claves);
    }

    [Theory]
    [InlineData("AutoExam/Theme/Tokens.Claro.xaml")]
    [InlineData("AutoExam/Theme/Tokens.Oscuro.xaml")]
    public void LosTresNiveles_SonRealmenteDistintosYVanEnOrden(string tokens)
    {
        // Tres claves con el mismo valor cumplirían el test de arriba y no producirían
        // ninguna jerarquía visible. Lo que importa es que suave < normal < elevada.
        var sombras = Vista(tokens).Descendants()
            .Where(e => e.Name.LocalName == "DropShadowEffect")
            .ToDictionary(
                e => e.Attributes().First(a => a.Name.LocalName == "Key").Value,
                e => double.Parse(e.Attribute("Opacity")!.Value, System.Globalization.CultureInfo.InvariantCulture));

        Assert.True(sombras["SombraTarjetaSuave"] < sombras["SombraTarjeta"],
            "La tarjeta seleccionable no se ve más al ras que la informativa.");
        Assert.True(sombras["SombraTarjeta"] < sombras["SombraTarjetaElevada"],
            "El resumen no se ve más elevado que una tarjeta informativa.");
    }

    [Fact]
    public void LosTresNiveles_SeUsanDeVerdad_NoSoloEstanDefinidos_US030()
    {
        // El criterio compara tarjetas entre sí: tres estilos declarados y ninguno aplicado
        // no producen ninguna jerarquía. Cada nivel tiene que estar en uso en alguna pantalla.
        string[] vistas =
        {
            "AutoExam/Views/AsistenteView.xaml",
            "AutoExam/Views/ExamenView.xaml",
            "AutoExam/Views/HistorialView.xaml",
            "AutoExam/Views/InicioView.xaml",
        };

        string todo = string.Concat(vistas.Select(Fuente));

        Assert.Contains("TarjetaResumen", todo, StringComparison.Ordinal);
        Assert.Contains("TarjetaSeleccionable", todo, StringComparison.Ordinal);
        // El nivel del medio también, aunque sea el default histórico.
        Assert.Contains("StaticResource Tarjeta}", todo, StringComparison.Ordinal);
    }

    [Fact]
    public void LasTarjetasDelHistorial_VanAlNivelSeleccionable()
    {
        // Se tildan para armar un repaso, y además son muchas juntas: con la sombra de una
        // tarjeta informativa la lista entera se ensucia.
        var tarjeta = Vista("AutoExam/Views/HistorialView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 e.Elements().Any(h => h.Name.LocalName == "Grid") &&
                                 (e.Attribute("Effect")?.Value ?? string.Empty).Contains("SombraTarjetaSuave"));

        Assert.True(tarjeta is not null,
            "Las tarjetas del historial no usan el nivel de profundidad de tarjeta seleccionable.");
    }

    // ------------------------------------------------------------------
    // Examen: pregunta vs. opciones, y opción elegida
    // ------------------------------------------------------------------

    [Fact]
    public void LaPregunta_PesaMasQueLasOpciones_US030()
    {
        // El criterio pide que "la pregunta tenga más peso visual que las opciones". La
        // pregunta va en el nivel más alto de la jerarquía; las opciones tienen su propio
        // template, sin sombra.
        var tarjetaDeLaPregunta = Vista("AutoExam/Views/ExamenView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("Style")?.Value ?? string.Empty).Contains("TarjetaResumen") &&
                                 e.Descendants().Any(d =>
                                     (d.Attribute("Text")?.Value ?? string.Empty).Contains("TextoPregunta")));

        Assert.True(tarjetaDeLaPregunta is not null,
            "El enunciado no está en el nivel de profundidad más alto: se vería igual que una opción.");
    }

    [Fact]
    public void LaOpcionElegida_SeDistingueConAlgoMasQueUnCambioDeTono_US030()
    {
        // El criterio es explícito: "claramente distinto al resto, no un cambio sutil". Un
        // fondo tenue más un borde de 2 px obliga a comparar dos tonos parecidos entre sí;
        // la barra de acento se ve sin comparar nada.
        var elegida = Estilo("OpcionExamen").Descendants()
            .Single(e => e.Name.LocalName == "Trigger" &&
                         (e.Attribute("Property")?.Value ?? string.Empty) == "IsChecked" &&
                         (e.Attribute("Value")?.Value ?? string.Empty) == "True");

        bool acento = elegida.Elements().Any(s =>
            (s.Attribute("TargetName")?.Value ?? string.Empty) == "Acento");

        Assert.True(acento, "La opción elegida no enciende la barra de acento (US-030).");
    }

    [Fact]
    public void LaBarraDeAcento_NoCorreElTextoDeLaOpcion()
    {
        // Va superpuesta en el Grid del template, no como una columna del contenido: si
        // ocupara lugar, elegir una opción movería su texto y las cuatro tarjetas bailarían
        // cada vez que se cambia de respuesta.
        var acento = Estilo("OpcionExamen").Descendants()
            .Single(e => (e.Attribute("Name")?.Value ??
                          e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value ?? string.Empty) == "Acento");

        Assert.Equal("False", acento.Attribute("IsHitTestVisible")?.Value);
        // Arranca invisible por opacidad, no por Visibility: colapsarlo sí cambiaría el layout.
        Assert.Equal("0", acento.Attribute("Opacity")?.Value);
    }

    [Fact]
    public void ElAcentoDeLaOpcion_NoUsaLosColoresDeCorrectoNiIncorrecto()
    {
        // Mientras se responde todavía no hay nada correcto ni incorrecto que señalar: pintar
        // la elegida de verde o rojo diría algo que la app no sabe.
        var acento = Estilo("OpcionExamen").Descendants()
            .Single(e => e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Acento"));

        string fondo = acento.Attribute("Background")?.Value ?? string.Empty;

        Assert.DoesNotContain("Acierto", fondo, StringComparison.Ordinal);
        Assert.DoesNotContain("Error", fondo, StringComparison.Ordinal);
        Assert.Contains("PincelMarca", fondo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // US-027 — el color de la materia como acento del examen
    // ------------------------------------------------------------------

    [Fact]
    public void ElExamen_LlevaElColorDeSuMateria_US027()
    {
        var doc = Vista("AutoExam/Views/ExamenView.xaml");

        var barra = doc.Descendants().Single(e => e.Name.LocalName == "ProgressBar");

        Assert.Contains("ColorMateria", barra.Attribute("Foreground")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ElColorDelExamen_SeResuelveAlDibujar_NoSeCopia_RN30()
    {
        // RN-30: el color es un atributo de la Materia. Si el ExamenViewModel lo guardara en
        // un campo al iniciar el examen, cambiarle el color a la materia no se reflejaría.
        string codigo = Fuente("AutoExam/ViewModels/ExamenViewModel.cs");

        Assert.Contains("public string ColorMateria => PaletaMaterias.ColorDe(", codigo,
            StringComparison.Ordinal);

        // Y se vuelve a notificar cuando la paleta cambia, para que se repinte en vivo.
        Assert.Contains("PaletaMaterias.Cambio +=", codigo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Centrado vertical cuando el contenido no llena la ventana
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("AutoExam/Views/AsistenteView.xaml")]
    [InlineData("AutoExam/Views/InicioView.xaml")]
    public void ElContenidoCorto_SeCentraALoAlto_US030(string ruta)
    {
        // Poner VerticalAlignment="Center" a secas no hace nada dentro de un ScrollViewer:
        // mide su contenido con alto infinito, así que al hijo no le queda espacio sobrante
        // que repartir. Atar MinHeight al alto visible del propio ScrollViewer es lo que se
        // lo da, y desaparece solo cuando el contenido no entra.
        var contenedor = Vista(ruta).Descendants()
            .FirstOrDefault(e => (e.Attribute("MinHeight")?.Value ?? string.Empty)
                .Contains("ViewportHeight", StringComparison.Ordinal));

        Assert.True(contenedor is not null,
            $"{ruta} no centra a lo alto: el contenido corto queda pegado arriba (US-030).");
    }

    [Fact]
    public void CadaPasoDelAsistente_SeCentra()
    {
        // El bloque exterior tiene el alto; cada paso tiene que pedir centrarse dentro de él.
        var pasos = Vista("AutoExam/Views/AsistenteView.xaml").Descendants()
            .Where(e => e.Name.LocalName == "StackPanel" &&
                        e.Elements().Any(h => h.Name.LocalName == "StackPanel.Style") &&
                        e.Descendants().Any(d => d.Name.LocalName == "DataTrigger" &&
                                                 (d.Attribute("Binding")?.Value ?? string.Empty).Contains("Paso")))
            .ToList();

        Assert.Equal(3, pasos.Count);
        Assert.All(pasos, p => Assert.Equal("Center", p.Attribute("VerticalAlignment")?.Value));
    }

    // ------------------------------------------------------------------
    // Texto de ayuda debajo de un rótulo de sección
    // ------------------------------------------------------------------

    [Fact]
    public void ElTextoDeAyuda_TieneSuPropioEstilo_ConInterlineado_US030()
    {
        var ayuda = Estilo("TxtAyuda");

        var interlineado = ayuda.Elements()
            .FirstOrDefault(s => (s.Attribute("Property")?.Value ?? string.Empty) == "LineHeight");

        Assert.True(interlineado is not null, "TxtAyuda no define interlineado.");

        // Sin esto WPF trata LineHeight como un mínimo y el interlineado real lo sigue
        // fijando la fuente: el valor no tendría ningún efecto visible.
        Assert.Contains(ayuda.Elements(), s =>
            (s.Attribute("Property")?.Value ?? string.Empty) == "LineStackingStrategy");

        var margen = ayuda.Elements()
            .FirstOrDefault(s => (s.Attribute("Property")?.Value ?? string.Empty) == "Margin");

        Assert.True(margen is not null, "TxtAyuda no separa el texto del rótulo de arriba.");
    }

    [Fact]
    public void LasAclaracionesDelAsistente_UsanEseEstilo()
    {
        // El criterio nombra "las aclaraciones de Capítulos o Materia" como el caso concreto.
        // Que sea un estilo y no márgenes a mano es lo que hace que el ritmo sea el mismo en
        // las tres, que era el problema: cada una traía su propio margen.
        string xaml = Fuente("AutoExam/Views/AsistenteView.xaml");

        int usos = xaml.Split("StaticResource TxtAyuda").Length - 1;

        Assert.True(usos >= 3, $"Sólo {usos} aclaración(es) usan TxtAyuda; se esperaban al menos 3.");
    }
}
