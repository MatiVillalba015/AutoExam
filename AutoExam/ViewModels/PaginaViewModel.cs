using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoExam.ViewModels;

/// <summary>
/// Lo minimo que el shell necesita saber de una pagina para dibujarla en la
/// navegacion lateral. El shell no conoce ningun tipo concreto de pagina.
/// </summary>
public abstract partial class PaginaViewModel : ObservableObject
{
    protected PaginaViewModel(string clave, string titulo, string icono)
    {
        Clave = clave;
        Titulo = titulo;
        Icono = icono;
    }

    public string Clave { get; }

    public string Titulo { get; }

    /// <summary>Nombre del simbolo de WPF-UI, ej. "Library24".</summary>
    public string Icono { get; }

    /// <summary>Dato corto que se muestra bajo el titulo en la navegacion.</summary>
    [ObservableProperty]
    private string _insignia = string.Empty;

    [ObservableProperty]
    private bool _habilitada = true;

    /// <summary>La marca el shell. La navegacion la usa para pintar la pagina activa.</summary>
    [ObservableProperty]
    private bool _esActual;

    /// <summary>Se llama cada vez que la pagina pasa a estar visible.</summary>
    public virtual void AlEntrar()
    {
    }
}

/// <summary>Navegacion y barra de estado, vistas desde una pagina hija.</summary>
public interface INavegacion
{
    void IrA(string clave);

    void Estado(string texto);

    /// <summary>Refresca la etiqueta "Gemini: modelo" de la barra inferior.</summary>
    void RefrescarEstadoApi();
}
