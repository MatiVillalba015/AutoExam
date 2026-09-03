using System.IO;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// US-021 / RN-18 — hover suave en los cuatro botones principales del menú, y US-029 —
/// que ese hover no dé el salto de zoom.
///
/// El menú cambió de forma en US-030: la barra lateral pegada al borde izquierdo se
/// reemplazó por la grilla de inicio, y el estilo <c>ItemNavegacion</c> se eliminó junto con
/// ella. Lo que NO cambió es la garantía que esta suite protege —los cuatro accesos animan al
/// pasar el mouse, con los parámetros centralizados, y respetan "reducir movimiento"—, así
/// que la suite sigue existiendo apuntando al control que hoy cumple ese rol:
/// <c>TarjetaAcceso</c>.
/// </summary>
public class MenuHoverTests
{
    private static readonly Lazy<string> Estilos = new(() =>
        File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Theme/Estilos.xaml")));

    private static XElement EstiloDelMenu()
    {
        var doc = XDocument.Parse(Estilos.Value);

        var estilo = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                 e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == "TarjetaAcceso"));

        Assert.True(estilo is not null, "No se encontró el estilo TarjetaAcceso en Estilos.xaml.");
        return estilo!;
    }

    /// <summary>Disparos de hover: MultiTrigger con IsMouseOver = True y movimiento no reducido.</summary>
    private static List<XElement> DisparosDeHoverAnimado()
        => EstiloDelMenu().Descendants()
            .Where(e => e.Name.LocalName == "MultiTrigger")
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).EndsWith("IsMouseOver", StringComparison.Ordinal) &&
                (c.Attribute("Value")?.Value ?? string.Empty) == "True"))
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).Contains("MovimientoReducido") &&
                (c.Attribute("Value")?.Value ?? string.Empty) == "False"))
            .ToList();

    // ------------------------------------------------------------------
    // AC — los cuatro accesos del menú usan este estilo
    // ------------------------------------------------------------------

    [Fact]
    public void LosCuatroAccesosDelMenu_UsanElEstiloConHover()
    {
        var inicio = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/InicioView.xaml"));

        var tarjeta = inicio.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Button" &&
                                 (e.Attribute("Style")?.Value ?? string.Empty).Contains("TarjetaAcceso"));

        Assert.True(tarjeta is not null,
            "Las tarjetas del menú no usan TarjetaAcceso, así que no heredan su hover (US-021).");

        // Es un ItemTemplate sobre la colección de accesos: un solo template cubre los
        // cuatro, así que no hay forma de que uno quede sin animación.
        bool desdeLaColeccion = tarjeta!.Ancestors()
            .Any(a => a.Name.LocalName == "ItemsControl" &&
                      (a.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("Accesos"));

        Assert.True(desdeLaColeccion, "La tarjeta del menú no sale de la colección de accesos.");
    }

    [Fact]
    public void ElMenuYaNoEsUnaBarraPegadaAlCostado_US030()
    {
        // US-030 pide los cuatro botones "en una grilla más centrada y espaciada, en vez de
        // pegados a un costado". Si volviera la columna fija con ItemsSource="{Binding
        // Paginas}", el layout viejo estaría de vuelta sin que nada más lo note.
        string ventana = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/MainWindow.xaml"));

        Assert.DoesNotContain("ItemNavegacion", ventana, StringComparison.Ordinal);
    }

    [Fact]
    public void LaGrillaDelInicio_TieneIconoArribaYTextoAbajo_US030()
    {
        var inicio = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/InicioView.xaml"));

        // Un StackPanel vertical (el default) con el ícono primero y el título después es lo
        // que produce "ícono grande arriba, texto abajo".
        var panel = inicio.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "StackPanel" &&
                                 (e.Attribute("Orientation")?.Value ?? "Vertical") == "Vertical" &&
                                 e.Elements().Any(h => h.Name.LocalName.Contains("SymbolIcon", StringComparison.Ordinal)));

        Assert.True(panel is not null, "La tarjeta no apila ícono y texto en vertical (US-030).");

        var hijos = panel!.Elements().ToList();
        int icono = hijos.FindIndex(h => h.Name.LocalName.Contains("SymbolIcon", StringComparison.Ordinal));
        int texto = hijos.FindIndex(h => h.Name.LocalName == "TextBlock");

        Assert.True(icono >= 0 && texto > icono,
            "El texto no va debajo del ícono: US-030 pide ícono grande arriba y texto abajo.");
    }

    // ------------------------------------------------------------------
    // AC — entra y sale suave, con los parámetros centralizados
    // ------------------------------------------------------------------

    [Fact]
    public void ElHover_AnimaAlEntrarYTambienAlSalir()
    {
        var disparo = Assert.Single(DisparosDeHoverAnimado());

        bool entra = disparo.Elements().Any(e => e.Name.LocalName.EndsWith("EnterActions", StringComparison.Ordinal));
        bool sale = disparo.Elements().Any(e => e.Name.LocalName.EndsWith("ExitActions", StringComparison.Ordinal));

        Assert.True(entra, "El hover no anima al entrar el cursor.");
        Assert.True(sale, "El hover no anima al salir: el realce volvería de golpe (US-021).");
    }

    [Fact]
    public void ElHover_TomaDuracionYSuavizadoDeUnRecursoCentralizado_RN18()
    {
        var animaciones = DisparosDeHoverAnimado()
            .SelectMany(t => t.Descendants().Where(d => d.Name.LocalName == "DoubleAnimation"))
            .ToList();

        Assert.NotEmpty(animaciones);

        foreach (var a in animaciones)
        {
            Assert.Contains("StaticResource", a.Attribute("Duration")?.Value ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("StaticResource", a.Attribute("EasingFunction")?.Value ?? string.Empty, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------
    // US-029 — el hover no da el salto de zoom
    // ------------------------------------------------------------------

    [Fact]
    public void ElHover_HaceUnZoomLeveYSuave_US029()
    {
        // Este test decía lo contrario hasta que el criterio cambió, y vale explicar por qué,
        // porque parece una vuelta atrás y no lo es.
        //
        // La primera pasada de US-029 pedía sacar "el pequeño salto/zoom que hoy pasa al pasar
        // el mouse por un botón", y se sacó la escala entera. El criterio nuevo pide "un zoom
        // leve y su texto crece mínimamente, de forma suave". Leídos juntos, lo que molestaba
        // no era escalar sino que el escalado fuera un salto instantáneo: la queja original era
        // sobre el "salto", no sobre el zoom.
        //
        // Así que la garantía se mueve, no desaparece: la escala vuelve, pero sólo interpolada
        // con los parámetros centralizados y en una proporción chica. Un Setter de escala
        // directo —un salto— sigue estando prohibido, y lo cubre el test de abajo.
        var escalas = DisparosDeHoverAnimado()
            .SelectMany(t => t.Descendants().Where(d => d.Name.LocalName == "DoubleAnimation"))
            .Where(a => (a.Attribute("Storyboard.TargetProperty")?.Value ?? string.Empty)
                .Contains("Scale", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(escalas.Count > 0,
            "El hover de la tarjeta del menú no hace ningún zoom: US-029 lo pide explícitamente.");

        foreach (var animacion in escalas)
        {
            Assert.Contains("StaticResource", animacion.Attribute("Duration")?.Value ?? string.Empty,
                StringComparison.Ordinal);
            Assert.Contains("StaticResource", animacion.Attribute("EasingFunction")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ElZoomDelHover_EsLeve_NoUnSalto_US029()
    {
        // "Zoom leve" y "el texto crece mínimamente". Sobre una tarjeta de 190 px, 1.03 son
        // unos 6 px: se percibe como que la tarjeta se acerca. Pasado cierto punto el texto
        // empieza a re-rasterizarse de forma visible y vuelve a leerse como el salto que la
        // primera versión de US-029 pedía sacar.
        var destinos = DisparosDeHoverAnimado()
            .SelectMany(t => t.Descendants().Where(d => d.Name.LocalName == "DoubleAnimation"))
            .Where(a => (a.Attribute("Storyboard.TargetProperty")?.Value ?? string.Empty)
                .Contains("Scale", StringComparison.OrdinalIgnoreCase))
            .Select(a => double.Parse(a.Attribute("To")!.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        Assert.NotEmpty(destinos);
        Assert.All(destinos, d => Assert.InRange(d, 1.0, 1.05));
    }

    [Fact]
    public void NingunEstadoDeHover_CambiaLaEscalaDeGolpe_US029()
    {
        // La escala sólo puede moverse interpolada. Un Setter directo sobre una ScaleTransform
        // dentro de un disparo de hover es exactamente el salto original.
        var saltos = EstiloDelMenu().Descendants()
            .Where(e => e.Name.LocalName is "Trigger" or "MultiTrigger")
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).EndsWith("IsMouseOver", StringComparison.Ordinal)))
            .SelectMany(t => t.Elements().Where(s => s.Name.LocalName == "Setter"))
            .Where(s => (s.Attribute("Property")?.Value ?? string.Empty)
                .Contains("Scale", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(saltos.Count == 0,
            "Hay un Setter de escala en un disparo de hover: eso es el salto instantáneo, no una transición.");
    }

    [Fact]
    public void ElPulsado_TampocoAnimaConReducirMovimiento_RN33()
    {
        // Esta guarda faltaba en el pulsado de todos los templates hasta US-029: el resto de
        // la app ya respetaba "reducir movimiento" pero el botón seguía dando el saltito.
        var pulsados = EstiloDelMenu().Descendants()
            .Where(e => e.Name.LocalName == "MultiTrigger")
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).EndsWith("IsPressed", StringComparison.Ordinal) &&
                (c.Attribute("Value")?.Value ?? string.Empty) == "True"))
            .ToList();

        Assert.NotEmpty(pulsados);

        Assert.All(pulsados, t => Assert.Contains(t.Descendants(), c =>
            c.Name.LocalName == "Condition" &&
            (c.Attribute("Property")?.Value ?? string.Empty).Contains("MovimientoReducido") &&
            (c.Attribute("Value")?.Value ?? string.Empty) == "False"));
    }

    // ------------------------------------------------------------------
    // AC / RN-18 — respeta "reducir movimiento" sin quedarse sin hover
    // ------------------------------------------------------------------

    [Fact]
    public void ConReducirMovimiento_ElHoverNoAnimaPeroSigueExistiendo()
    {
        // Con la preferencia activada el hover no puede desaparecer: sin ninguna señal, no
        // habría forma de saber sobre qué tarjeta está el cursor. Se aplica el estado final
        // de una, sin animar.
        var directo = EstiloDelMenu().Descendants()
            .Where(e => e.Name.LocalName == "MultiTrigger")
            .FirstOrDefault(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).Contains("MovimientoReducido") &&
                (c.Attribute("Value")?.Value ?? string.Empty) == "True"));

        Assert.True(directo is not null,
            "Con 'reducir movimiento' activado la tarjeta se queda sin ninguna señal de hover.");

        Assert.Contains(directo!.Elements(), e => e.Name.LocalName == "Setter");
    }

    // ------------------------------------------------------------------
    // AC — el hover no se confunde con la sección activa
    // ------------------------------------------------------------------

    [Fact]
    public void ElHover_NoSeConfundeConLaSeccionActiva()
    {
        // En el menú nuevo esta confusión ya no puede darse por construcción: las tarjetas
        // viven en la pantalla de inicio, y desde el inicio no se está parado en ninguna
        // sección. La garantía se cumple porque no hay estado "seleccionado" que pintar.
        var estilo = EstiloDelMenu();

        bool tieneEstadoActivo = estilo.Descendants().Any(e =>
            e.Name.LocalName is "Trigger" or "MultiTrigger" &&
            (e.Attribute("Property")?.Value ?? string.Empty) == "IsChecked");

        Assert.False(tieneEstadoActivo,
            "La tarjeta del menú tiene estado 'seleccionada': si lo pintara igual que el hover, " +
            "pasar el mouse simularía estar parado en esa sección (US-021).");
    }
}
