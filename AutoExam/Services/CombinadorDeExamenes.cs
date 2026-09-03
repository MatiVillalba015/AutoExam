using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>Resultado de armar un repaso: las preguntas elegidas y por que salieron esas.</summary>
/// <param name="Preguntas">Preguntas ya mezcladas y listas para rendir.</param>
/// <param name="Disponibles">Cuantas habia en total entre los examenes elegidos.</param>
/// <param name="Pedidas">Cuantas habia pedido el alumno.</param>
public sealed record RepasoArmado(IReadOnlyList<Pregunta> Preguntas, int Disponibles, int Pedidas)
{
    /// <summary>
    /// True si no habia suficientes preguntas y el examen salio mas corto de lo pedido.
    /// US-026 exige avisarlo: si no, el alumno pide 60 y recibe 22 sin entender por que.
    /// </summary>
    public bool SeAjustoLaCantidad => Preguntas.Count < Pedidas;
}

/// <summary>
/// Arma un examen de repaso mezclando preguntas de varios intentos ya rendidos (US-026).
///
/// No habla con Gemini ni con la red: las preguntas ya existen, generadas y guardadas en su
/// momento por US-025. Eso es lo que hace que un repaso sea instantaneo y no gaste una sola
/// peticion de la cuota diaria (RN-27), que es justamente la restriccion que vuelve valioso
/// el repaso: se puede armar tantas veces como haga falta.
/// </summary>
public static class CombinadorDeExamenes
{
    /// <summary>
    /// Elige <paramref name="cantidadPedida"/> preguntas al azar del conjunto formado por
    /// <paramref name="examenes"/>, sin repetir ninguna (RN-27) y sin superar lo disponible.
    /// </summary>
    /// <remarks>
    /// Las preguntas vuelven como copias en blanco: el repaso es un examen nuevo, asi que
    /// arrastrar la respuesta que el alumno habia marcado la primera vez ademas de estar mal
    /// le mostraria de entrada cuales habia acertado.
    /// </remarks>
    public static RepasoArmado Armar(
        IReadOnlyList<ExamenRendido> examenes, int cantidadPedida, Random? azar = null)
    {
        var origen = new List<(ExamenRendido Examen, Pregunta Pregunta)>();

        foreach (var examen in examenes ?? Array.Empty<ExamenRendido>())
        {
            // Un examen sin detalle guardado (RN-26) no puede aportar nada: es de antes de
            // US-025 y solo tiene el resumen numerico. Y un repaso no alimenta otro repaso,
            // que es encadenar combinados de combinados (fuera de alcance).
            if (!examen.PuedeAlimentarRepaso)
            {
                continue;
            }

            foreach (var pregunta in examen.Preguntas)
            {
                origen.Add((examen, pregunta));
            }
        }

        int disponibles = origen.Count;
        int pedidas = Math.Max(0, cantidadPedida);

        if (disponibles == 0 || pedidas == 0)
        {
            return new RepasoArmado(Array.Empty<Pregunta>(), disponibles, pedidas);
        }

        var rnd = azar ?? Random.Shared;

        // Se baraja el conjunto entero y se toman las primeras: tomar al azar de a una y
        // descartar repetidas se vuelve lentisimo cuando se piden casi todas las que hay.
        var barajado = origen.OrderBy(_ => rnd.Next()).Take(Math.Min(pedidas, disponibles));

        var elegidas = new List<Pregunta>();

        foreach (var (examen, pregunta) in barajado)
        {
            var copia = pregunta.Clonar();

            // Las opciones se remezclan: repetir el mismo orden dejaria repasar de memoria
            // ("era la C") en vez de por el contenido.
            copia.MezclarOpciones(rnd);
            copia.ReiniciarParaRevancha();

            // De que examen venia. Es lo que pide US-026 para el detalle, y lo unico que
            // ubica una pregunta cuando el repaso mezcla materias distintas.
            copia.ExamenOrigen = string.IsNullOrWhiteSpace(examen.TituloTexto)
                ? examen.LibroTitulo
                : examen.TituloTexto;

            elegidas.Add(copia);
        }

        return new RepasoArmado(elegidas, disponibles, pedidas);
    }

    /// <summary>Cuantas preguntas hay para repasar entre los examenes elegidos.</summary>
    public static int ContarDisponibles(IEnumerable<ExamenRendido> examenes) =>
        (examenes ?? Array.Empty<ExamenRendido>())
            .Where(e => e.PuedeAlimentarRepaso)
            .Sum(e => e.Preguntas.Count);

    /// <summary>
    /// Titulo del examen de repaso. Con dos o tres los nombra —es lo que el alumno reconoce
    /// en el historial—; con mas, la lista completa no entraria en una fila.
    /// </summary>
    public static string TituloDelRepaso(IReadOnlyList<string> examenes) => examenes.Count switch
    {
        0 => "Repaso combinado",
        <= 3 => "Repaso: " + string.Join(" + ", examenes),
        _ => $"Repaso de {examenes.Count} examenes"
    };
}
