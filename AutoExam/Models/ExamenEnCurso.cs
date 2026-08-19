using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace AutoExam.Models;

/// <summary>
/// Estado en memoria del examen que se esta rindiendo, incluidas las rondas del Modo Revancha.
/// </summary>
public class ExamenEnCurso : ObservableBase
{
    private int _indiceActual;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public ObservableCollection<Pregunta> Preguntas { get; } = new();

    public string LibroId { get; set; } = string.Empty;
    public string LibroTitulo { get; set; } = string.Empty;
    public string Materia { get; set; } = string.Empty;
    public string AlcanceDescripcion { get; set; } = string.Empty;

    public DateTime Inicio { get; set; } = DateTime.Now;

    /// <summary>0 = intento original. 1, 2, 3... = rondas de revancha.</summary>
    public int Ronda { get; set; }

    /// <summary>Registro persistido del intento original; las revanchas lo van actualizando.</summary>
    public ExamenRendido? Registro { get; set; }

    public int IndiceActual
    {
        get => _indiceActual;
        set
        {
            int limite = Math.Max(0, Preguntas.Count - 1);
            if (Set(ref _indiceActual, Math.Clamp(value, 0, limite)))
            {
                NotificarNavegacion();
            }
        }
    }

    [JsonIgnore]
    public Pregunta? Actual => Preguntas.Count == 0 ? null : Preguntas[Math.Clamp(IndiceActual, 0, Preguntas.Count - 1)];

    [JsonIgnore]
    public bool EsRevancha => Ronda > 0;

    [JsonIgnore]
    public string TituloRonda => EsRevancha
        ? $"Modo Revancha · Ronda {Ronda}"
        : "Examen en curso";

    [JsonIgnore]
    public string TextoProgreso => Preguntas.Count == 0
        ? "Sin preguntas"
        : $"Pregunta {IndiceActual + 1} de {Preguntas.Count}";

    [JsonIgnore]
    public int Respondidas => Preguntas.Count(p => p.Estado == EstadoPreguntaEnum.Respondida);

    [JsonIgnore]
    public int Salteadas => Preguntas.Count(p => p.Estado == EstadoPreguntaEnum.Salteada);

    [JsonIgnore]
    public int SinResponder => Preguntas.Count(p => p.Estado == EstadoPreguntaEnum.SinResponder);

    [JsonIgnore]
    public double PorcentajeAvance => Preguntas.Count == 0
        ? 0
        : (Respondidas + Salteadas) * 100d / Preguntas.Count;

    [JsonIgnore]
    public bool PuedeAnterior => IndiceActual > 0;

    [JsonIgnore]
    public bool PuedeSiguiente => IndiceActual < Preguntas.Count - 1;

    [JsonIgnore]
    public TimeSpan Transcurrido => DateTime.Now - Inicio;

    public void NotificarNavegacion()
    {
        OnPropertyChanged(nameof(Actual));
        OnPropertyChanged(nameof(TextoProgreso));
        OnPropertyChanged(nameof(PuedeAnterior));
        OnPropertyChanged(nameof(PuedeSiguiente));
    }

    public void NotificarContadores()
    {
        OnPropertyChanged(nameof(Respondidas));
        OnPropertyChanged(nameof(Salteadas));
        OnPropertyChanged(nameof(SinResponder));
        OnPropertyChanged(nameof(PorcentajeAvance));
    }

    /// <summary>Salta a la primera pregunta que quedo sin responder; devuelve false si no hay ninguna.</summary>
    public bool IrAPrimeraSinResponder()
    {
        for (int i = 0; i < Preguntas.Count; i++)
        {
            if (Preguntas[i].Estado == EstadoPreguntaEnum.SinResponder)
            {
                IndiceActual = i;
                return true;
            }
        }

        return false;
    }
}
