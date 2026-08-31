using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoExam.Services;

namespace AutoExam.Behaviors;

/// <summary>
/// Permite soltar uno o varios archivos sobre cualquier control y enviar sus rutas
/// a un comando del ViewModel, sin code-behind en la vista.
///
/// Arrastrar nunca es la unica via: el control que use esto tiene que ofrecer
/// tambien un click que abra el selector de archivos (WCAG 2.2, 2.5.7).
///
/// Multi-archivo (arquitectura Inc-4 §3/§4.4): el drop de un set de imagenes tiene
/// que llegar entero al ViewModel, asi que el comando se ejecuta SIEMPRE con un
/// <c>string[]</c> (array de una sola ruta para los formatos de archivo unico).
/// </summary>
public static class SoltarArchivo
{
    /// <summary>Comando que recibe las rutas de los archivos soltados (<c>string[]</c>).</summary>
    public static readonly DependencyProperty ComandoProperty =
        DependencyProperty.RegisterAttached(
            "Comando", typeof(ICommand), typeof(SoltarArchivo),
            new PropertyMetadata(null, AlCambiarComando));

    /// <summary>
    /// Extensiones aceptadas, con punto, separadas por espacios ("<c>.pdf .docx .jpg</c>").
    /// Vacio acepta cualquier archivo existente. Por defecto, todas las que admite la app.
    /// </summary>
    public static readonly DependencyProperty ExtensionesProperty =
        DependencyProperty.RegisterAttached(
            "Extensiones", typeof(string), typeof(SoltarArchivo),
            new PropertyMetadata(string.Join(" ", FactoriaExtractores.ExtensionesAdmitidas)));

    /// <summary>True mientras hay al menos un archivo valido encima. Para resaltar la zona.</summary>
    public static readonly DependencyProperty EncimaProperty =
        DependencyProperty.RegisterAttached(
            "Encima", typeof(bool), typeof(SoltarArchivo), new PropertyMetadata(false));

    public static ICommand? GetComando(DependencyObject d) => (ICommand?)d.GetValue(ComandoProperty);
    public static void SetComando(DependencyObject d, ICommand? valor) => d.SetValue(ComandoProperty, valor);

    public static string GetExtensiones(DependencyObject d) => (string)d.GetValue(ExtensionesProperty);
    public static void SetExtensiones(DependencyObject d, string valor) => d.SetValue(ExtensionesProperty, valor);

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

        // Se acepta el drop de cualquier archivo (Effects=Copy) para que el evento Drop
        // llegue al ViewModel aunque la extension no sea admitida: si se rechazara aca con
        // Effects=None, WPF no dispararia Drop y el usuario no recibiria ningun mensaje
        // (NFR-37 exige avisar la causa tambien al arrastrar). El resaltado de la zona
        // (Encima) si distingue: solo se enciende si hay al menos un archivo valido.
        bool hayArchivos =
            e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 };

        e.Effects = hayArchivos ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        SetEncima(destino, ArchivosValidos(destino, e.Data).Length > 0);
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

        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] soltadas)
        {
            return;
        }

        // Se pasan TODAS las rutas soltadas que existen en disco, SIN filtrar por
        // extension: el filtrado y el aviso por formato no admitido los hace el
        // ViewModel (mismo canal Avisar/InfoBar que el selector de archivos), para
        // no rechazar en silencio al arrastrar un .doc/.xls/.ppt o algo no soportado
        // (NFR-37). ArchivosValidos (whitelist) se usa solo para el resaltado del
        // DragOver.
        string[] rutas = soltadas.Where(File.Exists).ToArray();
        if (rutas.Length == 0)
        {
            return;
        }

        var comando = GetComando(destino);
        if (comando is not null && comando.CanExecute(rutas))
        {
            comando.Execute(rutas);
        }
    }

    /// <summary>
    /// Rutas del FileDrop que existen en disco y tienen una extension admitida por el
    /// elemento, en el mismo orden en que se soltaron (NFR-43). Nunca null.
    /// </summary>
    private static string[] ArchivosValidos(DependencyObject destino, IDataObject datos)
    {
        if (!datos.GetDataPresent(DataFormats.FileDrop) ||
            datos.GetData(DataFormats.FileDrop) is not string[] rutas)
        {
            return Array.Empty<string>();
        }

        var admitidas = GetExtensiones(destino)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rutas.Where(r =>
            File.Exists(r) &&
            (admitidas.Count == 0 || admitidas.Contains(Path.GetExtension(r))))
            .ToArray();
    }
}
