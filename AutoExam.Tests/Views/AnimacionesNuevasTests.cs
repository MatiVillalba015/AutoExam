using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-016 — animaciones nuevas en superficies que hasta ahora no animaban (RN-11).
///
/// Las dos superficies que suma este incremento:
///  1. El círculo de la nota en Resultados, con un "pop" de escala. Es el momento en que el
///     alumno ve el resultado y hasta ahora aparecía junto con el resto de la tarjeta.
///  2. La tarjeta de estadísticas de Historial, que aparecía de golpe al dejar de estar vacío.
///
/// Lo que estos tests protegen no es que la animación "se vea linda" —eso no se puede afirmar
/// desde un test estructural— sino las tres condiciones que RN-11 impone y que son fáciles de
/// perder en un refactor: parámetros centralizados, guarda de "reducir movimiento", y que se
/// animen sólo propiedades que no reacomodan el layout.
/// </summary>
public class AnimacionesNuevasTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    /// <summary>Storyboards colgados de un disparo guardado por <c>Animaciones.Reducidas == False</c>.</summary>
    private static List<XElement> AnimacionesGuardadas(XElement raiz)
        => raiz.Descendants()
            .Where(e => e.Name.LocalName == "MultiDataTrigger")
            .Where(t => t.Descendants().Any(c =>
                c.Name.LocalName == "Condition" &&
                (c.Attribute("Binding")?.Value ?? string.Empty).Contains("Animaciones.Reducidas") &&
                (c.Attribute("Value")?.Value ?? string.Empty) == "False"))
            .SelectMany(t => t.Descendants().Where(d => d.Name.LocalName == "DoubleAnimation"))
            .ToList();

    private static XElement CirculoDeLaNota()
    {
        var borde = Vista("AutoExam/Views/ExamenView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("CornerRadius")?.Value ?? string.Empty) == "43");

        Assert.True(borde is not null, "No se encontró el círculo de la nota en ExamenView.xaml.");
        return borde!;
    }

    private static XElement TarjetaDeEstadisticas()
    {
        var borde = Vista("AutoExam/Views/HistorialView.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Border" &&
                                 (e.Attribute("Visibility")?.Value ?? string.Empty).Contains("HayExamenes"));

        Assert.True(borde is not null, "No se encontró la tarjeta de estadísticas en HistorialView.xaml.");
        return borde!;
    }

    // ------------------------------------------------------------------
    // La superficie anima, y sólo cuando corresponde
    // ------------------------------------------------------------------

    [Fact]
    public void ElCirculoDeLaNota_Anima_YRespetaReducirMovimiento()
    {
        var animaciones = AnimacionesGuardadas(CirculoDeLaNota());

        Assert.NotEmpty(animaciones);
    }

    [Fact]
    public void LaTarjetaDeEstadisticas_Anima_YRespetaReducirMovimiento()
    {
        var animaciones = AnimacionesGuardadas(TarjetaDeEstadisticas());

        Assert.NotEmpty(animaciones);
    }

    // ------------------------------------------------------------------
    // RN-11 — parámetros centralizados, no números sueltos
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("circulo")]
    [InlineData("estadisticas")]
    public void LasAnimacionesNuevas_TomanDuracionYSuavizadoDeUnRecursoCentralizado_RN11(string superficie)
    {
        var animaciones = AnimacionesGuardadas(
            superficie == "circulo" ? CirculoDeLaNota() : TarjetaDeEstadisticas());

        Assert.NotEmpty(animaciones);

        foreach (var a in animaciones)
        {
            string duracion = a.Attribute("Duration")?.Value ?? string.Empty;
            string suavizado = a.Attribute("EasingFunction")?.Value ?? string.Empty;

            Assert.True(duracion.Contains("StaticResource", StringComparison.Ordinal),
                $"La duración debería salir de un recurso centralizado y dice \"{duracion}\" (RN-11).");
            Assert.True(suavizado.Contains("StaticResource", StringComparison.Ordinal),
                $"El suavizado debería salir de un recurso centralizado y dice \"{suavizado}\" (RN-11).");
        }
    }

    // ------------------------------------------------------------------
    // AC — no bloquea ni reacomoda: sólo Opacity y transforms
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("circulo")]
    [InlineData("estadisticas")]
    public void LasAnimacionesNuevas_NoTocanPropiedadesQueReacomodanElLayout(string superficie)
    {
        var animaciones = AnimacionesGuardadas(
            superficie == "circulo" ? CirculoDeLaNota() : TarjetaDeEstadisticas());

        foreach (var a in animaciones)
        {
            string objetivo = a.Attributes()
                .FirstOrDefault(x => x.Name.LocalName.EndsWith("TargetProperty", StringComparison.Ordinal))?.Value
                ?? string.Empty;

            bool aceptable = objetivo == "Opacity"
                             || objetivo.Contains("RenderTransform", StringComparison.Ordinal);

            Assert.True(aceptable,
                $"Animar \"{objetivo}\" reacomoda el layout o repinta de más; se esperaba Opacity o un RenderTransform.");
        }
    }

    /// <summary>
    /// Una animación de escala necesita un origen al centro: sin él, WPF escala desde la esquina
    /// superior izquierda y el círculo "salta" en diagonal en vez de crecer en su lugar.
    /// </summary>
    [Fact]
    public void ElCirculoDeLaNota_EscalaDesdeSuCentro()
    {
        var borde = CirculoDeLaNota();

        bool escala = borde.Descendants().Any(e => e.Name.LocalName == "ScaleTransform");
        Assert.True(escala, "El círculo de la nota debería tener un ScaleTransform.");

        Assert.Equal("0.5,0.5", borde.Attribute("RenderTransformOrigin")?.Value);
    }
}
