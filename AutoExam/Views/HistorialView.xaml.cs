using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AutoExam.ViewModels;

namespace AutoExam.Views;

public partial class HistorialView : UserControl
{
    public HistorialView()
    {
        InitializeComponent();

        // Las casillas de la lista enlazan ExamenRendido.Seleccionado, que el historial no
        // observa examen por examen. Este enganche unico mantiene vivos el conteo de
        // preguntas disponibles y el boton de armar el repaso (US-026), sin sumarle al modelo
        // notificaciones cruzadas. Mismo patron que ya usa AsistenteView para los documentos.
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(SeleccionCambio), true);
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(SeleccionCambio), true);
    }

    private void SeleccionCambio(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistorialViewModel vm)
        {
            vm.RecalcularRepaso();
        }
    }
}
