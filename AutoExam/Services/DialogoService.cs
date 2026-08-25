using System.Diagnostics;
using AutoExam.Views;
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
    // Confirmar/Aviso/Error usan una ventana propia (DialogoVentana, Fluent/Mica)
    // en vez de MessageBox.Show para que respeten el tema claro/oscuro de la app
    // (US-002). ElegirPdf y AbrirCarpeta quedan fuera de alcance de US-002 y
    // siguen igual.
    public bool Confirmar(string mensaje, string titulo = "AutoExam")
        => Mostrar(TipoDialogo.Pregunta, titulo, mensaje);

    public void Aviso(string titulo, string mensaje)
        => Mostrar(TipoDialogo.Aviso, titulo, mensaje);

    public void Error(string titulo, string mensaje)
        => Mostrar(TipoDialogo.Error, titulo, mensaje);

    private static bool Mostrar(TipoDialogo tipo, string titulo, string mensaje)
    {
        var dialogo = new DialogoVentana(tipo, titulo, mensaje);
        dialogo.ShowDialog();
        return dialogo.Resultado;
    }

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
