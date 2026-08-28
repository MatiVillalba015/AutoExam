using System.Reflection;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// <c>ArmarMensajeSinPreguntas</c> y <c>CalcularTopeTokens</c> son <c>private static</c> a
/// propósito en <see cref="GeminiApiService"/> (lógica interna, sin necesidad de exponerla al
/// resto de la app) — se invocan acá por reflection porque son pura lógica de texto/cálculo, sin
/// red de por medio, y no vale la pena bajarles la visibilidad (ni sumar una interfaz nueva) solo
/// para poder probarlas: mismo criterio "sin abstracciones nuevas" que ya fija
/// specs/02-tech-spec.md, Incremento 3, "Restricción transversal de estilo de código".
///
/// Si el nombre/firma de alguno de los dos métodos cambia, el <c>InvalidOperationException</c>
/// de abajo señala exactamente cuál — más claro que un <see cref="NullReferenceException"/> de
/// reflection a ciegas.
/// </summary>
internal static class GeminiApiServiceReflexion
{
    public static string ArmarMensajeSinPreguntas(SolicitudGeneracion solicitud, DiagnosticoGeneracion diagnostico)
    {
        var metodo = typeof(GeminiApiService).GetMethod(
            "ArmarMensajeSinPreguntas",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "GeminiApiService.ArmarMensajeSinPreguntas no existe o cambió de firma — " +
                "ver contrato en specs/03-architecture.md §4.1.");

        return (string)metodo.Invoke(null, new object[] { solicitud, diagnostico })!;
    }

    public static int CalcularTopeTokens(string modelo)
    {
        var metodo = typeof(GeminiApiService).GetMethod(
            "CalcularTopeTokens",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "GeminiApiService.CalcularTopeTokens no existe o cambió de firma — " +
                "ver specs/02-tech-spec.md, Incremento 3, 'Estado real del código' (líneas 1430+).");

        return (int)metodo.Invoke(null, new object[] { modelo })!;
    }
}
