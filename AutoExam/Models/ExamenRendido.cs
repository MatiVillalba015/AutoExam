using System.Text.Json.Serialization;

namespace AutoExam.Models;

/// <summary>Resultado agregado de una ronda de revancha.</summary>
public class RondaRevancha
{
    public int Numero { get; set; }
    public int TotalPreguntas { get; set; }
    public int Correctas { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string Descripcion => $"Revancha #{Numero}: {Correctas}/{TotalPreguntas} correctas";
}

/// <summary>
/// Un intento de examen ya corregido, tal como queda guardado en perfil.json.
///
/// Hereda de <see cref="ObservableBase"/> desde US-026 para que destildar todos los examenes
/// de una se refleje en las casillas de la lista. <c>ObservableBase</c> no agrega propiedades
/// publicas, asi que lo que se escribe en perfil.json no cambia.
/// </summary>
public class ExamenRendido : ObservableBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime Fecha { get; set; } = DateTime.Now;

    public string LibroId { get; set; } = string.Empty;
    public string LibroTitulo { get; set; } = string.Empty;
    public string Materia { get; set; } = string.Empty;

    /// <summary>Texto legible del alcance: modulos, rango de paginas y/o tema libre.</summary>
    public string AlcanceDescripcion { get; set; } = string.Empty;

    public int TotalPreguntas { get; set; }
    public int Correctas { get; set; }
    public int Incorrectas { get; set; }
    public int Salteadas { get; set; }

    public double PorcentajeAciertos { get; set; }
    public int NotaUBA { get; set; }
    public string Condicion { get; set; } = string.Empty;
    public bool Aprobado { get; set; }

    public int DuracionSegundos { get; set; }

    public List<RondaRevancha> Revanchas { get; set; } = new();

    /// <summary>True cuando el bucle de revancha llego al 100% de aciertos.</summary>
    public bool CompletadoAl100 { get; set; }

    /// <summary>
    /// Detalle completo del intento: cada pregunta con lo que el alumno marco, cual era la
    /// correcta y el analisis por opcion (RN-25).
    ///
    /// Hasta US-025 el historial guardaba solo el resumen numerico, asi que el examen se
    /// podia mirar pero no repasar: a la semana siguiente no quedaba forma de saber en que
    /// te habias equivocado. Guardar el detalle es lo que convierte al historial en material
    /// de estudio, y ademas es la fuente de la que US-026 saca preguntas para un repaso.
    ///
    /// Vacia en los examenes rendidos con una version anterior. Ese caso NO se reconstruye
    /// (RN-25): se informa como tal (RN-26, ver <see cref="TieneDetalle"/>).
    /// </summary>
    public List<Pregunta> Preguntas { get; set; } = new();

    /// <summary>
    /// True si este intento es un examen de repaso armado con preguntas de otros examenes
    /// (US-026), y no un examen generado desde material con IA.
    ///
    /// Importa para dos cosas: se muestra distinto en la lista, y no se puede usar como
    /// fuente de otro repaso — encadenar repasos de repasos esta fuera de alcance.
    /// </summary>
    public bool EsRepaso { get; set; }

    /// <summary>Titulos de los examenes que alimentaron este repaso, en el orden elegido.</summary>
    public List<string> ExamenesDeOrigen { get; set; } = new();

    /// <summary>
    /// RN-26: false en los examenes rendidos antes de US-025. Se deduce de la lista vacia y
    /// no de un numero de version, porque un examen siempre tiene al menos una pregunta:
    /// no hay forma de que un intento nuevo llegue aca con el detalle vacio.
    /// </summary>
    [JsonIgnore]
    public bool TieneDetalle => Preguntas.Count > 0;

    /// <summary>
    /// Marcado en el Historial para entrar en un examen de repaso (US-026). No se persiste:
    /// es una eleccion de un momento, no un atributo del intento.
    /// </summary>
    [JsonIgnore]
    public bool Seleccionado
    {
        get => _seleccionado;
        set => Set(ref _seleccionado, value);
    }

    private bool _seleccionado;

    /// <summary>
    /// True si este examen puede aportar preguntas a un repaso (US-026): tiene el detalle
    /// guardado y no es el mismo un repaso.
    /// </summary>
    [JsonIgnore]
    public bool PuedeAlimentarRepaso => TieneDetalle && !EsRepaso;

    [JsonIgnore]
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");

    [JsonIgnore]
    public string NotaTexto => $"{NotaUBA} ({PorcentajeAciertos:0.#}%)";

    [JsonIgnore]
    public string DetalleTexto =>
        $"{Correctas} correctas · {Incorrectas} incorrectas · {Salteadas} salteadas · {DuracionTexto}";

    [JsonIgnore]
    public string DuracionTexto => TimeSpan.FromSeconds(DuracionSegundos).ToString(@"hh\:mm\:ss");

    [JsonIgnore]
    public string TituloTexto => string.IsNullOrWhiteSpace(AlcanceDescripcion)
        ? LibroTitulo
        : $"{LibroTitulo} — {AlcanceDescripcion}";

    /// <summary>Etiqueta corta para distinguir un repaso en la lista del historial (US-026).</summary>
    [JsonIgnore]
    public string EtiquetaTipo => EsRepaso ? "Repaso combinado" : string.Empty;

    /// <summary>
    /// Color de la materia de este examen (US-027), para la franja de su tarjeta en el
    /// historial (US-030 / RN-34).
    ///
    /// Se resuelve por nombre al dibujar y NO se guarda con el examen: es exactamente lo que
    /// pide RN-30, y es lo que hace que cambiarle el color a "Fisiologia" repinte tambien los
    /// examenes de fisiologia rendidos hace meses. Guardar el color con cada intento los
    /// dejaria congelados en el color que la materia tenia ese dia.
    /// </summary>
    [JsonIgnore]
    public string ColorMateria => PaletaMaterias.ColorDe(Materia);

    /// <summary>Vuelve a leer el color de la materia. La llama el historial cuando cambia la paleta.</summary>
    public void NotificarColorMateria() => OnPropertyChanged(nameof(ColorMateria));
}
