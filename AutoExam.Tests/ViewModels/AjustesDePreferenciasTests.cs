using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;
using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-046..US-054 — la pantalla de Ajustes por dentro: que cada preferencia se aplique y se
/// guarde al tocarla, que las dos acciones irreversibles pidan confirmación (RN-6) y que
/// restaurar valores de fábrica no se lleve puesto nada del usuario (RN-55).
///
/// Rutas aisladas porque cada cambio de preferencia escribe config.json de verdad: es
/// justamente lo que hace que el valor sobreviva a cerrar la app, así que probarlo sin
/// escribir no probaría nada.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class AjustesDePreferenciasTests
{
    private static (AjustesViewModel Vm, SesionUsuarioService Sesion, DialogosDeSimulacion Dialogos,
        NavegacionDeSimulacion Nav) Armar()
    {
        RutasApp.AsegurarCarpetas();

        // Cada test arranca de una config limpia: config.json es compartido dentro de la
        // colección, y un test que dejara el tema en verde haría fallar al siguiente.
        if (File.Exists(RutasApp.ArchivoConfig))
        {
            File.Delete(RutasApp.ArchivoConfig);
        }

        var sesion = new SesionUsuarioService();
        sesion.Cargar();

        var dialogos = new DialogosDeSimulacion();
        var nav = new NavegacionDeSimulacion();

        var vm = new AjustesViewModel(new BibliotecaService(), sesion, new GeminiApiService(), dialogos, nav);
        vm.CargarDesdeConfig();

        return (vm, sesion, dialogos, nav);
    }

    // ------------------------------------------------------------------
    // US-048 — zoom
    // ------------------------------------------------------------------

    [Fact]
    public void CambiarElZoom_LoAplicaATodaLaApp_YLoGuarda_US048()
    {
        var (vm, sesion, _, nav) = Armar();

        vm.AumentarZoomCommand.Execute(null);

        Assert.Equal(1.1, vm.Zoom, 3);
        Assert.Equal(1.1, nav.UltimoZoom!.Value, 3);
        Assert.Equal(1.1, sesion.Config.Zoom, 3);
    }

    [Fact]
    public void ElZoomGuardado_VuelveAlAbrirLaApp_US048()
    {
        var (vm, sesion, _, _) = Armar();

        sesion.Config.Zoom = 1.3;
        vm.CargarDesdeConfig();

        Assert.Equal(1.3, vm.Zoom, 3);
        Assert.Equal("130%", vm.ZoomTexto);
    }

    [Fact]
    public void EnElMaximo_ElBotonDeAgrandar_QuedaDeshabilitado_US048()
    {
        var (vm, _, _, _) = Armar();

        vm.Zoom = ZoomDeLaApp.Maximo;

        Assert.False(vm.AumentarZoomCommand.CanExecute(null));
        Assert.True(vm.ReducirZoomCommand.CanExecute(null));
    }

    [Fact]
    public void EnElMinimo_ElBotonDeAchicar_QuedaDeshabilitado_US048()
    {
        var (vm, _, _, _) = Armar();

        vm.Zoom = ZoomDeLaApp.Minimo;

        Assert.False(vm.ReducirZoomCommand.CanExecute(null));
        Assert.True(vm.AumentarZoomCommand.CanExecute(null));
    }

    // ------------------------------------------------------------------
    // US-049 — tema
    // ------------------------------------------------------------------

    [Fact]
    public void HayUnaMuestraPorTema_YSoloUnaMarcadaComoLaActual_US049()
    {
        var (vm, _, _, _) = Armar();

        Assert.Equal(PaletaDeApp.Temas.Count, vm.Temas.Count);
        Assert.Single(vm.Temas, t => t.EsElActual);
        Assert.Equal(PaletaDeApp.PorDefecto, vm.Temas.First(t => t.EsElActual).Clave);
    }

    [Fact]
    public void ElegirOtroTema_LoGuarda_YSeMantieneAlVolverAAbrir_US049()
    {
        var (vm, sesion, _, _) = Armar();

        vm.ElegirTemaCommand.Execute("azul");

        Assert.Equal("azul", sesion.Config.TemaDeColor);
        Assert.Equal("azul", vm.Temas.Single(t => t.EsElActual).Clave);

        vm.CargarDesdeConfig();
        Assert.Equal("azul", vm.TemaElegido);
    }

    /// <summary>
    /// El tema "claro" es el que apaga el fondo oscuro. Se mantiene sincronizado con el campo
    /// viejo <c>TemaOscuro</c> para que un config.json de esta versión siga abriendo bien en
    /// una anterior.
    /// </summary>
    [Fact]
    public void ElTemaClaro_DejaTemaOscuroEnFalse_YLosDemasEnTrue_US049()
    {
        var (vm, sesion, _, _) = Armar();

        vm.ElegirTemaCommand.Execute("claro");
        Assert.False(sesion.Config.TemaOscuro);

        vm.ElegirTemaCommand.Execute("verde");
        Assert.True(sesion.Config.TemaOscuro);
    }

    // ------------------------------------------------------------------
    // US-050 / US-054 — avisos y movimiento
    // ------------------------------------------------------------------

    /// <summary>
    /// La migración de US-049 corriendo de verdad sobre un config.json escrito por la versión
    /// anterior: sin ella, quien venía usando el tema claro abre en oscuro tras actualizar.
    /// </summary>
    [Fact]
    public void UnConfigDeLaVersionAnterior_ConservaSuTemaClaro_US049()
    {
        RutasApp.AsegurarCarpetas();
        File.WriteAllText(RutasApp.ArchivoConfig, "{\"TemaOscuro\":false}");

        var sesion = new SesionUsuarioService();
        sesion.Cargar();

        Assert.Equal("claro", sesion.Config.TemaDeColor);
        Assert.False(PaletaDeApp.Resolver(sesion.Config.TemaDeColor).Oscuro);
    }

    [Fact]
    public void UnZoomFueraDeRangoEnElArchivo_SeAcotaAlCargar_US048()
    {
        RutasApp.AsegurarCarpetas();
        File.WriteAllText(RutasApp.ArchivoConfig, "{\"Zoom\":9.5}");

        var sesion = new SesionUsuarioService();
        sesion.Cargar();

        Assert.Equal(ZoomDeLaApp.Maximo, sesion.Config.Zoom, 3);
    }

    [Fact]
    public void ApagarLasNotificaciones_SeGuarda_US050()
    {
        var (vm, sesion, _, _) = Armar();

        vm.Notificaciones = false;

        Assert.False(sesion.Config.Notificaciones);
    }

    [Fact]
    public void ActivarReducirMovimiento_SeGuarda_US054()
    {
        var (vm, sesion, _, _) = Armar();

        vm.ReducirMovimiento = true;

        Assert.True(sesion.Config.ReducirMovimiento);
    }

    /// <summary>
    /// Con Windows pidiendo movimiento reducido, el toggle de la app en "no" y las animaciones
    /// igual apagadas son dos configuraciones que se contradicen. El último criterio de US-054
    /// pide que no queden así sin avisar.
    /// </summary>
    [Fact]
    public void SiElSistemaYaPideReducirMovimiento_LaPantallaLoDice_US054()
    {
        var (vm, _, _, _) = Armar();

        Assert.Equal(AutoExam.Behaviors.Animaciones.PorElSistema, vm.MovimientoReducidoPorElSistema);

        if (vm.MovimientoReducidoPorElSistema)
        {
            Assert.NotEqual(string.Empty, vm.AvisoDeMovimiento);
            Assert.Contains("Windows", vm.AvisoDeMovimiento, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Equal(string.Empty, vm.AvisoDeMovimiento);
        }
    }

    // ------------------------------------------------------------------
    // US-046 — vaciar caché
    // ------------------------------------------------------------------

    [Fact]
    public void VaciarCache_PideConfirmacionAntes_RN6()
    {
        var (vm, _, dialogos, _) = Armar();

        dialogos.RespuestaConfirmar = false;
        vm.VaciarCacheCommand.Execute(null);

        Assert.Equal(1, dialogos.LlamadasConfirmar);
    }

    /// <summary>
    /// El texto de la confirmación importa tanto como la confirmación: "vaciar caché" suena a
    /// que se puede llevar puesto el material, y el criterio de US-046 es explícito en que no
    /// borra libros ni historial.
    /// </summary>
    [Fact]
    public void LaConfirmacionDeVaciarCache_AclaraQueNoSeBorranLibrosNiHistorial_US046()
    {
        var (vm, _, dialogos, _) = Armar();

        dialogos.RespuestaConfirmar = false;
        vm.VaciarCacheCommand.Execute(null);

        string mensaje = dialogos.ConfirmacionesPedidas[0].Mensaje;

        Assert.Contains("libros", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("historial", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SinNadaTemporalQueBorrar_ElBotonDeVaciarCache_SeApaga_US046()
    {
        var (vm, _, _, _) = Armar();

        // Sin carpetas huérfanas la medición da cero, y prometer liberar espacio que no existe
        // es peor que no ofrecer el botón.
        Assert.Equal(vm.BytesTemporales > 0, vm.HayAlgoQueVaciar);
    }

    [Fact]
    public void LaMedicion_ReparteElTotalEntreLosTresGrupos_US046()
    {
        var (vm, _, _, _) = Armar();

        Assert.Equal(3, vm.Usos.Count);

        double suma = vm.FraccionLibros + vm.FraccionHistorial + vm.FraccionTemporales;

        // O no hay nada guardado (todo en cero) o las tres fracciones cubren el total.
        Assert.True(suma is 0 or > 0.99 and < 1.01,
            $"Las tres fracciones de la barra suman {suma}: la barra no representa el total.");
    }

    // ------------------------------------------------------------------
    // US-047 — restaurar valores de fábrica
    // ------------------------------------------------------------------

    [Fact]
    public void RestaurarFabrica_PideConfirmacion_YSinElla_NoCambiaNada_RN6()
    {
        var (vm, sesion, dialogos, _) = Armar();

        vm.ElegirTemaCommand.Execute("verde");
        dialogos.RespuestaConfirmar = false;

        vm.RestaurarFabricaCommand.Execute(null);

        Assert.Equal(1, dialogos.LlamadasConfirmar);
        Assert.Equal("verde", sesion.Config.TemaDeColor);
    }

    [Fact]
    public void RestaurarFabrica_DevuelveTemaZoomYAvisosASuValorInicial_US047()
    {
        var (vm, sesion, dialogos, _) = Armar();

        vm.ElegirTemaCommand.Execute("azul");
        vm.Zoom = 1.4;
        vm.Notificaciones = false;
        vm.ReducirMovimiento = true;

        dialogos.RespuestaConfirmar = true;
        vm.RestaurarFabricaCommand.Execute(null);

        Assert.Equal(PaletaDeApp.PorDefecto, vm.TemaElegido);
        Assert.Equal(ZoomDeLaApp.Normal, vm.Zoom, 3);
        Assert.True(vm.Notificaciones);
        Assert.False(vm.ReducirMovimiento);

        Assert.Equal(PaletaDeApp.PorDefecto, sesion.Config.TemaDeColor);
        Assert.Equal(ZoomDeLaApp.Normal, sesion.Config.Zoom, 3);
    }

    [Fact]
    public void RestaurarFabrica_NoBorraLasClavesDeGemini_RN55()
    {
        var (vm, sesion, dialogos, _) = Armar();

        sesion.Config.EstablecerClaves("AQ.una,AQ.otra");
        sesion.GuardarConfig();
        vm.CargarDesdeConfig();

        dialogos.RespuestaConfirmar = true;
        vm.RestaurarFabricaCommand.Execute(null);

        Assert.Equal(new[] { "AQ.una", "AQ.otra" }, sesion.Config.ClavesDisponibles);
        Assert.Equal(2, vm.Claves.ComoLista().Count);
    }

    [Fact]
    public void LaConfirmacionDeRestaurar_AclaraQueNoSePierdenLibrosHistorialNiClaves_US047()
    {
        var (vm, _, dialogos, _) = Armar();

        dialogos.RespuestaConfirmar = false;
        vm.RestaurarFabricaCommand.Execute(null);

        string mensaje = dialogos.ConfirmacionesPedidas[0].Mensaje;

        Assert.Contains("libros", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("historial", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("claves", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // US-051 — copia de seguridad
    // ------------------------------------------------------------------

    [Fact]
    public async Task ImportarUnaCopia_PideConfirmacion_AclarandoQueReemplaza_RN6()
    {
        var (vm, _, dialogos, nav) = Armar();

        string copia = Path.Combine(Path.GetTempPath(), $"autoexam-vm-{Guid.NewGuid():N}.axcopia");
        CopiaDeSeguridadService.Exportar(copia, libros: 2, examenes: 5);

        try
        {
            dialogos.RutaDeCopiaAImportar = copia;
            dialogos.RespuestaConfirmar = false;

            await vm.ImportarCopiaCommand.ExecuteAsync(null);

            Assert.Equal(1, dialogos.LlamadasConfirmar);
            Assert.Contains("REEMPLAZA", dialogos.ConfirmacionesPedidas[0].Mensaje, StringComparison.Ordinal);

            // Sin confirmar no se recarga nada: la importación ni siquiera empezó.
            Assert.Equal(0, nav.LlamadasRecargarDatos);
        }
        finally
        {
            File.Delete(copia);
        }
    }

    [Fact]
    public async Task ImportarUnArchivoQueNoEsUnaCopia_AvisaYNoPideConfirmacion_US051()
    {
        var (vm, _, dialogos, nav) = Armar();

        string falso = Path.Combine(Path.GetTempPath(), $"autoexam-vm-{Guid.NewGuid():N}.axcopia");
        File.WriteAllText(falso, "cualquier cosa");

        try
        {
            dialogos.RutaDeCopiaAImportar = falso;

            await vm.ImportarCopiaCommand.ExecuteAsync(null);

            // No se le pregunta al usuario si quiere reemplazar sus datos con un archivo que no
            // sirve: se le dice que el archivo no sirve.
            Assert.Equal(0, dialogos.LlamadasConfirmar);
            Assert.Equal(0, nav.LlamadasRecargarDatos);
            Assert.Equal(3, vm.Severidad);
            Assert.Contains("no se modificó", vm.Mensaje, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(falso);
        }
    }

    [Fact]
    public async Task CancelarElSelectorDeArchivo_NoHaceNada_US051()
    {
        var (vm, _, dialogos, nav) = Armar();

        dialogos.RutaDondeGuardarCopia = null;
        dialogos.RutaDeCopiaAImportar = null;

        await vm.ExportarCopiaCommand.ExecuteAsync(null);
        await vm.ImportarCopiaCommand.ExecuteAsync(null);

        Assert.Equal(0, dialogos.LlamadasConfirmar);
        Assert.Equal(0, nav.LlamadasRecargarDatos);
    }

    [Fact]
    public async Task ExportarTodo_EscribeElArchivoDondeSeEligio_US051()
    {
        var (vm, _, dialogos, _) = Armar();

        string destino = Path.Combine(Path.GetTempPath(), $"autoexam-vm-{Guid.NewGuid():N}.axcopia");
        dialogos.RutaDondeGuardarCopia = destino;

        try
        {
            await vm.ExportarCopiaCommand.ExecuteAsync(null);

            Assert.True(File.Exists(destino), "No se escribió la copia en la ruta elegida.");
            Assert.Equal(1, vm.Severidad);

            // El nombre sugerido lleva la extensión propia: es lo que hace que la copia se
            // reconozca después en la carpeta de Descargas.
            Assert.EndsWith(CopiaDeSeguridadService.Extension, dialogos.NombresSugeridosParaCopia[0],
                StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(destino);
        }
    }

    // ------------------------------------------------------------------
    // US-056 — lo que ya existía sigue igual
    // ------------------------------------------------------------------

    [Fact]
    public void PreguntasPorLote_SigueEnAjustes_YConSuMismoRango_US056()
    {
        var (vm, sesion, _, _) = Armar();

        Assert.Equal(sesion.Config.PreguntasPorLote, vm.PreguntasPorLote);

        vm.PreguntasPorLote = 8;
        vm.GuardarCommand.Execute(null);

        Assert.Equal(8, sesion.Config.PreguntasPorLote);
    }

    [Fact]
    public void NingunAjusteNuevo_TocaPreguntasPorLote_US056()
    {
        var (vm, sesion, dialogos, _) = Armar();

        vm.PreguntasPorLote = 7;
        vm.GuardarCommand.Execute(null);

        vm.ElegirTemaCommand.Execute("verde");
        vm.Zoom = 1.2;
        vm.Notificaciones = false;
        vm.ReducirMovimiento = true;

        dialogos.RespuestaConfirmar = true;
        vm.RestaurarFabricaCommand.Execute(null);

        Assert.Equal(7, sesion.Config.PreguntasPorLote);
    }
}
