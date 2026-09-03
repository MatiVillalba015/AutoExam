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

        // Los chips de modulo enlazan Modulo.Seleccionado y, desde US-024, las casillas de
        // documento enlazan Libro.Seleccionado. El asistente no observa esas colecciones
        // item por item, asi que este enganche unico —CheckBox tambien es un ToggleButton—
        // mantiene vivos el resumen y el boton de generar sin ensuciar los modelos con
        // notificaciones cruzadas.
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(ModuloCambio), true);
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(ModuloCambio), true);
    }

    private void ModuloCambio(object sender, RoutedEventArgs e)
    {
        if (DataContext is AsistenteViewModel vm)
        {
            vm.RecalcularSeleccion();
        }
    }
}
