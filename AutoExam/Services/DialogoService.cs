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

    /// <summary>
    /// Selector multi-formato y multi-seleccion de fuentes (US-008/US-010, arquitectura
    /// Inc-4 §4.3). Devuelve las rutas elegidas, o null si el usuario cancelo.
    /// </summary>
    string[]? ElegirFuentes();

    void AbrirCarpeta(string ruta);
}

public class DialogoService : IDialogos
{
    // Confirmar/Aviso/Error usan una ventana propia (DialogoVentana, Fluent/Mica)
    // en vez de MessageBox.Show para que respeten el tema claro/oscuro de la app
    // (US-002). ElegirFuentes y AbrirCarpeta quedan fuera de alcance de US-002.
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

    public string[]? ElegirFuentes()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Elegi el material: PDF, Word, Excel, PowerPoint o imagenes",
            Filter = FiltroFuentes(),
            Multiselect = true
        };

        return dialogo.ShowDialog() == true ? dialogo.FileNames : null;
    }

    // Filtro combinado a partir de la lista unica de extensiones admitidas
    // (FactoriaExtractores.ExtensionesAdmitidas) para no repetirla aca.
    private static string FiltroFuentes()
    {
        static string Patron(params string[] exts) => string.Join(";", exts.Select(e => "*" + e));

        string todas = Patron(FactoriaExtractores.ExtensionesAdmitidas.ToArray());

        return string.Join("|",
            $"Todos los materiales ({todas})|{todas}",
            "PDF (*.pdf)|*.pdf",
            "Word (*.docx)|*.docx",
            "Excel (*.xlsx)|*.xlsx",
            "PowerPoint (*.pptx)|*.pptx",
            $"Imagenes ({Patron(".jpg", ".jpeg", ".png", ".heic", ".heif")})|{Patron(".jpg", ".jpeg", ".png", ".heic", ".heif")}");
    }

    public void AbrirCarpeta(string ruta)
    {
        RutasApp.AsegurarCarpetas();
        Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
    }
}
