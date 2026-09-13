using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;
using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-044 — la fracción que dibuja el arco del anillo del resumen del Historial.
///
/// El arco dibuja el PORCENTAJE DE ACIERTOS, que es el mismo dato que el texto "ACIERTOS" de
/// al lado; el número grande del medio sigue siendo el promedio en nota, sobre 10. Son dos
/// métricas distintas que conviven en la misma tarjeta a propósito, y por eso lo que se
/// verifica acá es justamente que el arco salga de la de aciertos: salía de la nota dividida
/// por 10, así que el anillo y el porcentaje escrito al lado podían contradecirse.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class FraccionDelAnilloTests
{
    [Fact]
    public void ElArco_SaleDelMismoPorcentajeQueElTextoDeAciertos_US044()
    {
        ConVm((vm, sesion) =>
        {
            // Dos exámenes con 30% y 70% de aciertos: el promedio de aciertos da 50%.
            Sembrar(sesion, Rendido(nota: 3, aciertos: 30), Rendido(nota: 7, aciertos: 70));

            vm.Refrescar();

            Assert.Equal("50%", vm.Aciertos);
            Assert.Equal(0.5, vm.AciertosFraccion, 3);
        });
    }

    /// <summary>
    /// La nota y los aciertos no tienen por qué coincidir: el arco tiene que seguir a los
    /// aciertos aunque la nota diga otra cosa. Es exactamente el caso del bug, donde con 23%
    /// de aciertos el anillo dibujaba la nota.
    /// </summary>
    [Fact]
    public void ConNotaYAciertosDistintos_ElArcoSigueALosAciertos_US044()
    {
        ConVm((vm, sesion) =>
        {
            Sembrar(sesion, Rendido(nota: 9, aciertos: 23));

            vm.Refrescar();

            Assert.Equal("9,0", vm.Promedio.Replace('.', ','));
            Assert.Equal("23%", vm.Aciertos);
            Assert.Equal(0.23, vm.AciertosFraccion, 3);
        });
    }

    [Fact]
    public void ConTodoAcertado_ElAnilloDaLaVueltaEntera_US044()
    {
        ConVm((vm, sesion) =>
        {
            Sembrar(sesion, Rendido(nota: 10, aciertos: 100));

            vm.Refrescar();

            Assert.Equal(1, vm.AciertosFraccion, 3);
        });
    }

    [Fact]
    public void SinExamenes_ElAnilloQuedaEnCero_US044()
    {
        // El resumen entero no se muestra sin exámenes, pero la fracción no puede quedarse con
        // el valor del historial anterior: borrar todo tiene que dejar el anillo vacío.
        ConVm((vm, sesion) =>
        {
            Sembrar(sesion, Rendido(nota: 9, aciertos: 90));
            vm.Refrescar();
            Assert.True(vm.AciertosFraccion > 0);

            sesion.BorrarHistorial();
            vm.Refrescar();

            Assert.Equal(0, vm.AciertosFraccion);
        });
    }

    // ------------------------------------------------------------------

    private static void ConVm(Action<HistorialViewModel, SesionUsuarioService> prueba)
    {
        string raiz = Path.Combine(Path.GetTempPath(), "AutoExam.Tests", "Anillo-" + Guid.NewGuid().ToString("N"));
        string raizOriginal = RutasApp.Raiz;

        try
        {
            RutasApp.RedirigirRaiz(raiz);

            var sesion = new SesionUsuarioService();
            sesion.Cargar();

            prueba(new HistorialViewModel(sesion, new DialogosDeSimulacion(), new NavegacionDeSimulacion()), sesion);
        }
        finally
        {
            RutasApp.RedirigirRaiz(raizOriginal);
            try { if (Directory.Exists(raiz)) Directory.Delete(raiz, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static void Sembrar(SesionUsuarioService sesion, params ExamenRendido[] examenes)
    {
        foreach (var examen in examenes)
        {
            sesion.RegistrarExamen(examen);
        }
    }

    /// <summary>
    /// La nota y el porcentaje de aciertos se pasan por separado a propósito: en la app real no
    /// son la misma cuenta (la escala UBA no es lineal contra los aciertos), y si el test los
    /// derivara uno del otro no podría distinguir cuál de los dos está dibujando el anillo.
    /// </summary>
    private static ExamenRendido Rendido(int nota, double aciertos) => new()
    {
        LibroTitulo = "Tp",
        Materia = "Fisiologia",
        Fecha = DateTime.Now,
        NotaUBA = nota,
        Aprobado = nota >= 4,
        TotalPreguntas = 10,
        Correctas = (int)Math.Round(aciertos / 10),
        PorcentajeAciertos = aciertos,
    };
}
