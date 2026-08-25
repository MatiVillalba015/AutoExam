using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// Regresión de <see cref="ActualizacionService.PaqueteDisponible"/> (specs/03-architecture.md
/// §1.3/§6 R-4). Es la comprobación previa que evita ofrecer una actualización cuyo ZIP no está
/// publicado (ver AlComprobar/PuedeOfrecer en ActualizacionService.cs).
///
/// La rama de validación de URL es lógica pura (sin red) y se prueba como unit test. Las ramas
/// de respuesta HTTP real (200/404) no tienen punto de inyección — <c>Http</c> es un
/// <c>HttpClient</c> estático privado del servicio — así que se prueban como tests de
/// integración contra endpoints estables de GitHub (mismo host que ya usa el pipeline real,
/// ver specs/03-architecture.md §4.1 paso 9). Se agrupan con <see cref="Trait"/> para poder
/// filtrarlos en un entorno sin red si hiciera falta, sin duplicar la cobertura de la rama
/// sin red de arriba.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class ActualizacionServicePaqueteDisponibleTests
{
    // ------------------------------------------------------------------
    // Rama sin red: validación de URL (unit)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PaqueteDisponible_ConUrlNulaOVacia_DevuelveFalseSinTocarLaRed(string? url)
    {
        bool disponible = ActualizacionService.PaqueteDisponible(url, out string motivo);

        Assert.False(disponible);
        Assert.Contains("direccion del paquete", motivo);
    }

    // ------------------------------------------------------------------
    // Rama con red: respuesta real de GitHub (integración)
    // ------------------------------------------------------------------

    [Trait("Categoria", "Red")]
    [Fact]
    public void PaqueteDisponible_ConUrlQueExiste_DevuelveTrue()
    {
        // Mismo manifiesto que usa ActualizacionService.UrlManifiesto en produccion: si esto
        // deja de responder 200, el pipeline real (specs/03-architecture.md §4.1 paso 9)
        // tambien se rompe, asi que es una referencia estable a proposito.
        bool disponible = ActualizacionService.PaqueteDisponible(
            ActualizacionService.UrlManifiesto, out string motivo);

        Assert.True(disponible);
        Assert.Equal(string.Empty, motivo);
    }

    [Trait("Categoria", "Red")]
    [Fact]
    public void PaqueteDisponible_ConUrlQueDaNotFound_DevuelveFalseConMotivo404()
    {
        string urlInexistente =
            "https://raw.githubusercontent.com/MatiVillalba015/AutoExam/main/"
            + "no-existe-este-paquete-de-test-autoexam.zip";

        bool disponible = ActualizacionService.PaqueteDisponible(urlInexistente, out string motivo);

        Assert.False(disponible);
        Assert.Contains("404", motivo);
    }
}
