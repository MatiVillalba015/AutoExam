using System.Text.Json.Serialization;

namespace AutoExam.Models;

/// <summary>
/// Una materia de la biblioteca (US-023) con su color de identidad (US-027).
///
/// Hasta US-027 una materia era solamente un nombre. Ahora es una entidad, porque el color
/// tiene que vivir en algun lado y RN-30 es explicita en donde: en la materia, no en el
/// examen. Esa diferencia es lo que hace que cambiarle el color a "Fisiologia" repinte
/// tambien los examenes de fisiologia que ya estaban rendidos, en vez de dejar cada examen
/// congelado con el color que la materia tenia el dia que se genero.
///
/// El vinculo con libros y examenes sigue siendo el NOMBRE (<c>Libro.Materia</c>,
/// <c>ExamenRendido.Materia</c>), no un id: es lo que ya existia y lo que hace que el
/// material anterior a US-023 se migre solo (RN-22).
/// </summary>
public class Materia : ObservableBase
{
    private string _nombre = string.Empty;
    private string _color = string.Empty;

    public string Nombre
    {
        get => _nombre;
        set => Set(ref _nombre, value);
    }

    /// <summary>
    /// Color en formato <c>#RRGGBB</c>, tomado de <see cref="PaletaMaterias"/>. Nunca queda
    /// vacio: si el alumno no elige uno, el alta le asigna el primero sin usar (US-027).
    /// </summary>
    public string Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    [JsonIgnore]
    public bool TieneColor => !string.IsNullOrWhiteSpace(Color);

    public override string ToString() => Nombre;
}

/// <summary>
/// Paleta acotada de colores de materia (US-027 / RN-31) y registro para resolverlos en
/// tiempo de dibujado.
///
/// <b>Por que una paleta cerrada y no un selector RGB:</b> el color de materia se dibuja como
/// franja y como acento sobre fondo claro y sobre fondo oscuro. Un selector libre deja elegir
/// un amarillo que desaparece en el tema claro o un azul marino que se pierde en el oscuro, y
/// el alumno no tiene por que estar calculando contraste. Estos diez estan elegidos para
/// leerse en los dos temas.
///
/// <b>Por que ninguno es rojo ni verde puro:</b> esos dos tonos ya significan algo en la app
/// —correcta e incorrecta— y RN-27 pide no tocar ese significado. Una materia en verde
/// bandera al lado de una respuesta correcta compite con la unica lectura de color que
/// importa mientras se corrige.
///
/// <b>Por que el registro es estatico:</b> el color se resuelve al dibujar, a partir del
/// nombre de la materia (RN-30). Un examen rendido guarda el nombre de su materia, no su
/// color; para pintarlo hace falta poder preguntar "que color tiene hoy Fisiologia" desde una
/// plantilla, sin que cada modelo tenga que arrastrar una referencia al servicio.
/// </summary>
public static class PaletaMaterias
{
    /// <summary>Color de las materias sin color asignado y de "Sin materia".</summary>
    public const string Neutro = "#7E8AA0";

    /// <summary>
    /// Los colores ofrecibles, en el orden en que se reparten automaticamente. El orden no es
    /// alfabetico ni casual: son tonos consecutivos bien separados en la rueda, para que dos
    /// materias creadas una detras de otra no salgan parecidas.
    /// </summary>
    public static readonly IReadOnlyList<string> Colores = new[]
    {
        "#8B7BF0", // violeta
        "#3EB4C9", // cian
        "#E2814A", // naranja
        "#6C8CF5", // indigo
        "#2FA98A", // teal
        "#E06B9A", // rosa
        "#D9A036", // ambar
        "#B96BD4", // magenta
        "#7BAE3F", // lima
        Neutro,    // pizarra
    };

    /// <summary>Nombre legible de cada color, para el lector de pantalla y el tooltip.</summary>
    public static string NombreDe(string color) => color?.ToUpperInvariant() switch
    {
        "#8B7BF0" => "Violeta",
        "#3EB4C9" => "Cian",
        "#E2814A" => "Naranja",
        "#6C8CF5" => "Indigo",
        "#2FA98A" => "Verde azulado",
        "#E06B9A" => "Rosa",
        "#D9A036" => "Ambar",
        "#B96BD4" => "Magenta",
        "#7BAE3F" => "Lima",
        "#7E8AA0" => "Pizarra",
        _ => "Color"
    };

    private static readonly Dictionary<string, string> PorNombre =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Se dispara cuando cambia el mapa de colores. Lo escuchan las pantallas que dibujan
    /// materias para volver a pedir el color de sus items: sin esto, cambiar el color de una
    /// materia no repintaria el historial ya cargado en memoria (RN-30).
    /// </summary>
    public static event Action? Cambio;

    /// <summary>Reemplaza el mapa con las materias actuales y avisa a quien este dibujando.</summary>
    public static void Registrar(IEnumerable<Materia> materias)
    {
        PorNombre.Clear();

        foreach (var materia in materias ?? Array.Empty<Materia>())
        {
            if (!string.IsNullOrWhiteSpace(materia.Nombre) && materia.TieneColor)
            {
                PorNombre[materia.Nombre.Trim()] = materia.Color;
            }
        }

        Cambio?.Invoke();
    }

    /// <summary>
    /// Color de una materia por su nombre. Una materia desconocida —borrada, o el nombre
    /// que quedo escrito en un examen viejo— cae en el neutro en vez de romper el dibujado.
    /// </summary>
    public static string ColorDe(string? nombre)
    {
        if (!string.IsNullOrWhiteSpace(nombre) && PorNombre.TryGetValue(nombre.Trim(), out string? color))
        {
            return color;
        }

        return Neutro;
    }

    /// <summary>
    /// Siguiente color a proponer: el primero que todavia no use ninguna materia, o si ya
    /// estan todos tomados, el que sigue por cantidad de materias (US-027: se puede repetir,
    /// pero primero se ofrecen los libres).
    /// </summary>
    public static string SiguienteLibre(IEnumerable<Materia> existentes)
    {
        var usados = (existentes ?? Array.Empty<Materia>())
            .Where(m => m.TieneColor)
            .Select(m => m.Color)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string color in Colores)
        {
            if (!usados.Contains(color))
            {
                return color;
            }
        }

        return Colores[usados.Count % Colores.Count];
    }

    /// <summary>true si el color pertenece a la paleta (RN-31: no hay colores fuera de ella).</summary>
    public static bool EsDeLaPaleta(string? color) =>
        color is not null && Colores.Contains(color, StringComparer.OrdinalIgnoreCase);
}
