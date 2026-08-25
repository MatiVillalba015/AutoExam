using System.Windows;
using Wpf.Ui.Controls;

namespace AutoExam.Views;

/// <summary>
/// Tipo de dialogo: define el icono y si hay boton de "No" ademas del de
/// aceptar. Vive junto a la ventana porque solo DialogoVentana lo consume.
/// </summary>
public enum TipoDialogo
{
    Pregunta,
    Aviso,
    Error
}

/// <summary>
/// Ventana de dialogo propia de AutoExam (Fluent/Mica, US-002) que reemplaza a
/// MessageBox.Show para confirmaciones, avisos y errores. Los MessageBox.Show de
/// App.xaml.cs (crash del dispatcher) y MainWindow.xaml.cs (falla de arranque)
/// quedan fuera de alcance a proposito: son redes de seguridad para cuando el
/// propio pipeline de recursos/tema podria no estar en estado confiable
/// (ver specs/03-architecture.md, seccion 3).
/// </summary>
public partial class DialogoVentana : FluentWindow
{
    public bool Resultado { get; private set; }

    public DialogoVentana(TipoDialogo tipo, string titulo, string mensaje)
    {
        InitializeComponent();

        Owner = ObtenerPropietario();

        IconoTipo.Symbol = tipo switch
        {
            TipoDialogo.Pregunta => SymbolRegular.QuestionCircle24,
            TipoDialogo.Aviso => SymbolRegular.Info24,
            TipoDialogo.Error => SymbolRegular.ErrorCircle24,
            _ => SymbolRegular.Info24
        };

        TxtTituloBloque.Text = titulo;
        TxtMensajeBloque.Text = mensaje;

        if (tipo == TipoDialogo.Pregunta)
        {
            BtnCancelar.Visibility = Visibility.Visible;
            BtnCancelar.Content = "No";
            BtnAceptar.Content = "Si";
        }
        else
        {
            // Aviso y error no piden una decision reversible: cerrar de
            // cualquier forma (boton o la X) equivale a "leido", nunca a
            // "cancelado". Confirmar (Pregunta) deja Resultado en false por
            // default, asi que cerrar con la X tambien cuenta como "No".
            Resultado = true;
        }
    }

    private Window? ObtenerPropietario()
    {
        var actual = Application.Current?.MainWindow;
        return actual is { IsLoaded: true, IsVisible: true } ? actual : null;
    }

    private void BtnAceptar_Click(object sender, RoutedEventArgs e)
    {
        Resultado = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        Resultado = false;
        Close();
    }
}
