using System.Linq;
using System.Windows;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.ViewModels;

namespace AutoExam;

/// <summary>
/// Cascaron de la aplicacion. El code-behind solo hace lo que es propio de la
/// ventana: arrancar la carga y decidir si se puede cerrar. Todo lo demas vive
/// en ShellViewModel y en las vistas de Views/.
/// </summary>
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private ShellViewModel? Vm => DataContext as ShellViewModel;

    private async void Ventana_Loaded(object sender, RoutedEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        try
        {
            RestaurarGeometria(Vm.Config);
            await Vm.IniciarAsync();
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Ventana_Loaded", ex);
            MessageBox.Show($"No se pudo inicializar la aplicacion.\n\n{ex.Message}",
                "AutoExam", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Ventana_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        if (!Vm.PuedeCerrar())
        {
            e.Cancel = true;
            return;
        }

        GuardarGeometria(Vm.Config);
        Vm.Cerrar();
    }

    // ------------------------------------------------------------------
    // Geometria de ventana (US-003)
    // ------------------------------------------------------------------

    /// <summary>
    /// Aplica la geometria guardada en config.json, si hay una y sigue siendo visible.
    /// Se llama desde <see cref="Ventana_Loaded"/>, antes de que el usuario vea la
    /// ventana: en ese punto ya se puede pisar sin problema lo que
    /// <c>WindowStartupLocation="CenterScreen"</c> hubiera calculado. Si nunca se guardo
    /// nada o el rectangulo guardado no entra en ningun monitor conectado (por ejemplo
    /// se desconecto uno), se deja el default del XAML (CenterScreen, 1240x820).
    /// </summary>
    private void RestaurarGeometria(AppConfig config)
    {
        if (!GeometriaVentanaService.HayGeometriaGuardada(config.VentanaAncho, config.VentanaAlto))
        {
            return;
        }

        var areasDeTrabajo = System.Windows.Forms.Screen.AllScreens.Select(pantalla => pantalla.WorkingArea);

        if (!GeometriaVentanaService.EstaVisible(
                config.VentanaX, config.VentanaY, config.VentanaAncho, config.VentanaAlto, areasDeTrabajo))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = config.VentanaAncho;
        Height = config.VentanaAlto;
        Left = config.VentanaX;
        Top = config.VentanaY;

        // config.json corrupto (corte de luz durante JsonStore.Guardar, edicion a mano,
        // etc.) puede traer un entero fuera de rango: JsonStore lo deserializa sin
        // problema porque no hay JsonStringEnumConverter, pero Window.WindowState SI
        // valida y tira ArgumentException con cualquier valor que no sea Normal/
        // Minimized/Maximized. Se cae a Normal en vez de dejar crashear el arranque.
        WindowState = Enum.IsDefined(typeof(WindowState), config.VentanaEstado)
            ? config.VentanaEstado
            : WindowState.Normal;
    }

    /// <summary>
    /// Anota tamanio, posicion y estado actuales en <paramref name="config"/>. No guarda
    /// nada en disco: eso lo hace <see cref="ShellViewModel.Cerrar"/>, que ya llama a
    /// <c>SesionUsuarioService.GuardarConfig()</c> a continuacion (mismo punto donde hoy
    /// se decide cerrar, ver <see cref="Ventana_Closing"/>).
    /// </summary>
    private void GuardarGeometria(AppConfig config)
    {
        // Minimized no se persiste como tal (no tendria sentido volver a abrir
        // minimizada), pero mientras esta minimizada Left/Top/Width/Height reflejan el
        // placement "iconic" de Win32 (algo como -32000,-32000), no la geometria real:
        // hay que leer RestoreBounds igual que con Maximized, y guardar el estado como
        // Normal. Solo si esta realmente Normal se puede confiar en Left/Top/Width/Height.
        bool debeUsarRestoreBounds = WindowState != WindowState.Normal;

        Rect bounds = debeUsarRestoreBounds ? RestoreBounds : new Rect(Left, Top, Width, Height);

        config.VentanaAncho = bounds.Width;
        config.VentanaAlto = bounds.Height;
        config.VentanaX = bounds.X;
        config.VentanaY = bounds.Y;
        config.VentanaEstado = WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
    }
}
