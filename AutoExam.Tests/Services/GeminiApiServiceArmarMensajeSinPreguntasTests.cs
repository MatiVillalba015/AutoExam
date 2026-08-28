using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// <c>GeminiApiService.ArmarMensajeSinPreguntas</c> — AC-T30 (specs/02-tech-spec.md, Incremento
/// 3) y specs/03-architecture.md §4.1: distingue causa externa (cuota diaria) de causa
/// app-controlable (techo de tokens / truncado) anteponiendo una de dos oraciones fijas antes
/// del encabezado genérico ya existente, priorizando cuota si ambas ocurrieron.
///
/// Lógica pura de armado de texto — no dispara ninguna llamada de red, no necesita doble de
/// Gemini (a diferencia de AC-T27/AC-T29/AC-T31, ver nota al final de este archivo).
/// </summary>
public class GeminiApiServiceArmarMensajeSinPreguntasTests
{
    private static SolicitudGeneracion Solicitud(string modelo = "gemini-2.0-flash") => new()
    {
        Modelo = modelo
    };

    [Fact]
    public void CuotaDiariaDetectada_AnteponeOracionDeCuotaExterna()
    {
        var diagnostico = new DiagnosticoGeneracion { CuotaDiariaDetectada = true };

        string mensaje = GeminiApiServiceReflexion.ArmarMensajeSinPreguntas(Solicitud(), diagnostico);

        Assert.StartsWith(
            "No se pudo completar el examen porque se agoto la cuota diaria de Gemini de tu " +
            "clave — esto no se arregla reintentando en la app; cargá otra clave en Ajustes o " +
            "esperá al día siguiente.",
            mensaje);
    }

    [Fact]
    public void SoloLotesTruncados_AnteponeOracionDeTechoDeTokens()
    {
        var diagnostico = new DiagnosticoGeneracion { LotesTruncados = 2 };

        string mensaje = GeminiApiServiceReflexion.ArmarMensajeSinPreguntas(Solicitud(), diagnostico);

        Assert.StartsWith(
            "No se pudo completar el examen porque varias respuestas de Gemini llegaron " +
            "cortadas antes de terminar — bajar la cantidad de preguntas por peticion en " +
            "Ajustes suele resolverlo.",
            mensaje);
    }

    [Fact]
    public void CuotaYTruncadosAmbos_PriorizaOracionDeCuotaSobreTruncado()
    {
        var diagnostico = new DiagnosticoGeneracion
        {
            CuotaDiariaDetectada = true,
            LotesTruncados = 3
        };

        string mensaje = GeminiApiServiceReflexion.ArmarMensajeSinPreguntas(Solicitud(), diagnostico);

        Assert.StartsWith("No se pudo completar el examen porque se agoto la cuota diaria", mensaje);
        Assert.DoesNotContain("llegaron cortadas antes de terminar", mensaje);
    }

    [Fact]
    public void SinCuotaNiTruncados_MantieneEncabezadoGenericoSinOracionNueva()
    {
        var diagnostico = new DiagnosticoGeneracion();

        string mensaje = GeminiApiServiceReflexion.ArmarMensajeSinPreguntas(Solicitud("gemini-2.0-flash"), diagnostico);

        Assert.StartsWith("Gemini no devolvio ninguna pregunta valida con el modelo \"gemini-2.0-flash\".", mensaje);
        Assert.DoesNotContain("cuota diaria", mensaje);
        Assert.DoesNotContain("llegaron cortadas antes de terminar", mensaje);
    }

    [Fact]
    public void MensajeFinal_SiempreIncluyeElNombreDelModeloYElResumenDelDiagnostico()
    {
        var diagnostico = new DiagnosticoGeneracion();
        diagnostico.Registrar("Lote 1: sin preguntas mapeables.");

        string mensaje = GeminiApiServiceReflexion.ArmarMensajeSinPreguntas(Solicitud("mi-modelo-de-prueba"), diagnostico);

        Assert.Contains("mi-modelo-de-prueba", mensaje);
        Assert.Contains("Detalle de los intentos:", mensaje);
        Assert.Contains("Lote 1: sin preguntas mapeables.", mensaje);
    }

    // Nota de cobertura (specs/02-tech-spec.md, Incremento 3):
    // AC-T27/AC-T29/AC-T31 (tasa de exito real de un examen de 30 preguntas contra la API real,
    // reintento ante 429/truncado en vivo, repetibilidad entre corridas) no se automatizan en
    // esta suite: requieren un doble HTTP que simule truncado/429 de Gemini (el propio
    // NFR-25 del tech-spec lo deja como alternativa explicita a la API real) y su costo de
    // mantenimiento no está justificado dentro del alcance de este incremento ("codigo simple,
    // estilo trainee"). Quedan como verificación manual/QA contra la API real (ver limitación ya
    // documentada en specs/03-architecture.md, Incremento 3, "Riesgos técnicos").
}
