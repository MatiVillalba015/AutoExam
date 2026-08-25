using System.Drawing;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// <see cref="GeometriaVentanaService"/> — US-003, AC-T8/AC-T9 (specs/02-tech-spec.md),
/// contrato en specs/03-architecture.md §4.3. Es <c>public static</c> y no depende de
/// <c>System.Windows.Forms.Screen</c> directamente (recibe las áreas de trabajo ya resueltas)
/// a propósito, exactamente para poder probarla sin levantar ninguna ventana ni depender de
/// los monitores reales de la máquina que corre la suite — ver el comentario en el propio
/// servicio.
/// </summary>
public class GeometriaVentanaServiceTests
{
    // ------------------------------------------------------------------
    // HayGeometriaGuardada — centinela -1 = "nunca se guardó nada"
    // ------------------------------------------------------------------

    [Fact]
    public void HayGeometriaGuardada_ConLosDefaultsDeAppConfig_DevuelveFalse()
    {
        // specs/03-architecture.md §4.3: VentanaAncho/VentanaAlto = -1 por default.
        Assert.False(GeometriaVentanaService.HayGeometriaGuardada(-1, -1));
    }

    [Theory]
    [InlineData(-1, 820)]
    [InlineData(1240, -1)]
    public void HayGeometriaGuardada_ConSoloUnaDimensionSinGuardar_DevuelveFalse(double ancho, double alto)
    {
        // Un config.json corrupto/parcial no debe pasar el filtro solo porque una de las
        // dos dimensiones haya quedado con un valor real.
        Assert.False(GeometriaVentanaService.HayGeometriaGuardada(ancho, alto));
    }

    [Fact]
    public void HayGeometriaGuardada_ConAnchoYAltoValidos_DevuelveTrue()
    {
        Assert.True(GeometriaVentanaService.HayGeometriaGuardada(1240, 820));
    }

    [Fact]
    public void HayGeometriaGuardada_ConCero_CuentaComoGuardada()
    {
        // El centinela es -1, no <=0: 0 es (en teoria) un tamanio guardado valido, no "vacio".
        // Documenta el limite exacto en vez de asumirlo.
        Assert.True(GeometriaVentanaService.HayGeometriaGuardada(0, 0));
    }

    // ------------------------------------------------------------------
    // EstaVisible — AC-T9: fallback si el monitor guardado ya no esta conectado
    // ------------------------------------------------------------------

    [Fact]
    public void EstaVisible_RectanguloCompletamenteDentroDeUnMonitor_DevuelveTrue()
    {
        var monitorPrincipal = new[] { new Rectangle(0, 0, 1920, 1080) };

        bool visible = GeometriaVentanaService.EstaVisible(100, 100, 1240, 820, monitorPrincipal);

        Assert.True(visible);
    }

    [Fact]
    public void EstaVisible_MonitorGuardadoDesconectado_DevuelveFalse()
    {
        // Caso central de AC-T9: la ventana quedo guardada en un monitor que ya no esta
        // conectado (por ejemplo X=2000 de un segundo monitor a la derecha que se desenchufo).
        // Solo queda el monitor principal 0..1920 — no hay interseccion posible.
        var soloMonitorPrincipal = new[] { new Rectangle(0, 0, 1920, 1080) };

        bool visible = GeometriaVentanaService.EstaVisible(2000, 100, 1240, 820, soloMonitorPrincipal);

        Assert.False(visible);
    }

    [Fact]
    public void EstaVisible_SinNingunMonitorConectado_DevuelveFalse()
    {
        // No debería pasar en la práctica (siempre hay al menos un monitor), pero
        // Screen.AllScreens podría en teoría devolver una colección vacía en un entorno raro
        // (p.ej. sesión RDP sin monitor virtual todavía inicializada); el fallback no debe
        // tirar excepción ni asumir "true por defecto".
        bool visible = GeometriaVentanaService.EstaVisible(100, 100, 1240, 820, Array.Empty<Rectangle>());

        Assert.False(visible);
    }

    [Fact]
    public void EstaVisible_ConSoloUnPixelDeSuperposicion_DevuelveTrue()
    {
        // La regla es "intersecta", no "esta 100% contenida": un pixel de superposicion en el
        // borde ya cuenta, porque Windows deja mover una ventana hasta que casi desaparece.
        var monitor = new Rectangle(0, 0, 1920, 1080);
        // Ventana de 100x100 con esquina inferior-derecha en (1, 1): un solo pixel adentro.
        bool visible = GeometriaVentanaService.EstaVisible(-99, -99, 100, 100, new[] { monitor });

        Assert.True(visible);
    }

    [Fact]
    public void EstaVisible_MultiMonitorConSegundaPantallaEnCoordenadasNegativas_DevuelveTrue()
    {
        // NFR-07 (multi-monitor): un segundo monitor a la izquierda del principal aparece con
        // X negativo en Screen.AllScreens. La ventana guardada ahi debe seguir considerandose
        // visible aunque el monitor principal (indice 0 = X=0) no la contenga.
        var monitorPrincipal = new Rectangle(0, 0, 1920, 1080);
        var monitorSecundarioALaIzquierda = new Rectangle(-1920, 0, 1920, 1080);

        bool visible = GeometriaVentanaService.EstaVisible(
            -1000, 200, 1240, 820, new[] { monitorPrincipal, monitorSecundarioALaIzquierda });

        Assert.True(visible);
    }

    [Fact]
    public void EstaVisible_RectanguloEntreDosMonitoresSinTocarNinguno_DevuelveFalse()
    {
        // Hueco entre monitores (configuraciones desalineadas verticalmente): ni toca el
        // principal ni el segundo, aunque ambos existan.
        var monitorPrincipal = new Rectangle(0, 0, 1920, 1080);
        var monitorSecundarioMuyAbajo = new Rectangle(0, 3000, 1920, 1080);

        bool visible = GeometriaVentanaService.EstaVisible(
            100, 1500, 1240, 820, new[] { monitorPrincipal, monitorSecundarioMuyAbajo });

        Assert.False(visible);
    }
}
