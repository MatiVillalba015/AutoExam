using System.IO;
using System.Reflection;
using System.Windows;
using AutoExam.Behaviors;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Theme;

/// <summary>
/// US-054 / RN-62 — "Reducir movimiento" propio de la app, además de la preferencia del
/// sistema operativo, combinados con OR.
///
/// El OR es lo que evita dos configuraciones contradictorias: con Windows pidiendo movimiento
/// reducido, apagar el toggle de la app no puede volver a encender las animaciones. Es la
/// clase de regla que se rompe con un cambio de una línea (un <c>=</c> en lugar de un
/// <c>||</c>) y que después nadie asocia a la pantalla de Ajustes.
/// </summary>
public class MovimientoReducidoPropioTests
{
    /// <summary>
    /// La preferencia de la app es estado estático compartido: cada test la deja como estaba
    /// para no arrastrar el efecto a la suite entera.
    /// </summary>
    private static void Restaurando(Action prueba)
    {
        bool antes = Animaciones.PorLaApp;

        try
        {
            prueba();
        }
        finally
        {
            Animaciones.AplicarEn(null, antes);
        }
    }

    [Fact]
    public void ElToggleDeLaApp_ReduceElMovimiento_AunqueElSistemaNoLoPida_US054()
    {
        Restaurando(() =>
        {
            Animaciones.AplicarEn(null, true);

            Assert.True(Animaciones.PorLaApp);
            Assert.True(Animaciones.Reducidas);
        });
    }

    [Fact]
    public void ApagarElToggle_NoContradiceLaPreferenciaDelSistema_RN62()
    {
        Restaurando(() =>
        {
            Animaciones.AplicarEn(null, false);

            // Con el toggle en no, el resultado sigue siendo lo que diga el sistema: si Windows
            // pide reducir, se reduce igual.
            Assert.Equal(Animaciones.PorElSistema, Animaciones.Reducidas);
        });
    }

    [Fact]
    public void LaSenialDelSistema_SigueSiendoLaDeSiempre_NFR47()
    {
        // No cambió con US-054: es "Mostrar animaciones en Windows" más el tier de render.
        string fuente = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Behaviors/Animaciones.cs"));

        Assert.Contains("SystemParameters.ClientAreaAnimation", fuente, StringComparison.Ordinal);
        Assert.Contains("RenderCapability.Tier", fuente, StringComparison.Ordinal);
    }

    /// <summary>
    /// La propiedad adjunta que usan los <c>MultiTrigger</c> de Estilos.xaml tiene que heredar
    /// por el árbol: es lo que hace que tocar el toggle se vea al instante en cada plantilla,
    /// en vez de recién en el próximo arranque. Sin herencia habría que setearla control por
    /// control, y las plantillas de los estilos ni siquiera existen hasta que se instancia el
    /// control que las usa.
    /// </summary>
    [Fact]
    public void LaPropiedadAdjunta_HeredaPorElArbol_ParaQueElCambioSeVeaAlInstante_US054()
    {
        var metadata = Animaciones.MovimientoReducidoProperty.GetMetadata(typeof(DependencyObject));

        var marco = Assert.IsType<FrameworkPropertyMetadata>(metadata);

        Assert.True(marco.Inherits,
            "Sin herencia, el toggle de Ajustes no llega a los MultiTrigger de Estilos.xaml (US-054).");
    }

    [Fact]
    public void SetearLaPreferenciaEnLaRaiz_LlegaASusHijos_RN62()
    {
        // En el hilo STA compartido de la suite: crear un FrameworkElement exige STA, y este
        // test necesita un árbol de verdad para ver la herencia funcionando.
        Restaurando(() => TestSupport.WpfHost.Invocar(() =>
        {
            var raiz = new System.Windows.Controls.Grid();
            var hijo = new System.Windows.Controls.Border();
            raiz.Children.Add(hijo);

            Animaciones.AplicarEn(raiz, true);

            Assert.True(Animaciones.GetMovimientoReducido(raiz));

            // Lo que importa: el valor baja solo hasta el control que dibuja la animación.
            Assert.True(Animaciones.GetMovimientoReducido(hijo),
                "La preferencia no llega a los hijos: el toggle de Ajustes no apagaría nada (US-054).");
        }));
    }

    /// <summary>
    /// El valor por defecto de la propiedad es la preferencia del sistema: sin nadie que la
    /// setee —por ejemplo en un test de estilos que instancia un control suelto— el
    /// comportamiento tiene que seguir siendo el de antes de US-054.
    /// </summary>
    [Fact]
    public void SinNadieQueLaSetee_ElDefaultSigueSiendoLoQuePideElSistema_NFR47()
    {
        var metadata = Animaciones.MovimientoReducidoProperty.GetMetadata(typeof(DependencyObject));

        Assert.Equal(Animaciones.PorElSistema, (bool)metadata.DefaultValue!);
    }

    [Fact]
    public void ElViewModel_AplicaLaPreferenciaAlTocarla_YNoSoloAlGuardarla_US054()
    {
        // El criterio dice "activo Reducir movimiento, navego por la app, las animaciones se
        // desactivan": si solo se guardara, habría que reiniciar para verlo.
        string fuente = File.ReadAllText(
            ArchivoFuenteHelper.RutaFuente("AutoExam/ViewModels/AjustesViewModel.cs"));

        int aplicar = fuente.IndexOf("Animaciones.Aplicar", StringComparison.Ordinal);

        Assert.True(aplicar >= 0,
            "AjustesViewModel guarda la preferencia pero no la aplica (US-054).");
    }

    /// <summary>
    /// Y al arrancar también: la preferencia guardada tiene que estar puesta antes de que se
    /// dibuje la primera pantalla, no después de entrar a Ajustes.
    /// </summary>
    [Fact]
    public void ElShell_AplicaLasTresPreferenciasDeApariencia_AlArrancar()
    {
        var metodo = typeof(AutoExam.ViewModels.ShellViewModel)
            .GetMethod("AplicarApariencia", BindingFlags.Public | BindingFlags.Instance);

        Assert.True(metodo is not null,
            "ShellViewModel no tiene un punto único donde aplicar tema, zoom y movimiento.");

        string fuente = File.ReadAllText(
            ArchivoFuenteHelper.RutaFuente("AutoExam/ViewModels/ShellViewModel.cs"));

        foreach (string preferencia in new[] { "TemaService.Aplicar", "AplicarZoom", "Animaciones.Aplicar" })
        {
            Assert.Contains(preferencia, fuente, StringComparison.Ordinal);
        }
    }
}
