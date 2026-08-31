using System.IO;
using System.Threading.Tasks;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;
using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-012 — <c>HistorialViewModel.BorrarExamenCommand</c> (specs/03-architecture.md Inc-4 §4.6,
/// specs/02-tech-spec.md "US-012", AC-T56 / AC-T58 / AC-T59, NFR-50 / NFR-51). Cierra la brecha
/// de test-developer: no había ninguna suite de <c>HistorialViewModel</c>.
///
/// Nivel ViewModel: qué pasa alrededor del borrado — confirmación (texto normal vs. con revancha
/// en curso), cancelar = nada cambia, confirmar = <c>SesionUsuarioService.BorrarExamen</c> +
/// limpieza best-effort de <c>Imagenes\{id}</c> + evento <c>ExamenBorrado</c>. La recalculación
/// de estadísticas y la persistencia se prueban un nivel más abajo
/// (<see cref="AutoExam.Tests.Services.SesionUsuarioServiceBorrarExamenTests"/>) — acá no se duplica.
///
/// Cada test redirige <see cref="RutasApp.Raiz"/> a una carpeta propia: <c>BorrarExamen</c>
/// escribe <c>perfil.json</c> y el comando toca <c>Imagenes\{id}</c>.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class HistorialViewModelBorrarExamenTests
{
    [Fact]
    public Task BorrarExamen_Null_EsNoOp_NoPideConfirmacion() => ConVm(async (vm, sesion, dlg, nav) =>
    {
        Sembrar(sesion, Examen("a"));

        await vm.BorrarExamenCommand.ExecuteAsync(null);

        Assert.Equal(0, dlg.LlamadasConfirmar);
        Assert.Single(sesion.Perfil.Historial);
    });

    [Fact]
    public Task Cancelar_LaConfirmacion_NoCambiaNada_AC_T56() => ConVm(async (vm, sesion, dlg, nav) =>
    {
        var a = Examen("a");
        Sembrar(sesion, a, Examen("b"));
        dlg.RespuestaConfirmar = false;
        bool eventoDisparado = false;
        vm.ExamenBorrado += _ => eventoDisparado = true;

        await vm.BorrarExamenCommand.ExecuteAsync(a);

        Assert.Equal(1, dlg.LlamadasConfirmar);
        Assert.Equal(2, sesion.Perfil.Historial.Count);
        Assert.Contains(sesion.Perfil.Historial, e => e.Id == "a");
        Assert.False(eventoDisparado);
        Assert.Empty(nav.Estados);
    });

    [Fact]
    public Task Confirmar_Borra_LimpiaImagenes_YEmiteExamenBorrado_AC_T56_NFR50() => ConVm(async (vm, sesion, dlg, nav) =>
    {
        var a = Examen("aaa");
        Sembrar(sesion, a, Examen("bbb"));

        string carpetaImg = Path.Combine(RutasApp.Imagenes, "aaa");
        Directory.CreateDirectory(carpetaImg);
        File.WriteAllText(Path.Combine(carpetaImg, "img_01.jpg"), "x");

        string? idEvento = null;
        vm.ExamenBorrado += id => idEvento = id;

        await vm.BorrarExamenCommand.ExecuteAsync(a);

        Assert.DoesNotContain(sesion.Perfil.Historial, e => e.Id == "aaa");
        Assert.DoesNotContain(sesion.Historial, e => e.Id == "aaa");
        Assert.Contains(sesion.Perfil.Historial, e => e.Id == "bbb");
        Assert.Equal("aaa", idEvento);
        Assert.False(Directory.Exists(carpetaImg));
        Assert.Equal("Examen borrado del historial.", nav.UltimoEstado);
    });

    [Fact]
    public Task Confirmar_SinCarpetaDeImagenes_NoLanza_YBorraIgual_NFR50() => ConVm(async (vm, sesion, dlg, nav) =>
    {
        var a = Examen("sin-imgs");
        Sembrar(sesion, a);

        var ex = await Record.ExceptionAsync(() => vm.BorrarExamenCommand.ExecuteAsync(a));

        Assert.Null(ex);
        Assert.Empty(sesion.Perfil.Historial);
    });

    // ------------------------------------------------------------------
    // Texto de confirmación (AC-T56 / AC-T59 / NFR-51)
    // ------------------------------------------------------------------

    [Fact]
    public Task ConfirmacionNormal_MencionaElExamen_SinAdvertirRevancha_AC_T56() => ConVm(async (vm, sesion, dlg, nav) =>
    {
        var a = Examen("a", titulo: "Anatomía", alcance: "Módulo 3");
        Sembrar(sesion, a);

        await vm.BorrarExamenCommand.ExecuteAsync(a);

        var (mensaje, _) = dlg.ConfirmacionesPedidas[0];
        Assert.Contains("Anatomía", mensaje);
        Assert.Contains("no se puede deshacer", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revancha", mensaje, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public Task ConRevanchaEnCurso_LaConfirmacionAdvierteQueSeDescarta_AC_T59_NFR51() => ConVm(async (vm, sesion, dlg, nav) =>
    {
        var a = Examen("con-revancha", titulo: "Química");
        Sembrar(sesion, a);
        vm.HayRevanchaEnCursoDe = id => id == "con-revancha";

        await vm.BorrarExamenCommand.ExecuteAsync(a);

        var (mensaje, _) = dlg.ConfirmacionesPedidas[0];
        Assert.Contains("revancha", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("se descarta", mensaje, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public Task SinHookDeRevancha_UsaElTextoNormal_YFunciona() => ConVm(async (vm, sesion, dlg, nav) =>
    {
        var a = Examen("a", titulo: "Física");
        Sembrar(sesion, a);
        Assert.Null(vm.HayRevanchaEnCursoDe);

        await vm.BorrarExamenCommand.ExecuteAsync(a);

        var (mensaje, _) = dlg.ConfirmacionesPedidas[0];
        Assert.DoesNotContain("revancha", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(sesion.Perfil.Historial);
    });

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task ConVm(
        Func<HistorialViewModel, SesionUsuarioService, DialogosDeSimulacion, NavegacionDeSimulacion, Task> prueba)
    {
        string raiz = Path.Combine(Path.GetTempPath(), "AutoExam.Tests", "HVM-" + Guid.NewGuid().ToString("N"));
        string raizOriginal = RutasApp.Raiz;
        try
        {
            RutasApp.RedirigirRaiz(raiz);
            var sesion = new SesionUsuarioService();
            sesion.Cargar();
            var dlg = new DialogosDeSimulacion();
            var nav = new NavegacionDeSimulacion();
            var vm = new HistorialViewModel(sesion, dlg, nav);
            await prueba(vm, sesion, dlg, nav);
        }
        finally
        {
            RutasApp.RedirigirRaiz(raizOriginal);
            try { if (Directory.Exists(raiz)) Directory.Delete(raiz, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static void Sembrar(SesionUsuarioService sesion, params ExamenRendido[] examenes)
    {
        foreach (var e in examenes)
        {
            sesion.RegistrarExamen(e);
        }
    }

    private static ExamenRendido Examen(string id, string titulo = "Libro", string alcance = "") => new()
    {
        Id = id,
        Fecha = new DateTime(2026, 1, 1).AddMinutes(Math.Abs(id.GetHashCode()) % 5000),
        LibroTitulo = titulo,
        AlcanceDescripcion = alcance,
        TotalPreguntas = 10,
        Correctas = 7,
        PorcentajeAciertos = 70,
        NotaUBA = 6,
        Aprobado = true,
    };
}
