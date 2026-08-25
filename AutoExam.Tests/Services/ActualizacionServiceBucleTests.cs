using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// Regresión de <see cref="ActualizacionService.EsBucleDeActualizacion"/> y
/// <see cref="ActualizacionService.AnotarIntento(AppConfig, string)"/> — NFR-13/AC-T5
/// (specs/02-tech-spec.md) y contrato de arquitectura en specs/03-architecture.md §1.3/§6 (R-4).
///
/// Ambos métodos son <c>public static</c> a propósito ("para poder probarla sin levantar
/// ventanas", ver comentarios en ActualizacionService.cs) — no requieren mocks ni arrancar WPF.
/// <c>MaxIntentosPorVersion = 2</c> es una constante privada del servicio: estos tests fijan su
/// valor observable (2) como contrato de regresión, no lo reimplementan.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class ActualizacionServiceBucleTests
{
    // ------------------------------------------------------------------
    // EsBucleDeActualizacion
    // ------------------------------------------------------------------

    [Fact]
    public void EsBucleDeActualizacion_ConConfigNulo_DevuelveFalse()
    {
        bool resultado = ActualizacionService.EsBucleDeActualizacion(null!, "1.2.3");

        Assert.False(resultado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EsBucleDeActualizacion_ConVersionOfrecidaVaciaONula_DevuelveFalse(string? versionOfrecida)
    {
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.2.3",
            IntentosDeActualizacion = 5,
        };

        bool resultado = ActualizacionService.EsBucleDeActualizacion(config, versionOfrecida);

        Assert.False(resultado);
    }

    [Fact]
    public void EsBucleDeActualizacion_ConVersionDistintaALaUltimaIntentada_DevuelveFalse()
    {
        // Muchos intentos, pero sobre OTRA version: no es la version que se esta por ofrecer
        // ahora, asi que no hay bucle que cortar todavia.
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.0.0",
            IntentosDeActualizacion = 10,
        };

        bool resultado = ActualizacionService.EsBucleDeActualizacion(config, "1.2.3");

        Assert.False(resultado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EsBucleDeActualizacion_ConMismaVersionYMenosIntentosQueElMaximo_DevuelveFalse(int intentos)
    {
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.2.3",
            IntentosDeActualizacion = intentos,
        };

        bool resultado = ActualizacionService.EsBucleDeActualizacion(config, "1.2.3");

        Assert.False(resultado);
    }

    [Fact]
    public void EsBucleDeActualizacion_ConMismaVersionYIntentosIgualAlMaximo_DevuelveTrue()
    {
        // MaxIntentosPorVersion = 2: al segundo intento fallido ya se considera bucle.
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.2.3",
            IntentosDeActualizacion = 2,
        };

        bool resultado = ActualizacionService.EsBucleDeActualizacion(config, "1.2.3");

        Assert.True(resultado);
    }

    [Fact]
    public void EsBucleDeActualizacion_ConMismaVersionYMasIntentosQueElMaximo_DevuelveTrue()
    {
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.2.3",
            IntentosDeActualizacion = 7,
        };

        bool resultado = ActualizacionService.EsBucleDeActualizacion(config, "1.2.3");

        Assert.True(resultado);
    }

    [Fact]
    public void EsBucleDeActualizacion_ComparaVersionConSensibilidadAMayusculas()
    {
        // La comparacion es Ordinal (no OrdinalIgnoreCase): documentamos el comportamiento
        // real, no lo que "deberia" ser. Un cambio de casing no cuenta como misma version.
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.2.3-RC",
            IntentosDeActualizacion = 5,
        };

        bool resultado = ActualizacionService.EsBucleDeActualizacion(config, "1.2.3-rc");

        Assert.False(resultado);
    }

    // ------------------------------------------------------------------
    // AnotarIntento
    // ------------------------------------------------------------------

    [Fact]
    public void AnotarIntento_ConConfigNulo_NoLanza()
    {
        var excepcion = Record.Exception(() => ActualizacionService.AnotarIntento(null!, "1.2.3"));

        Assert.Null(excepcion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnotarIntento_ConVersionVaciaONula_NoModificaLaConfig(string? version)
    {
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.0.0",
            IntentosDeActualizacion = 3,
        };

        ActualizacionService.AnotarIntento(config, version!);

        Assert.Equal("1.0.0", config.UltimaVersionIntentada);
        Assert.Equal(3, config.IntentosDeActualizacion);
    }

    [Fact]
    public void AnotarIntento_PrimerIntentoDeUnaVersionNueva_DejaElContadorEnUno()
    {
        var config = new AppConfig(); // UltimaVersionIntentada = "", IntentosDeActualizacion = 0

        ActualizacionService.AnotarIntento(config, "1.2.3");

        Assert.Equal("1.2.3", config.UltimaVersionIntentada);
        Assert.Equal(1, config.IntentosDeActualizacion);
    }

    [Fact]
    public void AnotarIntento_MismaVersionQueElUltimoIntento_IncrementaElContador()
    {
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.2.3",
            IntentosDeActualizacion = 1,
        };

        ActualizacionService.AnotarIntento(config, "1.2.3");

        Assert.Equal("1.2.3", config.UltimaVersionIntentada);
        Assert.Equal(2, config.IntentosDeActualizacion);
    }

    [Fact]
    public void AnotarIntento_VersionDistintaALaUltimaIntentada_ReiniciaElContadorEnUno()
    {
        // Sube una version nueva (o se corrigio el paquete): el contador de la version
        // anterior no debe arrastrarse, sino no se podria distinguir "bucle real" de
        // "version nueva legitima".
        var config = new AppConfig
        {
            UltimaVersionIntentada = "1.2.3",
            IntentosDeActualizacion = 2,
        };

        ActualizacionService.AnotarIntento(config, "1.3.0");

        Assert.Equal("1.3.0", config.UltimaVersionIntentada);
        Assert.Equal(1, config.IntentosDeActualizacion);
    }

    [Fact]
    public void AnotarIntentoDosVeces_SobreLaMismaVersion_DisparaLaDeteccionDeBucle()
    {
        // Test de integracion entre los dos metodos: reproduce exactamente el escenario que
        // motiva NFR-13 — un paquete que anuncia una version que nunca instala de verdad.
        var config = new AppConfig();

        ActualizacionService.AnotarIntento(config, "1.2.3");
        Assert.False(ActualizacionService.EsBucleDeActualizacion(config, "1.2.3"));

        ActualizacionService.AnotarIntento(config, "1.2.3");
        Assert.True(ActualizacionService.EsBucleDeActualizacion(config, "1.2.3"));
    }
}
