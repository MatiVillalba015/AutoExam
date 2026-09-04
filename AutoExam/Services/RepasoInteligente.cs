using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>Un tema sobre el que se puede armar un repaso inteligente, con cuanto hay para repasar.</summary>
/// <param name="Clave">Identificador estable: nombre de materia, o Id del documento.</param>
/// <param name="Nombre">Como se muestra.</param>
/// <param name="EsMateria">true si agrupa una materia entera; false si es un documento suelto.</param>
/// <param name="Falladas">Cuantas preguntas distintas hay para repasar.</param>
public sealed record FocoDeRepaso(string Clave, string Nombre, bool EsMateria, int Falladas)
{
    public string Etiqueta => Falladas == 1
        ? $"{Nombre} · 1 pregunta"
        : $"{Nombre} · {Falladas} preguntas";
}

/// <summary>
/// Arma un examen con las preguntas que el alumno viene fallando (US-032).
///
/// Se apoya enteramente en lo que el historial ya guarda (RN-41/RN-42): no persiste nada
/// nuevo. Dos propiedades que ya existian lo hacen posible:
///
/// · <see cref="Pregunta.Id"/> sobrevive a <see cref="Pregunta.Clonar"/>, asi que la misma
///   pregunta conserva su identidad cuando reaparece en una revancha o en un repaso. Es lo
///   que permite decir "esta pregunta" a traves de varios intentos sin inventar una clave por
///   texto, que se rompe con el primer retoque de redaccion.
/// · <see cref="Pregunta.EsPendiente"/> ya significa "incorrecta o salteada", que es
///   exactamente la definicion de fallada del criterio.
///
/// La regla es "cuenta el ULTIMO intento": una pregunta esta fallada si la vez mas reciente
/// que la respondio le salio mal o la salteo. Eso es lo que hace que responderla bien en un
/// repaso la saque del pozo —criterio explicito de US-032— y que volver a errarla la devuelva,
/// sin guardar ningun contador aparte.
/// </summary>
public static class RepasoInteligente
{
    /// <summary>
    /// Ultimo resultado de cada pregunta, mirando todo el historial del mas nuevo al mas viejo.
    ///
    /// Participa cualquier examen con detalle guardado, incluidos los repasos — a diferencia
    /// de <see cref="CombinadorDeExamenes"/>, que los excluye. La diferencia es deliberada:
    /// alla un repaso no puede ser FUENTE de otro repaso (encadenar combinados de combinados
    /// esta fuera de alcance), pero aca lo que se lee de un repaso no son sus preguntas sino
    /// como le fue al alumno en ellas, y ese dato vale igual que el de cualquier otro intento.
    /// Ignorarlo romperia el criterio de que acertar en un repaso saque a la pregunta de la
    /// lista de falladas.
    /// </summary>
    private static Dictionary<string, (Pregunta Pregunta, ExamenRendido Examen)> UltimoIntentoPorPregunta(
        IEnumerable<ExamenRendido> historial)
    {
        var ultimo = new Dictionary<string, (Pregunta, ExamenRendido)>(StringComparer.Ordinal);

        foreach (var examen in (historial ?? Array.Empty<ExamenRendido>())
                     .Where(e => e.TieneDetalle)
                     .OrderByDescending(e => e.Fecha))
        {
            foreach (var pregunta in examen.Preguntas)
            {
                if (string.IsNullOrWhiteSpace(pregunta.Id))
                {
                    continue;
                }

                // El primero que se ve es el mas reciente: el historial viene ordenado de
                // nuevo a viejo, asi que no se pisa.
                if (!ultimo.ContainsKey(pregunta.Id))
                {
                    ultimo[pregunta.Id] = (pregunta, examen);
                }
            }
        }

        return ultimo;
    }

    /// <summary>
    /// Preguntas que siguen falladas, sin repetir (RN-40). Cada una aparece una sola vez
    /// aunque se haya errado en cinco examenes distintos, porque lo que se guarda es su
    /// ultimo intento y no cada aparicion.
    /// </summary>
    public static IReadOnlyList<Pregunta> Falladas(
        IEnumerable<ExamenRendido> historial, string? foco = null, bool esMateria = true)
    {
        var elegidas = new List<Pregunta>();

        foreach (var (pregunta, examen) in UltimoIntentoPorPregunta(historial).Values)
        {
            if (!pregunta.EsPendiente)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(foco) && !Coincide(examen, foco, esMateria))
            {
                continue;
            }

            elegidas.Add(pregunta);
        }

        return elegidas;
    }

    private static bool Coincide(ExamenRendido examen, string foco, bool esMateria) => esMateria
        ? string.Equals(examen.Materia, foco, StringComparison.OrdinalIgnoreCase)
        : string.Equals(examen.LibroId, foco, StringComparison.Ordinal);

    /// <summary>
    /// Sobre que se puede armar un repaso hoy: materias y documentos con al menos una pregunta
    /// fallada. Se ofrece lo que existe y no una lista fija de materias, para que nunca se
    /// pueda elegir un foco que da un examen de cero preguntas.
    /// </summary>
    public static IReadOnlyList<FocoDeRepaso> Focos(IEnumerable<ExamenRendido> historial)
    {
        var lista = historial as IReadOnlyList<ExamenRendido> ?? historial?.ToList() ?? new List<ExamenRendido>();

        var porMateria = new Dictionary<string, (string Nombre, int Cuenta)>(StringComparer.OrdinalIgnoreCase);
        var porDocumento = new Dictionary<string, (string Nombre, int Cuenta)>(StringComparer.Ordinal);

        foreach (var (pregunta, examen) in UltimoIntentoPorPregunta(lista).Values)
        {
            if (!pregunta.EsPendiente)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(examen.Materia))
            {
                porMateria.TryGetValue(examen.Materia, out var m);
                porMateria[examen.Materia] = (examen.Materia, m.Cuenta + 1);
            }

            if (!string.IsNullOrWhiteSpace(examen.LibroId))
            {
                porDocumento.TryGetValue(examen.LibroId, out var d);
                porDocumento[examen.LibroId] = (examen.LibroTitulo, d.Cuenta + 1);
            }
        }

        var focos = new List<FocoDeRepaso>();

        foreach (var (clave, valor) in porMateria)
        {
            focos.Add(new FocoDeRepaso(clave, valor.Nombre, EsMateria: true, valor.Cuenta));
        }

        foreach (var (clave, valor) in porDocumento)
        {
            focos.Add(new FocoDeRepaso(clave, valor.Nombre, EsMateria: false, valor.Cuenta));
        }

        // Primero lo que mas se fallo: es el orden en el que uno quiere ver esta lista.
        return focos
            .OrderByDescending(f => f.Falladas)
            .ThenBy(f => f.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Arma el repaso. Devuelve el mismo tipo que <see cref="CombinadorDeExamenes"/> para que
    /// el aviso de "se ajusto la cantidad" sea uno solo en toda la app (US-032 lo pide
    /// explicitamente "igual que en US-026").
    /// </summary>
    public static RepasoArmado Armar(
        IEnumerable<ExamenRendido> historial,
        int cantidadPedida,
        string? foco = null,
        bool esMateria = true,
        Random? azar = null)
    {
        var falladas = Falladas(historial, foco, esMateria);

        int disponibles = falladas.Count;
        int pedidas = Math.Max(0, cantidadPedida);

        if (disponibles == 0 || pedidas == 0)
        {
            return new RepasoArmado(Array.Empty<Pregunta>(), disponibles, pedidas);
        }

        var rnd = azar ?? Random.Shared;

        var elegidas = new List<Pregunta>();

        foreach (var pregunta in falladas.OrderBy(_ => rnd.Next()).Take(Math.Min(pedidas, disponibles)))
        {
            var copia = pregunta.Clonar();

            // Mismo trato que en un repaso combinado: opciones remezcladas para que no se
            // repase de memoria ("era la C"), y sin la respuesta marcada la vez anterior.
            copia.MezclarOpciones(rnd);
            copia.ReiniciarParaRevancha();

            elegidas.Add(copia);
        }

        return new RepasoArmado(elegidas, disponibles, pedidas);
    }

    /// <summary>Titulo del examen en el historial.</summary>
    public static string Titulo(string? nombreDelFoco) =>
        string.IsNullOrWhiteSpace(nombreDelFoco)
            ? "Repaso de lo que falle"
            : $"Repaso de lo que falle · {nombreDelFoco}";
}
