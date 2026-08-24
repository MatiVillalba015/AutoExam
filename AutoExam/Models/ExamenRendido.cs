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

/// <summary>Un intento de examen ya corregido, tal como queda guardado en perfil.json.</summary>
public class ExamenRendido
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
}
