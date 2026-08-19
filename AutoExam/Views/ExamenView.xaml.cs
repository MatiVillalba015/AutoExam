using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoExam.Views;

public partial class ExamenView : UserControl
{
    public ExamenView()
    {
        InitializeComponent();

        // Los atajos del examen viven en InputBindings, y los InputBindings solo
        // disparan si el foco esta dentro de la vista. Al aparecer, se lo lleva.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                Focus();
            }
        };

        // Un click en cualquier parte devuelve el foco a la vista para que los
        // atajos sigan andando despues de tocar un boton o seleccionar texto.
        PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (Keyboard.FocusedElement is not TextBox { IsReadOnly: false })
            {
                Focus();
            }
        };
    }
}
