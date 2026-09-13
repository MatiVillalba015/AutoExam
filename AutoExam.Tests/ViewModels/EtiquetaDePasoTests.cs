using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;
using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-043 — la etiqueta chica que va arriba de la tarjeta del paso ("01 MATERIAL").
///
/// Va en su propia clase porque construir el asistente toca <c>RutasApp.Raiz</c> y hay que
/// compartir la colección aislada con el resto de la suite que redirige la raíz; los tests
/// estructurales de US-043 no la necesitan y quedan sueltos en
/// <see cref="Views.AsistenteRedisenadoTests"/>.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class EtiquetaDePasoTests
{
    private static AsistenteViewModel NuevoAsistente()
    {
        var sesion = new SesionUsuarioService();
        sesion.Cargar();

        return new AsistenteViewModel(
            new BibliotecaService(), new PdfExtractorService(), new GeminiApiService(),
            sesion, new DialogosDeSimulacion(), new NavegacionDeSimulacion());
    }

    [Theory]
    [InlineData(1, "01", "MATERIAL")]
    [InlineData(2, "02", "ALCANCE")]
    [InlineData(3, "03", "FORMATO")]
    public void LaEtiqueta_DiceElPasoConCeroAdelanteYEnMayusculas_US043(int paso, string numero, string nombre)
    {
        var asistente = NuevoAsistente();

        asistente.Paso = paso;

        Assert.Equal(numero, asistente.PasoNumerado);
        Assert.Equal(nombre, asistente.PasoNombrado);
    }

    [Fact]
    public void LaEtiqueta_NoInventaNingunNombre_SaleDelMismoPasoQueElRiel_US043()
    {
        // No es un texto nuevo: es el título que el riel ya muestra, en mayúsculas. Si alguien
        // renombrara un paso, la etiqueta lo sigue sola.
        var asistente = NuevoAsistente();

        foreach (var paso in asistente.Pasos)
        {
            asistente.Paso = paso.Numero;

            Assert.Equal(paso.Titulo.ToUpperInvariant(), asistente.PasoNombrado);
        }
    }
}
