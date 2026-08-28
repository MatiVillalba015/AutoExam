using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// <see cref="DiagnosticoGeneracion"/> — contrato de US-009 / AC-T30 (specs/02-tech-spec.md,
/// Incremento 3) y specs/03-architecture.md §4.1: la clase ya existe, este incremento le agrega
/// dos campos públicos simples (<see cref="DiagnosticoGeneracion.LotesTruncados"/>,
/// <see cref="DiagnosticoGeneracion.CuotaDiariaDetectada"/>), sin motor de reglas nuevo.
///
/// Contract-first: estos tests referencian directamente los dos campos nuevos tal como los fija
/// la arquitectura, aunque <c>GeminiApiService.cs</c> todavía no los declare al momento de
/// escribir este archivo — el propio build del proyecto es el gate que confirma que el developer
/// implementó el contrato exacto (mismos nombres, mismos tipos).
/// </summary>
public class DiagnosticoGeneracionTests
{
    // ------------------------------------------------------------------
    // Resumen() / Registrar() — comportamiento ya existente, sin cambios de contrato
    // ------------------------------------------------------------------

    [Fact]
    public void Resumen_SinNotas_DevuelveMarcadorSinDetalle()
    {
        var diagnostico = new DiagnosticoGeneracion();

        Assert.Equal("  (sin detalle)", diagnostico.Resumen());
    }

    [Fact]
    public void Registrar_UnaNota_AparaceEnElResumenConVineta()
    {
        var diagnostico = new DiagnosticoGeneracion();

        diagnostico.Registrar("Lote 1: truncado por techo de tokens.");

        Assert.Equal("  · Lote 1: truncado por techo de tokens.", diagnostico.Resumen());
    }

    [Fact]
    public void Registrar_VariasNotas_QuedanUnaPorLineaEnOrden()
    {
        var diagnostico = new DiagnosticoGeneracion();

        diagnostico.Registrar("primera");
        diagnostico.Registrar("segunda");

        string esperado = "  · primera" + Environment.NewLine + "  · segunda";
        Assert.Equal(esperado, diagnostico.Resumen());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_NotaNulaOVacia_NoSeAgrega(string? nota)
    {
        var diagnostico = new DiagnosticoGeneracion();

        diagnostico.Registrar(nota!);

        Assert.Empty(diagnostico.Notas);
    }

    [Fact]
    public void Registrar_MasDeDoceNotas_IgnoraLasQueSuperanElTope()
    {
        var diagnostico = new DiagnosticoGeneracion();

        for (int i = 0; i < 20; i++)
        {
            diagnostico.Registrar($"nota {i}");
        }

        Assert.Equal(12, diagnostico.Notas.Count);
        Assert.Equal("nota 0", diagnostico.Notas[0]);
        Assert.Equal("nota 11", diagnostico.Notas[11]);
    }

    // ------------------------------------------------------------------
    // Campos nuevos de US-009 (specs/03-architecture.md §4.1)
    // ------------------------------------------------------------------

    [Fact]
    public void LotesTruncados_PorDefecto_EsCero()
    {
        var diagnostico = new DiagnosticoGeneracion();

        Assert.Equal(0, diagnostico.LotesTruncados);
    }

    [Fact]
    public void LotesTruncados_EsSettablePublico()
    {
        var diagnostico = new DiagnosticoGeneracion
        {
            LotesTruncados = 3
        };

        Assert.Equal(3, diagnostico.LotesTruncados);
    }

    [Fact]
    public void CuotaDiariaDetectada_PorDefecto_EsFalse()
    {
        var diagnostico = new DiagnosticoGeneracion();

        Assert.False(diagnostico.CuotaDiariaDetectada);
    }

    [Fact]
    public void CuotaDiariaDetectada_EsSettablePublico()
    {
        var diagnostico = new DiagnosticoGeneracion
        {
            CuotaDiariaDetectada = true
        };

        Assert.True(diagnostico.CuotaDiariaDetectada);
    }
}
