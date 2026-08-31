using System.Reflection;
using AutoExam.Services;

namespace AutoExam.Tests.Ingesta;

/// <summary>
/// Contrato de <see cref="IDialogos"/> tras M4 — specs/03-architecture.md (Incremento 4) §4.3:
/// <c>string? ElegirPdf()</c> se reemplaza por <c>string[]? ElegirFuentes()</c> (OpenFileDialog
/// con <c>Multiselect = true</c> y filtro combinado desde
/// <c>FactoriaExtractores.ExtensionesAdmitidas</c>). AC-T40 (Office en el selector), AC-T48
/// (imágenes + selección múltiple).
///
/// Sólo reflexión sobre la interfaz y la firma de <see cref="DialogoService"/>: NUNCA se
/// instancia ni invoca <c>DialogoService</c> acá — abre un <c>OpenFileDialog</c> modal real y
/// colgaría CI, mismo motivo documentado en
/// <see cref="AutoExam.Tests.Services.IDialogosContractTests"/>.
/// </summary>
public class IDialogosElegirFuentesContractTests
{
    private static MethodInfo? ElegirFuentesEn(Type tipo)
        => tipo.GetMethod("ElegirFuentes", BindingFlags.Public | BindingFlags.Instance);

    [Fact] // AC-T40 / AC-T48
    public void IDialogos_DeclaraElegirFuentes_SinParametros_DevolviendoArrayDeString()
    {
        var metodo = ElegirFuentesEn(typeof(IDialogos));

        Assert.True(metodo is not null,
            "IDialogos.ElegirFuentes() no existe — contrato §4.3 (reemplaza ElegirPdf()).");
        Assert.Empty(metodo!.GetParameters());
        Assert.Equal(typeof(string[]), metodo.ReturnType);
    }

    [Fact] // §4.3 "ElegirPdf() se elimina"
    public void IDialogos_YaNoDeclaraElegirPdf()
    {
        var viejo = typeof(IDialogos).GetMethod("ElegirPdf", BindingFlags.Public | BindingFlags.Instance);

        Assert.True(viejo is null,
            "IDialogos.ElegirPdf() sigue existiendo — §4.3 lo elimina en favor de ElegirFuentes(). " +
            "El selector de un solo PDF ya no cubre US-008/US-010.");
    }

    [Fact] // AC-T40 / AC-T48
    public void DialogoService_ImplementaElegirFuentes_ConLaMismaFirmaQueLaInterfaz()
    {
        var enInterfaz = ElegirFuentesEn(typeof(IDialogos));
        var enImpl = ElegirFuentesEn(typeof(DialogoService));

        Assert.True(enInterfaz is not null, "IDialogos.ElegirFuentes() no existe todavía (§4.3).");
        Assert.True(enImpl is not null, "DialogoService no implementa ElegirFuentes() (§4.3).");
        Assert.Equal(enInterfaz!.ReturnType, enImpl!.ReturnType);
        Assert.Equal(
            enInterfaz.GetParameters().Select(p => p.ParameterType),
            enImpl.GetParameters().Select(p => p.ParameterType));
    }

    [Theory] // regresión: M4 no toca el resto de IDialogos (§4.3 "sin cambios")
    [InlineData("Confirmar")]
    [InlineData("Aviso")]
    [InlineData("Error")]
    [InlineData("AbrirCarpeta")]
    public void ElRestoDeIDialogos_QuedaIntacto(string metodo)
    {
        Assert.NotNull(typeof(IDialogos).GetMethod(metodo, BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void DialogoService_SigueImplementandoIDialogos()
        => Assert.True(typeof(IDialogos).IsAssignableFrom(typeof(DialogoService)));
}
