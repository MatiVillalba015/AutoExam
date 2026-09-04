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

    /// <summary>
    /// True si este examen se armo mezclando preguntas de intentos anteriores (US-026) en
    /// vez de generarse desde material con IA. Viaja hasta el registro del historial para
    /// que el repaso quede identificado como tal y no se lo pueda usar como fuente de otro.
    /// </summary>
    public bool EsRepaso { get; set; }

    /// <summary>Titulos de los examenes que alimentaron este repaso, en el orden elegido.</summary>
    public List<string> ExamenesDeOrigen { get; set; } = new();

    /// <summary>
    /// Tiempo total para todo el examen, en segundos. 0 = sin limite, que es el modo de
    /// siempre (US-034).
    ///
    /// Es un limite TOTAL y no por pregunta a proposito: lo que el alumno esta practicando es
    /// administrar el tiempo de un parcial, y eso incluye decidir cuanto gastar en cada
    /// pregunta. Un limite por pregunta le sacaria justamente esa decision.
    ///
    /// Vive en el examen y no en la configuracion porque se elige por examen: se puede rendir
    /// uno a tiempo y el siguiente sin reloj.
    /// </summary>
    public int LimiteSegundos { get; set; }

    [JsonIgnore]
    public bool ConCronometro => LimiteSegundos > 0;

    /// <summary>
    /// Cuanto queda. Nunca baja de cero: mostrar un negativo despues de que el examen ya se
    /// entrego solo, aunque sea por un instante, se lee como un error.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Restante => ConCronometro
        ? TimeSpan.FromSeconds(Math.Max(0, LimiteSegundos - Transcurrido.TotalSeconds))
        : TimeSpan.Zero;

    [JsonIgnore]
    public bool SeAcaboElTiempo => ConCronometro && Restante <= TimeSpan.Zero;

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
