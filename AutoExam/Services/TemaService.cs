using System.Windows;
using Wpf.Ui.Appearance;

namespace AutoExam.Services;

/// <summary>
/// Cambia el tema en dos frentes: los controles de WPF-UI y los tokens propios
/// de AutoExam. Los diccionarios de tokens definen las mismas claves, asi que
/// intercambiar uno por el otro repinta toda la app sin tocar un solo estilo.
/// </summary>
public static class TemaService
{
    // Pack URI absoluto y no ruta relativa: una relativa se resuelve contra el
    // ensamblado de entrada, asi que se rompe apenas AutoExam deja de serlo
    // (por ejemplo al cargarlo desde un arnes de pruebas).
    private const string RutaOscuro = "pack://application:,,,/AutoExam;component/Theme/Tokens.Oscuro.xaml";
    private const string RutaClaro = "pack://application:,,,/AutoExam;component/Theme/Tokens.Claro.xaml";

    public static bool EsOscuro { get; private set; } = true;

    public static void Aplicar(bool oscuro)
    {
        EsOscuro = oscuro;

        ApplicationThemeManager.Apply(oscuro ? ApplicationTheme.Dark : ApplicationTheme.Light);
        IntercambiarTokens(oscuro ? RutaOscuro : RutaClaro);
    }

    private static void IntercambiarTokens(string ruta)
    {
        var recursos = Application.Current?.Resources;
        if (recursos is null)
        {
            return;
        }

        var nuevo = new ResourceDictionary { Source = new Uri(ruta, UriKind.Absolute) };

        // Se reemplaza el diccionario de tokens anterior, nunca se acumulan:
        // dos diccionarios con las mismas claves harian ganar al ultimo agregado
        // y el tema quedaria dependiendo del orden de las llamadas.
        var viejos = recursos.MergedDictionaries
            .Where(d => d.Source is not null &&
                        d.Source.OriginalString.Contains("Tokens.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var viejo in viejos)
        {
            recursos.MergedDictionaries.Remove(viejo);
        }

        // Los tokens van antes que Estilos.xaml en el orden de busqueda, pero como
        // los estilos usan DynamicResource el orden de insercion no los afecta.
        recursos.MergedDictionaries.Insert(0, nuevo);
    }
}
