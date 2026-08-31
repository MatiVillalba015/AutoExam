using System.Reflection;
using AutoExam.Services;

namespace AutoExam.Tests.Ingesta;

/// <summary>
/// Resolución por reflexión de los tipos que introduce el Módulo M4 (ingesta y alcance) del
/// Incremento 4 — contrato en specs/03-architecture.md (Incremento 4) §4.1/§4.3/§4.4/§4.5.
///
/// Estas suites arrancan EN PARALELO con <c>dev-ingesta-y-alcance</c> (team-roster.yaml):
/// cuando se escriben, ninguno de estos tipos/miembros existe todavía. Se resuelven por nombre
/// contra el ensamblado <c>AutoExam</c> (no por <c>typeof</c>) para que el proyecto de test
/// SIGA COMPILANDO mientras el developer trabaja — mismo criterio que
/// <see cref="AutoExam.Tests.Services.GeminiApiServiceReflexion"/> ya aplicó en el Incremento 3.
///
/// Hasta que M4 aterrice, cada test queda en rojo con un mensaje que nombra exactamente el
/// miembro faltante y la sección del contrato — es el gate de "hecho" de M4, no ruido.
/// </summary>
internal static class ContratoM4
{
    private static readonly Assembly EnsambladoApp = typeof(IDialogos).Assembly;

    public static Type Tipo(string nombreCompleto)
        => EnsambladoApp.GetType(nombreCompleto)
           ?? throw new InvalidOperationException(
               $"El tipo '{nombreCompleto}' no existe en el ensamblado AutoExam — " +
               "lo introduce el Módulo M4 del Incremento 4 (ver specs/03-architecture.md §4.1/§4.3/§4.4/§4.5). " +
               "Test en rojo esperado hasta que dev-ingesta-y-alcance / dev-extraccion-multiformato lo publiquen.");

    public static Type? TipoOpcional(string nombreCompleto) => EnsambladoApp.GetType(nombreCompleto);

    public static MethodInfo Metodo(Type tipo, string nombre, BindingFlags flags)
        => tipo.GetMethod(nombre, flags)
           ?? throw new InvalidOperationException(
               $"{tipo.FullName}.{nombre} no existe o cambió de firma — ver contrato en specs/03-architecture.md (Incremento 4).");

    /// <summary>Primer método (público o no, estático o de instancia) cuya firma de parámetros y
    /// retorno coincide — robusto ante renombres, para contratos donde importa la forma y no el
    /// nombre exacto del miembro.</summary>
    public static MethodInfo? MetodoPorForma(Type tipo, Type retorno, params Type[] parametros)
        => tipo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .FirstOrDefault(m =>
                retorno.IsAssignableFrom(m.ReturnType) &&
                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parametros));
}
