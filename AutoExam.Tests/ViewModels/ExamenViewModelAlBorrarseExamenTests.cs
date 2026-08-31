using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;
using AutoExam.Tests.TestSupport;
using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-012 — <c>ExamenViewModel.AlBorrarseExamen</c> (specs/03-architecture.md Inc-4 §4.6 / §3,
/// AC-T59 / NFR-51). Complementa <see cref="HistorialViewModelBorrarExamenTests"/> (lado que
/// emite el evento) con el lado que lo consume: al borrarse el examen original mientras hay un
/// intento/ronda en curso de ese registro, el intento se descarta sin registrarse y ninguna
/// ronda posterior lo reancla.
///
/// Usa el hilo STA compartido (<see cref="WpfHost"/>): <c>ExamenViewModel</c> crea
/// <c>DispatcherTimer</c>s en su ctor.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class ExamenViewModelAlBorrarseExamenTests
{
    private static ExamenViewModel NuevoVm(out NavegacionDeSimulacion nav)
    {
        var sesion = new SesionUsuarioService();
        sesion.Cargar();
        var n = new NavegacionDeSimulacion();
        nav = n;
        return new ExamenViewModel(sesion, new DialogosDeSimulacion(), n);
    }

    private static ExamenEnCurso Intento(int ronda, ExamenRendido? registro)
    {
        var e = new ExamenEnCurso
        {
            Id = "intento-" + Guid.NewGuid().ToString("N"),
            LibroTitulo = "Libro",
            Materia = "M",
            AlcanceDescripcion = "Todo",
            Ronda = ronda,
            Registro = registro,
        };
        e.Preguntas.Add(new Pregunta
        {
            TextoPregunta = "P1",
            Opciones = new() { "a", "b" },
            IndiceRespuestaCorrecta = 0,
        });
        return e;
    }

    [Fact]
    public void RevanchaEnCurso_DelExamenBorrado_SeDescartaSinRegistrar_AC_T59()
    {
        var (huboIntento, registroTrasBorrar, estado) = WpfHost.Invocar(() =>
        {
            var vm = NuevoVm(out var nav);
            var original = new ExamenRendido { Id = "orig", NotaUBA = 4, TotalPreguntas = 10, Correctas = 6 };
            vm.Iniciar(Intento(ronda: 1, registro: original));

            Assert.True(vm.HayIntentoAbierto);
            Assert.Equal("orig", vm.RegistroActualId);

            vm.AlBorrarseExamen("orig");

            return (vm.HayIntentoAbierto, vm.Examen?.Registro, nav.UltimoEstado);
        });

        Assert.False(huboIntento);            // Cerrar() descartó la ronda
        Assert.Null(registroTrasBorrar);
        Assert.Contains("revancha", estado, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntentoOriginalEnCurso_DelExamenBorrado_PierdeElRegistro_PeroSiguePudiendoRendirse()
    {
        var (huboIntento, registro) = WpfHost.Invocar(() =>
        {
            var vm = NuevoVm(out _);
            var reg = new ExamenRendido { Id = "orig", TotalPreguntas = 10 };
            vm.Iniciar(Intento(ronda: 0, registro: reg));

            vm.AlBorrarseExamen("orig");

            return (vm.HayIntentoAbierto, vm.Examen?.Registro);
        });

        Assert.True(huboIntento);   // no es revancha: no se cierra, sólo se desancla
        Assert.Null(registro);
    }

    [Fact]
    public void BorrarOtroExamen_NoTocaElIntentoEnCurso()
    {
        var (huboIntento, registroId) = WpfHost.Invocar(() =>
        {
            var vm = NuevoVm(out _);
            var reg = new ExamenRendido { Id = "orig", TotalPreguntas = 10 };
            vm.Iniciar(Intento(ronda: 1, registro: reg));

            vm.AlBorrarseExamen("otro-distinto");

            return (vm.HayIntentoAbierto, vm.RegistroActualId);
        });

        Assert.True(huboIntento);
        Assert.Equal("orig", registroId);
    }

    [Fact]
    public void SinIntentoAbierto_AlBorrarseExamen_EsNoOp()
    {
        var ex = Record.Exception(() => WpfHost.Invocar(() =>
        {
            var vm = NuevoVm(out _);
            vm.AlBorrarseExamen("cualquiera");
            return 0;
        }));

        Assert.Null(ex);
    }
}
