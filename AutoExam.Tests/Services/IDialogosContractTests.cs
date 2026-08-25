using System.Linq;
using System.Reflection;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// Pruebas de CONTRATO sobre <see cref="IDialogos"/> (specs/03-architecture.md §4.2) —
/// AC-T6/AC-T7 (specs/02-tech-spec.md), alcance de test-dev-dialogos-tema en
/// specs/team-roster.yaml.
///
/// Alcance deliberado: forma del contrato (tipos de retorno / parámetros) y compatibilidad
/// estructural de <see cref="DialogoService"/> con la interfaz, todo vía reflection — nunca se
/// instancia ni se invoca <see cref="DialogoService"/> acá, porque su implementación (hoy
/// <c>MessageBox.Show</c>, mañana una ventana/ContentDialog de WPF-UI del US-002) abre una
/// ventana modal real y colgaría cualquier corrida headless de CI. El comportamiento no-throw
/// del contrato se verifica en <see cref="DialogosDeSimulacionContractTests"/> contra el test
/// double <c>DialogosDeSimulacion</c> (AutoExam.Tests/TestDoubles), no acá.
///
/// Explícitamente fuera de alcance de este archivo (no son pruebas de contrato de interfaz):
/// - AC-T6 (DynamicResource de Theme/Tokens.* en tema claro/oscuro): verificación visual, queda
///   para QA manual sobre la ventana real.
/// - AC-T7, la parte de "las tres acciones irreversibles pasan por Confirmar": es una prueba de
///   integración por ViewModel (requiere mockear IDialogos en cada ViewModel que borra
///   historial / quita un libro / sale de un examen sin terminar), no de la interfaz en sí.
///   <see cref="DialogosDeSimulacionContractTests"/> deja documentado el patrón de uso
///   (registro de invocaciones a Confirmar) para que esas suites lo reutilicen.
/// </summary>
public class IDialogosContractTests
{
    [Fact]
    public void Confirmar_DevuelveBool()
    {
        var metodo = typeof(IDialogos).GetMethod(nameof(IDialogos.Confirmar));

        Assert.NotNull(metodo);
        Assert.Equal(typeof(bool), metodo!.ReturnType);
    }

    [Fact]
    public void Confirmar_RecibeMensajeYTitulo_ConTituloPorDefectoAutoExam()
    {
        var metodo = typeof(IDialogos).GetMethod(nameof(IDialogos.Confirmar))!;
        var parametros = metodo.GetParameters();

        Assert.Equal(2, parametros.Length);
        Assert.Equal(typeof(string), parametros[0].ParameterType);
        Assert.Equal("mensaje", parametros[0].Name);

        var tituloParam = parametros[1];
        Assert.Equal(typeof(string), tituloParam.ParameterType);
        Assert.Equal("titulo", tituloParam.Name);
        Assert.True(tituloParam.HasDefaultValue);
        Assert.Equal("AutoExam", tituloParam.DefaultValue);
    }

    [Theory]
    [InlineData(nameof(IDialogos.Aviso))]
    [InlineData(nameof(IDialogos.Error))]
    public void AvisoYError_DevuelvenVoid_YRecibenTituloYMensajeComoString(string nombreMetodo)
    {
        var metodo = typeof(IDialogos).GetMethod(nombreMetodo);

        Assert.NotNull(metodo);
        Assert.Equal(typeof(void), metodo!.ReturnType);

        var parametros = metodo.GetParameters();
        Assert.Equal(2, parametros.Length);
        Assert.Equal("titulo", parametros[0].Name);
        Assert.Equal("mensaje", parametros[1].Name);
        Assert.All(parametros, p => Assert.Equal(typeof(string), p.ParameterType));
    }

    [Fact]
    public void DialogoService_ImplementaIDialogos()
    {
        // Chequeo puramente estructural (Type.IsAssignableFrom): no crea una instancia de
        // DialogoService ni ejecuta ningún método, así que no puede abrir una ventana real.
        Assert.True(typeof(IDialogos).IsAssignableFrom(typeof(DialogoService)));
    }

    [Theory]
    [InlineData(nameof(IDialogos.Confirmar))]
    [InlineData(nameof(IDialogos.Aviso))]
    [InlineData(nameof(IDialogos.Error))]
    public void DialogoService_ExponeLaMismaFirmaQueLaInterfaz(string nombreMetodo)
    {
        var enInterfaz = typeof(IDialogos).GetMethod(nombreMetodo);
        var enImplementacion = typeof(DialogoService).GetMethod(nombreMetodo);

        Assert.NotNull(enInterfaz);
        Assert.NotNull(enImplementacion);
        Assert.Equal(enInterfaz!.ReturnType, enImplementacion!.ReturnType);
        Assert.Equal(
            enInterfaz.GetParameters().Select(p => p.ParameterType),
            enImplementacion.GetParameters().Select(p => p.ParameterType));
    }
}
