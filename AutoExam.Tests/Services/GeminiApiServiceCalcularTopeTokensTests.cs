using System.Reflection;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// <c>GeminiApiService.CalcularTopeTokens</c> — NFR-24/NFR-28 (specs/02-tech-spec.md, Incremento
/// 3) y specs/03-architecture.md §4.1: el primer lote de un examen debe usar el techo de tokens
/// real del modelo (informado por <c>ListModels</c> y cacheado en <c>_techoDeSalida</c>) en vez
/// del default conservador, sin superar <c>TopeTokensMaximo</c>, y sin perder un tope aprendido
/// más bajo dentro de la misma sesión de proceso.
///
/// Todos los tests de esta clase corren secuencialmente entre sí (xUnit no paraleliza tests
/// dentro de una misma clase), que es lo que hace seguro tocar el estado <c>static</c> de
/// <see cref="GeminiApiService"/> sin una colección compartida — mismo criterio de aislamiento
/// que ya documenta <c>GeminiApiService.ReiniciarAprendizajeDeSesion</c> ("Existe para las
/// pruebas, porque son campos estaticos"). Cada test usa un nombre de modelo único (GUID) para
/// no interferir con el caché <c>_techoDeSalida</c> de otro test.
/// </summary>
public sealed class GeminiApiServiceCalcularTopeTokensTests : IDisposable
{
    private const int TopeTokensPorDefecto = 8192;
    private const int TopeTokensMaximo = 16384;

    public GeminiApiServiceCalcularTopeTokensTests()
    {
        GeminiApiService.ReiniciarAprendizajeDeSesion();
    }

    public void Dispose() => GeminiApiService.ReiniciarAprendizajeDeSesion();

    private static string ModeloUnico() => $"modelo-prueba-{Guid.NewGuid():N}";

    /// <summary>JSON con la forma minima que devuelve ListModels, usado por ParsearListaModelos.</summary>
    private static string RespuestaListModels(string modelo, int outputTokenLimit) =>
        "{\"models\":[{\"name\":\"models/" + modelo + "\"," +
        "\"supportedGenerationMethods\":[\"generateContent\"]," +
        "\"outputTokenLimit\":" + outputTokenLimit + "}]}";

    [Fact]
    public void SinTechoConocido_UsaElDefaultConservador()
    {
        string modelo = ModeloUnico();

        int tope = GeminiApiServiceReflexion.CalcularTopeTokens(modelo);

        Assert.Equal(0, GeminiApiService.TechoDeSalidaConocido(modelo));
        Assert.Equal(TopeTokensPorDefecto, tope);
    }

    [Fact]
    public void ConTechoConocidoPorDebajoDelMaximo_UsaElTechoDelModelo()
    {
        string modelo = ModeloUnico();
        GeminiApiService.ParsearListaModelos(RespuestaListModels(modelo, 12000), out _);

        int tope = GeminiApiServiceReflexion.CalcularTopeTokens(modelo);

        Assert.Equal(12000, tope);
    }

    [Fact]
    public void ConTechoConocidoPorEncimaDelMaximo_TopeaEnElMaximoDeLaApp()
    {
        string modelo = ModeloUnico();
        GeminiApiService.ParsearListaModelos(RespuestaListModels(modelo, 65536), out _);

        int tope = GeminiApiServiceReflexion.CalcularTopeTokens(modelo);

        Assert.Equal(TopeTokensMaximo, tope);
    }

    [Fact]
    public void ConTopeVigenteAprendidoMasBajoQueElTechoDelModelo_UsaElAprendido()
    {
        // Simula, sin red, el estado al que _topeTokensVigente llega cuando el modelo ya
        // rechazo un pedido por el valor del techo (linea ~1339-1349 de GeminiApiService.cs):
        // ese aprendizaje vive en un campo static de proceso, y CalcularTopeTokens debe
        // respetarlo aunque el modelo admita, segun ListModels, un techo mas alto.
        string modelo = ModeloUnico();
        GeminiApiService.ParsearListaModelos(RespuestaListModels(modelo, 65536), out _);
        FijarTopeVigente(8192);

        int tope = GeminiApiServiceReflexion.CalcularTopeTokens(modelo);

        Assert.Equal(8192, tope);
    }

    [Fact]
    public void TrasReiniciarAprendizajeDeSesion_VuelveAUsarElTechoDelModeloSinElTopeAprendido()
    {
        string modelo = ModeloUnico();
        GeminiApiService.ParsearListaModelos(RespuestaListModels(modelo, 12000), out _);
        FijarTopeVigente(4096);

        GeminiApiService.ReiniciarAprendizajeDeSesion();
        int tope = GeminiApiServiceReflexion.CalcularTopeTokens(modelo);

        Assert.Equal(12000, tope);
    }

    private static void FijarTopeVigente(int valor)
    {
        var campo = typeof(GeminiApiService).GetField("_topeTokensVigente", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "GeminiApiService._topeTokensVigente no existe o cambio de nombre — ver specs/02-tech-spec.md, " +
                "Incremento 3, 'Estado real del codigo' (linea ~251).");

        campo.SetValue(null, valor);
    }
}
