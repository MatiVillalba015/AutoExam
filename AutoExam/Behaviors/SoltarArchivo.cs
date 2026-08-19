using System.IO;
using System.Windows;
using System.Windows.Input;

namespace AutoExam.Behaviors;

/// <summary>
/// Permite soltar un archivo sobre cualquier control y enviar su ruta a un
/// comando del ViewModel, sin code-behind en la vista.
///
/// Arrastrar nunca es la unica via: el control que use esto tiene que ofrecer
/// tambien un click que abra el selector de archivos (WCAG 2.2, 2.5.7).
/// </summary>
public static class SoltarArchivo
{
    /// <summary>Comando que recibe la ruta del archivo soltado.</summary>
    public static readonly DependencyProperty ComandoProperty =
        DependencyProperty.RegisterAttached(
            "Comando", typeof(ICommand), typeof(SoltarArchivo),
            new PropertyMetadata(null, AlCambiarComando));

    /// <summary>Extension aceptada, con punto. Vacio acepta cualquiera.</summary>
    public static readonly DependencyProperty ExtensionProperty =
        DependencyProperty.RegisterAttached(
            "Extension", typeof(string), typeof(SoltarArchivo), new PropertyMetadata(".pdf"));

    /// <summary>True mientras hay un archivo valido encima. Para resaltar la zona.</summary>
    public static readonly DependencyProperty EncimaProperty =
        DependencyProperty.RegisterAttached(
            "Encima", typeof(bool), typeof(SoltarArchivo), new PropertyMetadata(false));

    public static ICommand? GetComando(DependencyObject d) => (ICommand?)d.GetValue(ComandoProperty);
    public static void SetComando(DependencyObject d, ICommand? valor) => d.SetValue(ComandoProperty, valor);

    public static string GetExtension(DependencyObject d) => (string)d.GetValue(ExtensionProperty);
    public static void SetExtension(DependencyObject d, string valor) => d.SetValue(ExtensionProperty, valor);

    public static bool GetEncima(DependencyObject d) => (bool)d.GetValue(EncimaProperty);
    public static void SetEncima(DependencyObject d, bool valor) => d.SetValue(EncimaProperty, valor);

    private static void AlCambiarComando(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement elemento)
        {
            return;
        }

        elemento.DragOver -= AlArrastrarEncima;
        elemento.DragLeave -= AlSalir;
        elemento.Drop -= AlSoltar;

        if (e.NewValue is null)
        {
            elemento.AllowDrop = false;
            return;
        }

        elemento.AllowDrop = true;
        elemento.DragOver += AlArrastrarEncima;
        elemento.DragLeave += AlSalir;
        elemento.Drop += AlSoltar;
    }

    private static void AlArrastrarEncima(object sender, DragEventArgs e)
    {
        if (sender is not DependencyObject destino)
        {
            return;
        }

        bool valido = PrimerArchivoValido(destino, e.Data) is not null;

        e.Effects = valido ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        SetEncima(destino, valido);
    }

    private static void AlSalir(object sender, DragEventArgs e)
    {
        if (sender is DependencyObject destino)
        {
            SetEncima(destino, false);
        }
    }

    private static void AlSoltar(object sender, DragEventArgs e)
    {
        if (sender is not DependencyObject destino)
        {
            return;
        }

        SetEncima(destino, false);
        e.Handled = true;

        string? ruta = PrimerArchivoValido(destino, e.Data);
        if (ruta is null)
        {
            return;
        }

        var comando = GetComando(destino);
        if (comando is not null && comando.CanExecute(ruta))
        {
            comando.Execute(ruta);
        }
    }

    private static string? PrimerArchivoValido(DependencyObject destino, IDataObject datos)
    {
        if (!datos.GetDataPresent(DataFormats.FileDrop) ||
            datos.GetData(DataFormats.FileDrop) is not string[] rutas)
        {
            return null;
        }

        string extension = GetExtension(destino);

        return rutas.FirstOrDefault(r =>
            File.Exists(r) &&
            (string.IsNullOrEmpty(extension) ||
             string.Equals(Path.GetExtension(r), extension, StringComparison.OrdinalIgnoreCase)));
    }
}
