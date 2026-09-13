using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using AutoExam.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>
/// Una clave de Gemini cargada, con la etiqueta de su pestania (US-042).
///
/// La etiqueta no se guarda en ningun lado: se recalcula por posicion cada vez que se agrega
/// o se borra una. Guardarla obligaria a mantener sincronizados el nombre y el orden, y el
/// orden es lo unico que importa de verdad — es el orden en que se prueban las claves cuando
/// una agota su cuota (RN-3).
/// </summary>
public partial class ClaveDeGemini : ObservableObject
{
    public ClaveDeGemini(string valor = "")
    {
        _valor = valor;
    }

    [ObservableProperty]
    private string _valor;

    [ObservableProperty]
    private string _etiqueta = "clave 1";
}

/// <summary>
/// El juego de claves de Gemini como pestanias, una por clave (US-042).
///
/// Reemplaza al campo unico donde las claves se pegaban separadas por comas. El cambio no es
/// solo de forma: con un solo cuadro de texto, agregar una clave nueva obligaba a editar un
/// renglon que ya tenia otras adentro —enmascarado, ademas— y una coma de menos borraba dos
/// claves de una. Con una pestania por clave, cada una se toca sin rozar a las demas.
///
/// El fallback de cuota no cambia en nada: <see cref="ComoLista"/> devuelve las claves en el
/// orden de las pestanias y eso es exactamente lo que ya consumia <see cref="AppConfig"/> y,
/// detras, el anillo de claves.
///
/// Vive en su propia clase y no adentro de Onboarding o de Ajustes porque las dos pantallas
/// editan lo mismo: el criterio pide que la forma de cargar la clave sea una sola en toda la
/// app, y dos copias del mismo comportamiento es como se termina teniendo dos.
/// </summary>
public partial class ClavesDeGemini : ObservableObject
{
    public ClavesDeGemini()
    {
        Claves.CollectionChanged += AlCambiarLaColeccion;
        Cargar(Array.Empty<string>());
    }

    /// <summary>Avisa que se agrego, se borro o se edito una clave. Lo usa Ajustes para su resumen.</summary>
    public event Action? Cambiaron;

    public ObservableCollection<ClaveDeGemini> Claves { get; } = new();

    [ObservableProperty]
    private ClaveDeGemini? _seleccionada;

    /// <summary>Texto del boton de agregar. Dice que numero le va a tocar a la pestania nueva.</summary>
    public string EtiquetaDeAgregar => $"+ clave {Claves.Count + 1}";

    /// <summary>
    /// Falso con una sola pestania: el criterio pide poder borrar "una que no sea la unica que
    /// queda". Sin claves la pantalla no tendria donde escribir.
    /// </summary>
    public bool PuedeQuitar => Claves.Count > 1;

    /// <summary>
    /// Deja las pestanias reflejando estas claves. Siempre queda al menos una, vacia si hace
    /// falta: la pantalla de configuracion inicial arranca justamente sin ninguna clave.
    /// </summary>
    public void Cargar(IEnumerable<string> claves)
    {
        var utiles = claves
            .Select(c => (c ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .ToList();

        Claves.Clear();

        foreach (string clave in utiles)
        {
            Agregada(new ClaveDeGemini(clave));
        }

        if (Claves.Count == 0)
        {
            Agregada(new ClaveDeGemini());
        }

        Seleccionada = Claves[0];
    }

    /// <summary>Las claves cargadas, en el orden de las pestanias y sin las vacias.</summary>
    public IReadOnlyList<string> ComoLista() => Claves
        .Select(c => c.Valor.Trim())
        .Where(c => c.Length > 0)
        .ToList();

    /// <summary>
    /// Las claves en el formato que entiende <see cref="AppConfig.EstablecerClaves"/>. Se
    /// unen con salto de linea y no con coma porque una clave nunca trae saltos, y asi una
    /// coma pegada por error adentro de una pestania no parte esa clave en dos.
    /// </summary>
    public string ComoTexto() => string.Join(Environment.NewLine, ComoLista());

    /// <summary>La clave que se usa para probar la conexion: la primera cargada.</summary>
    public string Primera() => ComoLista().FirstOrDefault() ?? string.Empty;

    [RelayCommand]
    private void Agregar()
    {
        var nueva = new ClaveDeGemini();
        Agregada(nueva);
        Seleccionada = nueva;
    }

    [RelayCommand]
    private void Quitar(ClaveDeGemini? clave)
    {
        if (clave is null || !PuedeQuitar || !Claves.Contains(clave))
        {
            return;
        }

        int indice = Claves.IndexOf(clave);
        clave.PropertyChanged -= AlEditarUnaClave;
        Claves.Remove(clave);

        // Queda seleccionada la que ocupo su lugar, o la ultima si se borro la del final:
        // dejar la seleccion en nada mostraria el panel vacio y parece que se borraron todas.
        Seleccionada = Claves[Math.Min(indice, Claves.Count - 1)];
    }

    private void Agregada(ClaveDeGemini clave)
    {
        clave.PropertyChanged += AlEditarUnaClave;
        Claves.Add(clave);
    }

    private void AlEditarUnaClave(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClaveDeGemini.Valor))
        {
            Cambiaron?.Invoke();
        }
    }

    private void AlCambiarLaColeccion(object? _, NotifyCollectionChangedEventArgs e)
    {
        for (int i = 0; i < Claves.Count; i++)
        {
            Claves[i].Etiqueta = $"clave {i + 1}";
        }

        OnPropertyChanged(nameof(EtiquetaDeAgregar));
        OnPropertyChanged(nameof(PuedeQuitar));
        Cambiaron?.Invoke();
    }
}
