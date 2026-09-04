using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>Un intento en la linea de tiempo de una materia.</summary>
/// <param name="Fecha">Cuando se rindio.</param>
/// <param name="Nota">Nota UBA, 1 a 10.</param>
/// <param name="Porcentaje">Porcentaje de aciertos.</param>
/// <param name="Titulo">Que examen fue.</param>
public sealed record PuntoEvolucion(DateTime Fecha, int Nota, double Porcentaje, string Titulo)
{
    public string Detalle => $"{Titulo} · {Nota} ({Porcentaje:0.#}%) · {Fecha:dd/MM/yyyy}";
}

/// <summary>Un intento ubicado en el grafico: coordenadas relativas 0..1 y el dato que representa.</summary>
public sealed record MarcadorEvolucion(double X, double Y, PuntoEvolucion Dato);

/// <summary>
/// La evolucion de una materia, ya lista para dibujar.
///
/// Trae los puntos en coordenadas 0..1 en vez de pixeles: la vista los escala al tamanio real
/// del grafico. Asi el calculo se puede probar sin levantar WPF, que es lo unico que hace
/// verificable un grafico.
/// </summary>
public sealed class EvolucionMateria
{
    public EvolucionMateria(string materia, IReadOnlyList<PuntoEvolucion> puntos)
    {
        Materia = materia;
        Puntos = puntos;
    }

    public string Materia { get; }

    /// <summary>Del intento mas viejo al mas nuevo: es como se lee una linea de tiempo.</summary>
    public IReadOnlyList<PuntoEvolucion> Puntos { get; }

    /// <summary>
    /// Con un solo intento no hay evolucion que mostrar: una linea necesita dos puntos. El
    /// criterio pide decirlo, no dibujar un grafico vacio.
    /// </summary>
    public bool SePuedeGraficar => Puntos.Count >= EvolucionDeMateria.MinimoDeExamenes;

    public string Aviso => Puntos.Count switch
    {
        0 => "Todavía no rendiste ningún examen de esta materia.",
        1 => "Rendí al menos dos exámenes de esta materia para ver tu evolución. Llevás uno.",
        _ => string.Empty
    };

    public int Mejor => Puntos.Count == 0 ? 0 : Puntos.Max(p => p.Nota);

    public int Peor => Puntos.Count == 0 ? 0 : Puntos.Min(p => p.Nota);

    public double PromedioNota => Puntos.Count == 0 ? 0 : Math.Round(Puntos.Average(p => p.Nota), 1);

    /// <summary>
    /// Diferencia entre el primer intento y el ultimo. Es el dato que responde la pregunta
    /// que motiva la historia ("¿estoy mejorando o estancado?") sin tener que interpretar la
    /// linea.
    /// </summary>
    public int Progreso => Puntos.Count < 2 ? 0 : Puntos[^1].Nota - Puntos[0].Nota;

    public string TextoProgreso => Puntos.Count < 2
        ? string.Empty
        : Progreso switch
        {
            > 0 => $"Subiste {Progreso} punto{(Progreso == 1 ? string.Empty : "s")} desde tu primer examen.",
            < 0 => $"Bajaste {Math.Abs(Progreso)} punto{(Math.Abs(Progreso) == 1 ? string.Empty : "s")} desde tu primer examen.",
            _ => "Estás igual que en tu primer examen."
        };

    /// <summary>
    /// Los puntos en coordenadas relativas: X de 0 (el mas viejo) a 1 (el mas nuevo), Y de 0
    /// (nota 1) a 1 (nota 10).
    ///
    /// El eje Y va siempre de 1 a 10 y no del minimo al maximo de la serie: con un eje que se
    /// autoajusta, pasar de 7 a 8 se ve igual de dramatico que pasar de 2 a 9, y el grafico
    /// termina mintiendo sobre lo unico que tiene que contar.
    /// </summary>
    public IReadOnlyList<(double X, double Y)> Relativos()
    {
        if (Puntos.Count == 0)
        {
            return Array.Empty<(double, double)>();
        }

        if (Puntos.Count == 1)
        {
            return new[] { (0.5, Normalizar(Puntos[0].Nota)) };
        }

        var salida = new List<(double, double)>(Puntos.Count);

        for (int i = 0; i < Puntos.Count; i++)
        {
            salida.Add((i / (double)(Puntos.Count - 1), Normalizar(Puntos[i].Nota)));
        }

        return salida;
    }

    /// <summary>
    /// Los mismos puntos con su dato al lado, para dibujar un marcador por intento y poder
    /// mostrar en el tooltip de que examen fue. Se expone aparte de <see cref="Relativos"/>
    /// porque la polilinea solo necesita coordenadas y los marcadores necesitan las dos cosas.
    /// </summary>
    public IReadOnlyList<MarcadorEvolucion> Marcadores
    {
        get
        {
            var relativos = Relativos();
            var salida = new List<MarcadorEvolucion>(relativos.Count);

            for (int i = 0; i < relativos.Count; i++)
            {
                salida.Add(new MarcadorEvolucion(relativos[i].X, relativos[i].Y, Puntos[i]));
            }

            return salida;
        }
    }

    /// <summary>Altura relativa de la linea de aprobacion (nota 4), para dibujarla de referencia.</summary>
    public double AlturaAprobacion => Normalizar(EvolucionDeMateria.NotaAprobacion);

    private static double Normalizar(int nota) =>
        Math.Clamp((nota - EvolucionDeMateria.NotaMinima) /
                   (double)(EvolucionDeMateria.NotaMaxima - EvolucionDeMateria.NotaMinima), 0, 1);
}

/// <summary>
/// Arma la evolucion de notas por materia (US-033).
///
/// RN-42: sale entero de lo que <see cref="ExamenRendido"/> ya persiste —fecha, nota,
/// porcentaje y materia—, sin guardar nada nuevo. Por eso funciona tambien sobre examenes
/// rendidos hace meses, incluso los de antes de US-025 que no tienen detalle de preguntas: el
/// grafico no los necesita.
/// </summary>
public static class EvolucionDeMateria
{
    /// <summary>Una linea necesita dos puntos. Con uno solo se avisa en vez de dibujar.</summary>
    public const int MinimoDeExamenes = 2;

    public const int NotaMinima = 1;
    public const int NotaMaxima = 10;

    /// <summary>Nota de aprobacion (RN-1), para dibujar la linea de referencia.</summary>
    public const int NotaAprobacion = 4;

    /// <summary>Materias con al menos un examen rendido, ordenadas por cantidad.</summary>
    public static IReadOnlyList<string> MateriasConExamenes(IEnumerable<ExamenRendido> historial) =>
        (historial ?? Array.Empty<ExamenRendido>())
            .Where(e => !string.IsNullOrWhiteSpace(e.Materia))
            .GroupBy(e => e.Materia, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => g.First().Materia)
            .ToList();

    public static EvolucionMateria De(IEnumerable<ExamenRendido> historial, string materia)
    {
        var puntos = (historial ?? Array.Empty<ExamenRendido>())
            .Where(e => string.Equals(e.Materia, materia, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Fecha)
            .Select(e => new PuntoEvolucion(e.Fecha, e.NotaUBA, e.PorcentajeAciertos, e.TituloTexto))
            .ToList();

        return new EvolucionMateria(materia, puntos);
    }
}
