using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AutoExam.ViewModels;

namespace AutoExam.Views;

public partial class AsistenteView : UserControl
{
    public AsistenteView()
    {
        InitializeComponent();

        // Los chips de modulo enlazan Modulo.Seleccionado, que el asistente no
        // observa item por item. Este enganche unico mantiene vivo el resumen
        // sin ensuciar el modelo con notificaciones cruzadas.
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(ModuloCambio), true);
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(ModuloCambio), true);
    }

    private void ModuloCambio(object sender, RoutedEventArgs e)
    {
        if (DataContext is AsistenteViewModel vm)
        {
            vm.Recalcular();
        }
    }
}
