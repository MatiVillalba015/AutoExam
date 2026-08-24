using System.Windows;
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

        Vm.Cerrar();
    }
}
