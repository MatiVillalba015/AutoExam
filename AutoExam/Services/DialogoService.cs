using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace AutoExam.Services;

/// <summary>
/// Todo lo que un ViewModel necesita del sistema operativo o de una ventana
/// modal. Existir como interfaz es lo que permite testear los ViewModels sin
/// que se abra un cuadro de dialogo y se cuelgue la prueba.
/// </summary>
public interface IDialogos
{
    bool Confirmar(string mensaje, string titulo = "AutoExam");

    void Aviso(string titulo, string mensaje);

    void Error(string titulo, string mensaje);

    string? ElegirPdf();

    void AbrirCarpeta(string ruta);
}

public class DialogoService : IDialogos
{
    public bool Confirmar(string mensaje, string titulo = "AutoExam")
        => MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question)
           == MessageBoxResult.Yes;

    public void Aviso(string titulo, string mensaje)
        => MessageBox.Show($"{titulo}\n\n{mensaje}", "AutoExam", MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string titulo, string mensaje)
        => MessageBox.Show($"{titulo}\n\n{mensaje}", "AutoExam", MessageBoxButton.OK, MessageBoxImage.Error);

    public string? ElegirPdf()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Elegi el PDF del libro",
            Filter = "Documentos PDF (*.pdf)|*.pdf",
            Multiselect = false
        };

        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }

    public void AbrirCarpeta(string ruta)
    {
        RutasApp.AsegurarCarpetas();
        Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
    }
}
