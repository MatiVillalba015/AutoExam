namespace AutoExam.Models;

/// <summary>
/// Baldosa numerada del navegador de preguntas. Expone estado, no color: la
/// traduccion a pinceles la hace un converter contra el tema activo, para que
/// la paleta viva en un unico lugar (Theme/Tokens.*.xaml).
/// </summary>
public class NavegadorItem : ObservableBase
{
    private bool _esActual;

    public int Numero { get; init; }
    public int Indice { get; init; }
    public Pregunta Pregunta { get; init; } = null!;

    public bool EsActual
    {
        get => _esActual;
        set => Set(ref _esActual, value);
    }

    public EstadoPreguntaEnum Estado => Pregunta.Estado;

    /// <summary>Texto que lee un lector de pantalla en lugar del numero suelto.</summary>
    public string Accesible => $"Pregunta {Numero}, {Pregunta.EtiquetaEstado}";

    public void Refrescar()
    {
        OnPropertyChanged(nameof(Estado));
        OnPropertyChanged(nameof(Accesible));
    }
}
