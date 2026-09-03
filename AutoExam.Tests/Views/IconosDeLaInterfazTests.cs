using System.IO;
using System.Text.RegularExpressions;
using AutoExam.Tests.Infraestructura;
using Wpf.Ui.Controls;

namespace AutoExam.Tests.Views;

/// <summary>
/// RN-38 — el contenido de una tarjeta o botón (ícono incluido) nunca depende del hover para
/// verse.
///
/// El bug que motivó esta suite: la tarjeta "Generar examen" del menú aparecía completamente
/// en blanco hasta pasar el mouse por encima. La causa era un nombre de ícono que no existe en
/// la versión de WPF-UI que usa el proyecto. Los íconos se pasan como STRING (para que
/// PaginaViewModel no dependa del enum de la librería), así que un nombre inventado compila
/// perfecto y sólo falla al dibujar.
///
/// Estos tests cierran ese agujero: cada nombre de ícono que la app entrega tiene que existir
/// de verdad en <see cref="SymbolRegular"/>.
/// </summary>
public class IconosDeLaInterfazTests
{
    /// <summary>
    /// Nombres de ícono que aparecen en el código: los de las páginas
    /// (<c>base("clave", "Titulo", "Icono")</c>) y los de los accesos del menú.
    /// </summary>
    private static IEnumerable<(string Archivo, string Icono)> IconosDeclarados()
    {
        string[] archivos =
        {
            "AutoExam/ViewModels/AjustesViewModel.cs",
            "AutoExam/ViewModels/BibliotecaViewModel.cs",
            "AutoExam/ViewModels/ExamenViewModel.cs",
            "AutoExam/ViewModels/HistorialViewModel.cs",
            "AutoExam/ViewModels/AsistenteViewModel.cs",
            "AutoExam/ViewModels/InicioViewModel.cs",
            "AutoExam/ViewModels/ShellViewModel.cs",
        };

        // Un nombre de símbolo de Fluent siempre termina en el tamaño: Library24, Wand24...
        var patron = new Regex("\"([A-Z][A-Za-z]*\\d{2})\"", RegexOptions.Compiled);

        foreach (string archivo in archivos)
        {
            string codigo = File.ReadAllText(ArchivoFuenteHelper.RutaFuente(archivo));

            foreach (Match m in patron.Matches(codigo))
            {
                yield return (archivo, m.Groups[1].Value);
            }
        }
    }

    [Fact]
    public void CadaIconoDeclaradoEnCodigo_ExisteEnLaLibreria_RN38()
    {
        var declarados = IconosDeclarados().ToList();

        Assert.NotEmpty(declarados);

        var inexistentes = declarados
            .Where(d => !Enum.IsDefined(typeof(SymbolRegular), d.Icono))
            .Select(d => $"{d.Icono} (en {d.Archivo})")
            .ToList();

        Assert.True(inexistentes.Count == 0,
            "Estos nombres de ícono no existen en SymbolRegular, así que el control queda mudo " +
            "y su tarjeta se dibuja vacía sin ningún error visible:\n  " +
            string.Join("\n  ", inexistentes));
    }

    [Fact]
    public void CadaIconoUsadoEnXaml_ExisteEnLaLibreria_RN38()
    {
        // Los íconos escritos directo en el XAML (Symbol="Home24") fallan igual de silenciosos.
        var patron = new Regex(
            "(?:Symbol|Icon)\\s*=\\s*\"(?:\\{ui:SymbolIcon\\s+)?([A-Z][A-Za-z]*\\d{2})",
            RegexOptions.Compiled);

        var raiz = new DirectoryInfo(Path.GetDirectoryName(
            ArchivoFuenteHelper.RutaFuente("AutoExam/App.xaml"))!);

        var inexistentes = new List<string>();

        foreach (var archivo in raiz.GetFiles("*.xaml", SearchOption.AllDirectories))
        {
            if (archivo.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                archivo.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            foreach (Match m in patron.Matches(File.ReadAllText(archivo.FullName)))
            {
                string icono = m.Groups[1].Value;

                if (!Enum.IsDefined(typeof(SymbolRegular), icono))
                {
                    inexistentes.Add($"{icono} (en {archivo.Name})");
                }
            }
        }

        Assert.True(inexistentes.Count == 0,
            "Íconos inexistentes en el XAML:\n  " + string.Join("\n  ", inexistentes));
    }
}
