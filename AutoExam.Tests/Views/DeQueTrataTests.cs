using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-020 — "de qué trata" un material ya subido, sin abrir el archivo ni generar un examen.
///
/// Lo que estos tests protegen es la regla de gasto (RN-17), que es donde está la decisión: el
/// resumen se genera al tocar el botón y no al subir el archivo, porque cada uno cuesta una
/// petición de la cuota diaria y la mayoría de los materiales nunca necesitan que se los
/// explique. Y una vez generado se guarda, así que abrir el mismo libro dos veces no se cobra
/// dos veces.
///
/// La generación en sí no se prueba acá: depende de una llamada real a Gemini.
/// </summary>
public class DeQueTrataTests
{
    private static XDocument Vista() =>
        XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/BibliotecaView.xaml"));

    // ------------------------------------------------------------------
    // AC — hay un botón visible, y abre un panel
    // ------------------------------------------------------------------

    [Fact]
    public void LaVistaDeLibros_TieneUnBotonParaVerDeQueTrata()
    {
        var boton = Vista().Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "Button" or "ui:Button" &&
                                 (e.Attribute("Command")?.Value ?? string.Empty).Contains("VerDeQueTrata"));

        Assert.True(boton is not null,
            "BibliotecaView.xaml no tiene un botón atado a VerDeQueTrataCommand (US-020).");

        // Sin nombre accesible, un lector de pantalla anuncia solo "botón".
        Assert.False(string.IsNullOrWhiteSpace(
            boton!.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value
            ?? boton.Attribute("Content")?.Value));
    }

    [Fact]
    public void ElPanel_SeMuestraSoloCuandoEstaAbierto()
    {
        bool hay = Vista().Descendants()
            .Any(e => (e.Attribute("Visibility")?.Value ?? string.Empty).Contains("MostrarDeQueTrata"));

        Assert.True(hay, "El panel de 'de qué trata' no está atado a MostrarDeQueTrata.");
    }

    [Fact]
    public void HayUnaAccionParaCerrarlo_QueNoTocaElMaterial()
    {
        // El criterio pide poder cerrarlo y volver a la lista sin que el material cambie: por eso
        // la acción de cerrar es distinta de la de quitar el libro.
        bool cerrar = Vista().Descendants()
            .Any(e => (e.Attribute("Command")?.Value ?? string.Empty).Contains("CerrarDeQueTrata"));

        Assert.True(cerrar, "No hay forma de cerrar el panel de 'de qué trata'.");
    }

    // ------------------------------------------------------------------
    // AC — no bloquea la interfaz: hay estado de carga
    // ------------------------------------------------------------------

    [Fact]
    public void MientrasSeGenera_SeMuestraUnEstadoDeCarga()
    {
        bool anillo = Vista().Descendants()
            .Any(e => e.Name.LocalName.Contains("ProgressRing", StringComparison.Ordinal) &&
                      (e.Attribute("Visibility")?.Value ?? string.Empty).Contains("Resumiendo"));

        Assert.True(anillo,
            "Un material extenso puede tardar; sin indicador la app parece colgada (US-020).");
    }

    // ------------------------------------------------------------------
    // RN-17 — bajo demanda, y no se paga dos veces
    // ------------------------------------------------------------------

    [Fact]
    public void ElResumen_NoSeGeneraAlSubirElMaterial()
    {
        // Si el alta de un libro invocara el resumen, subir diez PDFs gastaría diez peticiones
        // de la cuota diaria antes de que el alumno pida nada.
        string alta = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Services/BibliotecaService.cs"));

        Assert.DoesNotContain("ResumirMaterialAsync", alta, StringComparison.Ordinal);
    }

    [Fact]
    public void ElResumen_SeGuardaConElLibro_ParaNoVolverAPagarlo()
    {
        var libro = new Libro { Titulo = "Fisiologia", DeQueTrata = "Trata de membranas y potenciales." };

        string json = JsonSerializer.Serialize(libro);
        var releido = JsonSerializer.Deserialize<Libro>(json);

        Assert.NotNull(releido);
        Assert.Equal(libro.DeQueTrata, releido!.DeQueTrata);
        Assert.True(releido.TieneResumen);
    }

    [Fact]
    public void UnLibroSinResumen_NoDiceTenerlo()
    {
        Assert.False(new Libro().TieneResumen);
    }

    [Fact]
    public void ElResumenCalculadoDeLaLista_NoSeConfundeConElDeIA()
    {
        // Libro.Resumen es la línea de la lista ("Materia · 300 pags.") y se recalcula sola;
        // Libro.DeQueTrata es el texto generado. Si Resumen se persistiera, quedaría congelado
        // un texto que tiene que seguir a los datos del libro.
        var libro = new Libro { Materia = "Fisiologia", CantidadPaginas = 300 };

        string json = JsonSerializer.Serialize(libro);

        Assert.DoesNotContain("\"Resumen\"", json, StringComparison.Ordinal);
        Assert.Contains("\"DeQueTrata\"", json, StringComparison.Ordinal);
    }
}
