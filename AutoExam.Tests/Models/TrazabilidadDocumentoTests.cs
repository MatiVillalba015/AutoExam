using System.IO;
using System.Text.Json;
using AutoExam.Models;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Models;

/// <summary>
/// RN-24 — un examen armado con varios documentos conserva de cual salio cada pregunta.
///
/// La trazabilidad que ya existia era la pagina. Combinando materiales (US-024) la pagina
/// sola deja de ubicar nada: la pagina 12 existe en los tres apuntes. Por eso la pregunta
/// suma el documento, y la linea que el alumno ve en la correccion lo nombra.
/// </summary>
public class TrazabilidadDocumentoTests
{
    // ------------------------------------------------------------------
    // AC — la referencia de origen dice de que documento salio la pregunta
    // ------------------------------------------------------------------

    [Fact]
    public void ConVariosDocumentos_LaReferenciaNombraElDocumento_RN24()
    {
        var p = new Pregunta { PaginaOrigen = 12, DocumentoOrigen = "Guyton" };

        Assert.Contains("Guyton", p.ReferenciaFuente, StringComparison.Ordinal);
        Assert.Contains("12", p.ReferenciaFuente, StringComparison.Ordinal);
    }

    [Fact]
    public void SinDocumento_LaReferenciaSigueSiendoLaDeSiempre()
    {
        // Un examen de una sola fuente no cambia: repetir el titulo del examen en cada
        // pregunta no aporta trazabilidad, no hay con que confundirlo.
        var p = new Pregunta { PaginaOrigen = 12 };

        Assert.Equal("Pagina 12 del PDF", p.ReferenciaFuente);
    }

    [Fact]
    public void SinPaginaExacta_ElDocumentoIgualUbicaLaPregunta()
    {
        // El modelo no siempre arriesga una pagina. Decir de cual de los tres apuntes salio
        // ya es bastante mas util que no decir nada.
        var p = new Pregunta { PaginaOrigen = 0, DocumentoOrigen = "Lehninger" };

        Assert.Contains("Lehninger", p.ReferenciaFuente, StringComparison.Ordinal);
    }

    [Fact]
    public void SinPaginaNiDocumento_LaReferenciaQuedaVacia_ComoAntes()
    {
        var p = new Pregunta { PaginaOrigen = 0 };

        Assert.Equal(string.Empty, p.ReferenciaFuente);
    }

    [Fact]
    public void ConTramoYDocumento_SeNombranLosDos()
    {
        var p = new Pregunta { PaginaOrigen = 0, PaginasAlcance = "paginas 10 a 40", DocumentoOrigen = "Best" };

        Assert.Contains("Best", p.ReferenciaFuente, StringComparison.Ordinal);
        Assert.Contains("10 a 40", p.ReferenciaFuente, StringComparison.Ordinal);
    }

    [Fact]
    public void ConUnDocumentoQueNoEsPdf_LaReferenciaNoDicePdf()
    {
        // Combinando un .docx con un PDF, decir "del PDF" en una pregunta que salio del
        // Word seria informacion falsa en la correccion.
        var p = new Pregunta { PaginaOrigen = 3, DocumentoOrigen = "Resumen de la cursada.docx" };

        Assert.DoesNotContain("del PDF", p.ReferenciaFuente, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — la referencia sigue estando al revisar el examen en el Historial
    // ------------------------------------------------------------------

    [Fact]
    public void ElDocumentoDeOrigen_SePersisteConElExamen()
    {
        var original = new Pregunta
        {
            TextoPregunta = "¿Que es el potencial de accion?",
            Opciones = new List<string> { "a", "b", "c", "d" },
            PaginaOrigen = 12,
            DocumentoOrigen = "Guyton"
        };

        string json = JsonSerializer.Serialize(original);
        var releida = JsonSerializer.Deserialize<Pregunta>(json);

        Assert.NotNull(releida);
        Assert.Equal("Guyton", releida!.DocumentoOrigen);
    }

    [Fact]
    public void LaRevancha_ConservaElDocumentoDeOrigen()
    {
        // La revancha clona las preguntas del intento original: si el documento se perdiera
        // ahi, la correccion de la segunda vuelta mostraria menos que la primera.
        var original = new Pregunta { PaginaOrigen = 12, DocumentoOrigen = "Guyton" };

        Assert.Equal("Guyton", original.Clonar().DocumentoOrigen);
    }

    // ------------------------------------------------------------------
    // La correccion muestra esa referencia
    // ------------------------------------------------------------------

    [Fact]
    public void LaTarjetaDeCorreccion_MuestraLaReferenciaDeOrigen()
    {
        // Desde US-025 la tarjeta de correccion de una pregunta vive en Theme/Plantillas.xaml
        // y no dentro de ExamenView: la comparten la correccion inmediata y el detalle de un
        // examen del historial, que tiene que verse igual. La garantia no cambio —la
        // referencia de origen se muestra— pero el archivo donde vive, si.
        string xaml = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Theme/Plantillas.xaml"));

        Assert.Contains("ReferenciaFuente", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void LasDosPantallasDeCorreccion_UsanLaMismaTarjeta()
    {
        // Si una de las dos volviera a tener su propia copia del XAML, la igualdad que pide
        // US-025 ("igual que en la pantalla de correccion") duraria hasta el primer retoque.
        foreach (string vista in new[] { "AutoExam/Views/ExamenView.xaml", "AutoExam/Views/HistorialView.xaml" })
        {
            string xaml = File.ReadAllText(ArchivoFuenteHelper.RutaFuente(vista));

            Assert.Contains("TarjetaCorreccion", xaml, StringComparison.Ordinal);
        }
    }
}
