using System.Linq;
using AutoExam.Services;
using AutoExam.Tests.TestDoubles;

namespace AutoExam.Tests.Services;

/// <summary>
/// Comportamiento no-throw del contrato <see cref="IDialogos"/> (AC-T6/AC-T7,
/// specs/02-tech-spec.md), verificado sobre el test double
/// <see cref="DialogosDeSimulacion"/> — ver <see cref="IDialogosContractTests"/> para el porqué
/// de no usar <c>DialogoService</c> real acá (abriría una ventana real y colgaría CI).
///
/// Estos tests también documentan el patrón de uso esperado para consumidores futuros: por
/// ejemplo, una suite de <c>HistorialViewModel</c> puede inyectar <see cref="DialogosDeSimulacion"/>,
/// configurar <see cref="DialogosDeSimulacion.RespuestaConfirmar"/> y asertar sobre
/// <see cref="DialogosDeSimulacion.ConfirmacionesPedidas"/> para cubrir AC-T7 a nivel de
/// integración de ViewModel — algo fuera del alcance de este archivo (contrato de interfaz).
/// </summary>
public class DialogosDeSimulacionContractTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Confirmar_DevuelveElBoolConfigurado_SinLanzar(bool respuestaEsperada)
    {
        IDialogos dialogos = new DialogosDeSimulacion { RespuestaConfirmar = respuestaEsperada };

        bool resultado = default;
        var excepcion = Record.Exception(() => resultado = dialogos.Confirmar("¿Confirmás?", "Título"));

        Assert.Null(excepcion);
        Assert.Equal(respuestaEsperada, resultado);
    }

    [Fact]
    public void Confirmar_SinTituloExplicito_UsaElDefaultAutoExam()
    {
        var dialogos = new DialogosDeSimulacion();
        IDialogos comoContrato = dialogos;

        comoContrato.Confirmar("mensaje sin titulo");

        var pedida = Assert.Single(dialogos.ConfirmacionesPedidas);
        Assert.Equal("AutoExam", pedida.Titulo);
        Assert.Equal("mensaje sin titulo", pedida.Mensaje);
    }

    [Fact]
    public void Confirmar_ConMensajeVacio_NoLanza()
    {
        IDialogos dialogos = new DialogosDeSimulacion();

        var excepcion = Record.Exception(() => dialogos.Confirmar(string.Empty));

        Assert.Null(excepcion);
    }

    [Fact]
    public void Aviso_NoLanza_YRegistraTituloYMensaje()
    {
        var dialogos = new DialogosDeSimulacion();
        IDialogos comoContrato = dialogos;

        var excepcion = Record.Exception(() => comoContrato.Aviso("Título", "Mensaje"));

        Assert.Null(excepcion);
        Assert.Equal(1, dialogos.LlamadasAviso);
        Assert.Equal(("Título", "Mensaje"), dialogos.AvisosMostrados.Single());
    }

    [Fact]
    public void Error_NoLanza_YRegistraTituloYMensaje()
    {
        var dialogos = new DialogosDeSimulacion();
        IDialogos comoContrato = dialogos;

        var excepcion = Record.Exception(() => comoContrato.Error("Título", "Mensaje"));

        Assert.Null(excepcion);
        Assert.Equal(1, dialogos.LlamadasError);
        Assert.Equal(("Título", "Mensaje"), dialogos.ErroresMostrados.Single());
    }

    [Fact]
    public void AvisoYError_LlamadosVariasVeces_AcumulanElRegistroSinLanzar()
    {
        var dialogos = new DialogosDeSimulacion();
        IDialogos comoContrato = dialogos;

        var excepcion = Record.Exception(() =>
        {
            comoContrato.Aviso("T1", "M1");
            comoContrato.Aviso("T2", "M2");
            comoContrato.Error("T3", "M3");
        });

        Assert.Null(excepcion);
        Assert.Equal(2, dialogos.LlamadasAviso);
        Assert.Equal(1, dialogos.LlamadasError);
    }
}
