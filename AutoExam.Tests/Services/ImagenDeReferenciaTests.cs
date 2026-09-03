using System.IO;
using System.Xml.Linq;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-018 — algunas preguntas del examen llevan una imagen de referencia del propio material.
///
/// El mecanismo ya existía para PDF: se extraen figuras, se adjuntan al pedido y el prompt le
/// exige al modelo colgar unas cuantas de las preguntas. Lo que agrega este incremento es la
/// <b>proporción</b>: antes era un tercio fijo, y el criterio pide una proporción aleatoria y
/// nunca todas.
///
/// La aleatoriedad es la parte delicada de testear. En vez de correr el sorteo una vez y
/// esperar suerte, estos tests recorren el espacio de resultados posibles y verifican los
/// límites que sí son deterministas: nunca 0 habiendo figuras, nunca más figuras de las que
/// hay, nunca todas las preguntas.
/// </summary>
public class ImagenDeReferenciaTests
{
    private static IEnumerable<int> Sorteos(int preguntas, int figuras, int veces = 400)
        => Enumerable.Range(0, veces)
            .Select(i => GeminiApiService.CuotaDeFiguras(preguntas, figuras, new Random(i)))
            .ToList();

    // ------------------------------------------------------------------
    // AC — "una proporción aleatoria, no todas"
    // ------------------------------------------------------------------

    [Fact]
    public void NuncaTodasLasPreguntas_LlevanImagen()
    {
        // Un examen entero con imagen deja de practicar la lectura, que es el objetivo principal.
        foreach (int c in Sorteos(preguntas: 15, figuras: 15))
        {
            Assert.True(c < 15, $"Se pidieron {c} preguntas con figura sobre 15: son todas.");
        }
    }

    [Fact]
    public void LaProporcion_VariaEntreExamenes()
    {
        // Si siempre diera el mismo número, "aleatoria" sería una palabra sin efecto.
        var distintos = Sorteos(preguntas: 15, figuras: 10).Distinct().ToList();

        Assert.True(distintos.Count > 1,
            "La cantidad de preguntas con figura salió siempre igual: " + string.Join(",", distintos));
    }

    [Fact]
    public void HabiendoFiguras_SiempreSeUsaAlMenosUna()
    {
        // Sin un piso, el modelo tiende a no completar "ImagenReferencia" en ninguna pregunta y
        // el examen sale sin una sola imagen aunque el material esté lleno de esquemas.
        foreach (int c in Sorteos(preguntas: 10, figuras: 4))
        {
            Assert.True(c >= 1, "Habiendo figuras disponibles no se pidió ninguna.");
        }
    }

    // ------------------------------------------------------------------
    // RN-14 — la imagen es un complemento: nunca puede trabar la generación
    // ------------------------------------------------------------------

    [Fact]
    public void SinFiguras_NoSePideNinguna()
    {
        Assert.Equal(0, GeminiApiService.CuotaDeFiguras(cantidadPreguntas: 10, figurasDisponibles: 0));
    }

    [Fact]
    public void NuncaSePidenMasFigurasDeLasQueHay()
    {
        // Pedir más obligaría al modelo a repetir una figura o a inventar un identificador que
        // después no resuelve contra ninguna imagen.
        foreach (int c in Sorteos(preguntas: 15, figuras: 2))
        {
            Assert.True(c <= 2, $"Se pidieron {c} preguntas con figura y solo hay 2 figuras.");
        }
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-3, 5)]
    [InlineData(5, -1)]
    public void LosCasosDegenerados_NoRompenNiDevuelvenNegativos(int preguntas, int figuras)
    {
        int c = GeminiApiService.CuotaDeFiguras(preguntas, figuras, new Random(1));

        Assert.True(c >= 0);
    }

    // ------------------------------------------------------------------
    // US-018 + US-022 — las figuras ya no son solo del PDF
    // ------------------------------------------------------------------

    [Fact]
    public void ElPrompt_NoAfirmaQueLasFigurasSalenDeUnPdf()
    {
        // Desde US-022 una figura puede venir de un .docx. Decirle al modelo "extraidas del PDF"
        // en ese caso es información falsa dentro del prompt.
        string codigo = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Services/GeminiApiService.cs"));

        int inicio = codigo.IndexOf("FIGURAS ADJUNTAS:", StringComparison.Ordinal);
        Assert.True(inicio > 0, "No se encontró la sección de figuras del prompt.");

        string seccion = codigo.Substring(inicio, Math.Min(1200, codigo.Length - inicio));

        Assert.DoesNotContain("figuras extraidas del PDF", seccion, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // AC — la imagen se ve junto al enunciado, y sigue estando en el Historial
    // ------------------------------------------------------------------

    [Fact]
    public void LaVistaDelExamen_MuestraLaImagenDeLaPreguntaAntesDeResponder()
    {
        var doc = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/ExamenView.xaml"));

        var imagen = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Image" &&
                                 (e.Attribute("Source")?.Value ?? string.Empty).Contains("RutaImagenAdjunta"));

        Assert.True(imagen is not null,
            "ExamenView.xaml no muestra la imagen de referencia de la pregunta (US-018).");

        // Uniform y no Fill: una figura estirada deja de ser legible, que es justo lo que el
        // criterio pide evitar ("no un recorte ilegible").
        Assert.Equal("Uniform", imagen!.Attribute("Stretch")?.Value);
    }

    [Fact]
    public void LaImagenDeUnaPregunta_ViveEnLaCarpetaDelExamen_ParaSeguirEnElHistorial()
    {
        // Las imágenes se escriben bajo la carpeta del examen y no en un temporal: es lo que
        // hace que al revisar la corrección meses después la figura siga estando.
        string codigo = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/ViewModels/AsistenteViewModel.cs"));

        Assert.Contains("CarpetaImagenesExamen", codigo, StringComparison.Ordinal);
    }
}
