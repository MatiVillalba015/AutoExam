using System.Globalization;
using System.IO;
using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-041 — rediseño del menú principal contra el mockup.
///
/// Es un cambio puramente visual sobre lo que ya construyó US-031: no se tocó ninguna acción,
/// ningún comando y ninguna navegación. Por eso estos tests no verifican comportamiento nuevo
/// sino la forma que el mockup fija y que, si alguien la deshace sin querer, no rompe nada más:
/// las cuatro tarjetas apiladas en una columna, el ícono en su cuadrado a la izquierda,
/// "Generar examen" destacada, y cada examen del panel con su barra de acento y su nota grande.
///
/// Lo que estos tests NO cubren es si se ve bien. Eso hay que mirarlo.
/// </summary>
public class MenuRedisenadoTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    private static XElement Estilo(string clave)
    {
        var estilo = Vista("AutoExam/Theme/Estilos.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Style" &&
                                 e.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == clave));

        Assert.True(estilo is not null, $"No se encontró el estilo {clave} en Estilos.xaml.");
        return estilo!;
    }

    private static XElement Tarjeta() => Vista("AutoExam/Views/InicioView.xaml").Descendants()
        .First(e => e.Name.LocalName == "Button" &&
                    (e.Attribute("Style")?.Value ?? string.Empty).Contains("TarjetaAcceso"));

    // ------------------------------------------------------------------
    // AC — las cuatro tarjetas, apiladas y en orden
    // ------------------------------------------------------------------

    [Fact]
    public void LasTarjetas_VanEnElOrdenDelMockup_US041()
    {
        // El orden lo define la colección, no la vista: la vista solo la recorre. Si alguien
        // reordena los accesos, la columna cambia de orden sin que la vista se entere.
        string shell = Fuente("AutoExam/ViewModels/ShellViewModel.cs");

        int[] posiciones =
        [
            shell.IndexOf("\"Generar examen\"", StringComparison.Ordinal),
            shell.IndexOf("\"Subir material\"", StringComparison.Ordinal),
            shell.IndexOf("\"Ver historial\"", StringComparison.Ordinal),
            shell.IndexOf("\"Ajustes\", \"Settings24\"", StringComparison.Ordinal),
        ];

        Assert.DoesNotContain(-1, posiciones);
        Assert.Equal(posiciones.OrderBy(p => p), posiciones);
    }

    [Fact]
    public void LaTarjeta_AcomodaSuContenidoEnFila_IconoYLuegoTexto_US041()
    {
        var estilo = Estilo("TarjetaAcceso");

        string alto = estilo.Elements()
            .First(s => s.Name.LocalName == "Setter" &&
                        s.Attribute("Property")?.Value == "MinHeight")
            .Attribute("Value")!.Value;

        // 190 px era el bloque con el ícono grande arriba y el texto abajo. Con el cuadrado de
        // ícono al costado del texto la tarjeta ocupa bastante menos, y ésa es la diferencia
        // que US-041 introduce dentro de la misma grilla 2x2.
        Assert.True(int.Parse(alto) <= 120,
            $"La tarjeta sigue midiendo {alto} px de alto: US-041 pone el ícono al lado del texto, no encima.");
    }

    [Fact]
    public void LaTarjeta_NoMuestraNingunDatoQueNoEstuvieraAntes_US041()
    {
        // El criterio lo nombra con todas las letras: nada de "4 libros" ni "2 rendidos" al
        // lado de cada tarjeta. La insignia sigue existiendo en el ViewModel porque la usan
        // otras pantallas; lo que el menú no hace es mostrarla.
        string vista = Fuente("AutoExam/Views/InicioView.xaml");

        Assert.DoesNotContain("Insignia", vista, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — el resumen del encabezado
    // ------------------------------------------------------------------

    [Fact]
    public void ElResumenDeArriba_DiceElPromedio_NoLaCuentaDeMateriales_US041()
    {
        // Es el único número del menú que resume cómo viene el estudio, y es el que el mockup
        // pone al lado del saludo. La cuenta de materiales no dice nada sobre cómo te fue.
        var menu = new InicioViewModel(Array.Empty<AccesoDeInicio>());

        menu.Actualizar(3, new[]
        {
            Rendido(new DateTime(2026, 3, 1), nota: 8),
            Rendido(new DateTime(2026, 3, 5), nota: 3),
        });

        string promedio = 5.5.ToString("0.0", CultureInfo.CurrentCulture);

        Assert.Equal($"{promedio} de promedio en 2 exámenes", menu.Bajada);
    }

    [Fact]
    public void ConUnSoloExamen_ElResumenNoDicePluralYNoSeRompe()
    {
        var menu = new InicioViewModel(Array.Empty<AccesoDeInicio>());

        menu.Actualizar(1, new[] { Rendido(new DateTime(2026, 3, 1), nota: 7) });

        Assert.Equal($"{7.0.ToString("0.0", CultureInfo.CurrentCulture)} de promedio en 1 examen", menu.Bajada);
    }

    [Fact]
    public void SinExamenesRendidos_ElResumenSigueInvitandoAGenerarElPrimero()
    {
        // No se muestra "0,0 de promedio en 0 exámenes": el primer día el menú invita, no
        // informa un promedio que todavía no existe.
        var menu = new InicioViewModel(Array.Empty<AccesoDeInicio>());

        menu.Actualizar(2, Array.Empty<ExamenRendido>());

        Assert.DoesNotContain("promedio", menu.Bajada, StringComparison.OrdinalIgnoreCase);
    }

    private static ExamenRendido Rendido(DateTime fecha, int nota) => new()
    {
        LibroTitulo = "Tp",
        Materia = "Fisiologia",
        Fecha = fecha,
        NotaUBA = nota,
        Aprobado = nota >= 4,
        TotalPreguntas = 10,
        Correctas = nota,
    };

    // ------------------------------------------------------------------
    // AC — el ícono, en su cuadrado con degradado violeta
    // ------------------------------------------------------------------

    [Fact]
    public void ElCuadroDelIcono_UsaElDegradadoVioletaDeLosTokens_US041()
    {
        string fondo = Estilo("CuadroDeIcono").Elements()
            .First(s => s.Name.LocalName == "Setter" &&
                        s.Attribute("Property")?.Value == "Background")
            .Attribute("Value")!.Value;

        Assert.Contains("PincelIconoAccion", fondo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AutoExam/Theme/Tokens.Oscuro.xaml")]
    [InlineData("AutoExam/Theme/Tokens.Claro.xaml")]
    public void ElDegradadoDelIcono_EsUnTokenDeTemaYNoUnColorSueltoEnLaVista(string diccionario)
    {
        // US-027/US-028 dejaron todos los colores en los diccionarios de tema. El degradado
        // nuevo sigue esa regla: si viviera en la vista, el tema claro heredaría el violeta
        // oscuro y el ícono se volvería una mancha.
        var pincel = Vista(diccionario).Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "LinearGradientBrush" &&
                                 e.Attributes().Any(a => a.Name.LocalName == "Key" &&
                                                         a.Value == "PincelIconoAccion"));

        Assert.True(pincel is not null, $"Falta PincelIconoAccion en {diccionario}.");
        Assert.True(pincel!.Elements().Count(g => g.Name.LocalName == "GradientStop") >= 2,
            "PincelIconoAccion no es un degradado: tiene menos de dos paradas.");
    }

    // ------------------------------------------------------------------
    // AC — "Generar examen" se distingue de las otras tres
    // ------------------------------------------------------------------

    [Fact]
    public void SoloGenerarExamen_VieneMarcadaComoPrincipal_US041()
    {
        string shell = Fuente("AutoExam/ViewModels/ShellViewModel.cs");

        // Una sola: el momento en que dos tarjetas se marcan como principales, ninguna lo es.
        Assert.Equal(1, shell.Split("esPrincipal: true").Length - 1);

        int marca = shell.IndexOf("esPrincipal: true", StringComparison.Ordinal);
        int generar = shell.IndexOf("\"Generar examen\"", StringComparison.Ordinal);
        int siguiente = shell.IndexOf("\"Subir material\"", StringComparison.Ordinal);

        Assert.InRange(marca, generar, siguiente);
    }

    [Fact]
    public void LaTarjetaPrincipal_SoloCambiaFondoYBorde_NoSuComportamiento_US041()
    {
        // El criterio es explícito: "no cambia su acción ni contenido, solo el estilo". Un
        // disparador dentro del mismo estilo garantiza que las cuatro comparten el template
        // entero —el mismo hover, el mismo zoom, la misma guarda de movimiento reducido—.
        var disparo = Estilo("TarjetaAcceso").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "DataTrigger" &&
                                 (e.Attribute("Binding")?.Value ?? string.Empty)
                                     .Contains("EsPrincipal", StringComparison.Ordinal));

        Assert.True(disparo is not null, "Nada distingue a la tarjeta principal (US-041).");

        var propiedades = disparo!.Elements()
            .Where(s => s.Name.LocalName == "Setter")
            .Select(s => s.Attribute("Property")?.Value)
            .ToList();

        Assert.Equal(["Background", "BorderBrush"], propiedades);
    }

    [Fact]
    public void LasOtrasTresTarjetas_QuedanConElFondoNeutro_US041()
    {
        // El contraste del mockup sale de que las otras tres NO son violetas. Si el fondo base
        // pasara a ser el de marca, la principal dejaría de distinguirse de nada.
        string fondo = Estilo("TarjetaAcceso").Elements()
            .First(s => s.Name.LocalName == "Setter" &&
                        s.Attribute("Property")?.Value == "Background")
            .Attribute("Value")!.Value;

        Assert.Contains("PincelTarjeta", fondo, StringComparison.Ordinal);
        Assert.DoesNotContain("Marca", fondo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — el panel de últimos exámenes
    // ------------------------------------------------------------------

    private static XElement FilaDeExamen() => Vista("AutoExam/Views/InicioView.xaml").Descendants()
        .First(e => e.Name.LocalName == "Button" &&
                    (e.Attribute("Style")?.Value ?? string.Empty).Contains("FilaDeActividad"));

    [Fact]
    public void CadaExamen_LlevaSuBarraDeAcentoALaIzquierda_US041()
    {
        var barra = FilaDeExamen().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 e.Attribute("Width") is not null &&
                                 (e.Attribute("Background")?.Value ?? string.Empty)
                                     .Contains("ColorMateria", StringComparison.Ordinal));

        Assert.True(barra is not null, "El examen no tiene barra de acento (US-041).");
        Assert.Equal("0", barra!.Attribute("Grid.Column")?.Value);

        // Es una barra, no un bloque: fina y a lo alto de la fila.
        Assert.True(int.Parse(barra.Attribute("Width")!.Value) <= 6,
            "La barra de acento es demasiado ancha para leerse como barra.");
    }

    [Fact]
    public void LaBarraDeAcento_UsaElColorDeLaMateria_NoUnoPropio_US041()
    {
        // US-027 / RN-34: el color de la materia es el mismo en Historial, en Biblioteca y acá.
        // Un color nuevo sólo para el menú rompería esa correspondencia.
        string fondo = FilaDeExamen().Descendants()
            .First(e => e.Name.LocalName == "Border" && e.Attribute("Width") is not null)
            .Attribute("Background")!.Value;

        Assert.Contains("ColorMateria", fondo, StringComparison.Ordinal);
    }

    [Fact]
    public void LaNotaDelExamen_SeMuestraGrandeALaDerechaDeLaFila_US041()
    {
        var nota = FilaDeExamen().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBlock" &&
                                 (e.Attribute("Text")?.Value ?? string.Empty)
                                     .Contains("Nota", StringComparison.Ordinal));

        Assert.True(nota is not null, "La fila no muestra la nota del examen.");

        // "Al costado derecho": última columna de la fila, después del título y el detalle.
        Assert.Equal("2", nota!.Attribute("Grid.Column")?.Value);

        // "Un número grande": bastante más que el cuerpo de texto, que ronda los 14 px.
        string tamanio = nota.Attribute("FontSize")?.Value ?? "0";
        Assert.True(double.Parse(tamanio, System.Globalization.CultureInfo.InvariantCulture) >= 22,
            $"La nota se muestra a {tamanio} px: US-041 la pide como número grande.");
    }

    [Fact]
    public void LaNotaDelExamen_VaEnTonoNeutro_NoEnRojoNiEnVerde_US041()
    {
        // El criterio pide el número "en un tono neutro/blanco acorde a la paleta (no en rojo)".
        // Acá la nota es referencia, no veredicto: el aprobado/aplazo se ve en el Historial y en
        // Resultados, donde tiene con qué compararse. Un rojo grande en el menú se leía como un
        // error de la app antes que como un aplazo.
        string color = FilaDeExamen().Descendants()
            .First(e => e.Name.LocalName == "TextBlock" &&
                        (e.Attribute("Text")?.Value ?? string.Empty).Contains("Nota", StringComparison.Ordinal))
            .Attribute("Foreground")!.Value;

        Assert.DoesNotContain("AprobadoAPincel", color, StringComparison.Ordinal);
        Assert.Contains("PincelTexto", color, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — hover: zoom mínimo y anillo violeta, en las tarjetas y en las fichas
    // ------------------------------------------------------------------

    private static XElement HoverAnimado(string clave)
    {
        var disparo = Estilo(clave).Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "MultiTrigger" &&
                                 e.Descendants().Any(c =>
                                     c.Name.LocalName == "Condition" &&
                                     (c.Attribute("Property")?.Value ?? string.Empty)
                                         .EndsWith("IsMouseOver", StringComparison.Ordinal) &&
                                     (c.Attribute("Value")?.Value ?? string.Empty) == "True") &&
                                 e.Elements().Any(a => a.Name.LocalName.EndsWith("EnterActions", StringComparison.Ordinal)));

        Assert.True(disparo is not null, $"{clave} no anima nada al pasar el mouse.");
        return disparo!;
    }

    [Theory]
    [InlineData("TarjetaAcceso")]
    [InlineData("FilaDeActividad")]
    public void ElHover_EnciendeUnAnilloVioletaAlrededorDelElemento_US041(string clave)
    {
        var estilo = Estilo(clave);

        var anillo = estilo.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("BorderBrush")?.Value ?? string.Empty)
                                     .Contains("PincelMarca", StringComparison.Ordinal) &&
                                 (e.Attribute("Opacity")?.Value ?? string.Empty) == "0");

        Assert.True(anillo is not null,
            $"{clave} no tiene una capa de borde violeta apagada que el hover pueda encender (US-041).");

        // Redondeado como el elemento que rodea, no cuadrado sobre una tarjeta redondeada.
        Assert.Contains("Radio", anillo!.Attribute("CornerRadius")?.Value ?? string.Empty,
            StringComparison.Ordinal);

        string nombre = anillo.Attributes()
            .First(a => a.Name.LocalName == "Name").Value;

        var disparo = HoverAnimado(clave);

        bool entra = disparo.Descendants()
            .Any(a => a.Name.LocalName == "DoubleAnimation" &&
                      (a.Attribute("Storyboard.TargetName")?.Value ?? string.Empty) == nombre &&
                      a.Attribute("To")?.Value == "1");

        bool sale = disparo.Descendants()
            .Any(a => a.Name.LocalName == "DoubleAnimation" &&
                      (a.Attribute("Storyboard.TargetName")?.Value ?? string.Empty) == nombre &&
                      a.Attribute("To")?.Value == "0");

        Assert.True(entra, $"El anillo violeta de {clave} no aparece al entrar el mouse.");
        Assert.True(sale, $"El anillo violeta de {clave} no se apaga al salir: quedaría encendido (US-041).");
    }

    [Theory]
    [InlineData("TarjetaAcceso")]
    [InlineData("FilaDeActividad")]
    public void ElHover_HaceUnZoomMinimoYSuave_EnLosDosElementosDelMenu_US041(string clave)
    {
        var escalas = HoverAnimado(clave).Descendants()
            .Where(a => a.Name.LocalName == "DoubleAnimation" &&
                        (a.Attribute("Storyboard.TargetProperty")?.Value ?? string.Empty)
                            .Contains("Scale", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(escalas.Count >= 2, $"{clave} no escala en el hover, o escala en un solo eje (US-041).");

        foreach (var escala in escalas)
        {
            // "Mínimo, sutil, no exagerado": el tope lo fija US-029 y acá se respeta.
            double destino = double.Parse(escala.Attribute("To")!.Value,
                System.Globalization.CultureInfo.InvariantCulture);

            Assert.InRange(destino, 1.0, 1.03);

            // "Vuelve suavemente": interpolado con los parámetros centralizados (RN-33), nunca
            // con un Setter que lo cambie de golpe.
            Assert.Contains("StaticResource", escala.Attribute("Duration")?.Value ?? string.Empty,
                StringComparison.Ordinal);
            Assert.Contains("StaticResource", escala.Attribute("EasingFunction")?.Value ?? string.Empty,
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("TarjetaAcceso")]
    [InlineData("FilaDeActividad")]
    public void ElZoomDelHover_RespetaReducirMovimiento_RN33(string clave)
    {
        // El zoom es justo el tipo de movimiento que la preferencia del sistema pide evitar.
        // Con ella activa el elemento se sigue resaltando, pero sin crecer.
        var conMovimientoReducido = Estilo(clave).Descendants()
            .Where(e => e.Name.LocalName == "MultiTrigger")
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Property")?.Value ?? string.Empty).Contains("MovimientoReducido") &&
                (c.Attribute("Value")?.Value ?? string.Empty) == "True"))
            .ToList();

        Assert.NotEmpty(conMovimientoReducido);

        foreach (var disparo in conMovimientoReducido)
        {
            Assert.DoesNotContain(disparo.Descendants(),
                a => (a.Attribute("Property")?.Value ?? string.Empty).Contains("Scale", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ------------------------------------------------------------------
    // AC — no se tocó ninguna lógica ni navegación
    // ------------------------------------------------------------------

    [Fact]
    public void ElRedisenio_NoAgregoNingunComandoNuevoAlMenu_US041()
    {
        // El criterio 5 es la restricción principal de la historia: es un cambio visual sobre
        // US-031. Todos los comandos que aparecen en el menú tienen que ser los que ya existían.
        var comandos = Vista("AutoExam/Views/InicioView.xaml").Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .Select(v => v.Contains("Comando", StringComparison.Ordinal) ? "Comando" : v)
            .Distinct()
            .ToList();

        string[] conocidos = ["Comando", "AtajoVerHistorialCommand", "AlternarQueEsCommand"];

        foreach (string comando in comandos)
        {
            Assert.True(conocidos.Any(c => comando.Contains(c, StringComparison.Ordinal)),
                $"El menú usa un comando que US-031 no tenía: {comando}");
        }
    }
}
