using AutoExam.Models;

namespace AutoExam.Services;

public class ResultadoExamen
{
    public int Total { get; init; }
    public int Correctas { get; init; }
    public int Incorrectas { get; init; }
    public int Salteadas { get; init; }

    public double Porcentaje { get; init; }
    public int Nota { get; init; }
    public string Condicion { get; init; } = string.Empty;
    public bool Aprobado { get; init; }

    public int Pendientes => Incorrectas + Salteadas;

    public string Resumen =>
        $"{Correctas}/{Total} correctas ({Porcentaje:0.#}%) · {Incorrectas} incorrectas · {Salteadas} salteadas";
}

/// <summary>
/// Correccion local y calificacion con la escala de la UBA (1 a 10, se aprueba con 4 = 60%).
/// Las preguntas salteadas cuentan como no respondidas, es decir, restan igual que un error.
/// </summary>
public static class EvaluadorUBA
{
    /// <summary>Umbral minimo de aprobacion en la UBA.</summary>
    public const double PorcentajeAprobacion = 60d;

    private static readonly (double minimo, int nota)[] Escala =
    {
        (95, 10),
        (88, 9),
        (81, 8),
        (74, 7),
        (68, 6),
        (64, 5),
        (60, 4),   // Aprobado
        (40, 3),   // Aplazos
        (20, 2),
        (0, 1)
    };

    /// <summary>Corrige el intento y fija el <see cref="Pregunta.Resultado"/> de cada pregunta.</summary>
    public static ResultadoExamen Evaluar(IEnumerable<Pregunta> preguntas)
    {
        var lista = preguntas.ToList();

        int correctas = 0, incorrectas = 0, salteadas = 0;

        foreach (var p in lista)
        {
            if (p.Estado == EstadoPreguntaEnum.Salteada || p.IndiceRespuestaUsuario is null)
            {
                p.Estado = EstadoPreguntaEnum.Salteada;
                p.Resultado = ResultadoPreguntaEnum.Salteada;
                salteadas++;
            }
            else if (p.IndiceRespuestaUsuario == p.IndiceRespuestaCorrecta)
            {
                p.Resultado = ResultadoPreguntaEnum.Correcta;
                correctas++;
            }
            else
            {
                p.Resultado = ResultadoPreguntaEnum.Incorrecta;
                incorrectas++;
            }
        }

        int total = lista.Count;
        double porcentaje = total == 0 ? 0 : correctas * 100d / total;
        int nota = CalcularNota(porcentaje);

        return new ResultadoExamen
        {
            Total = total,
            Correctas = correctas,
            Incorrectas = incorrectas,
            Salteadas = salteadas,
            Porcentaje = porcentaje,
            Nota = nota,
            Aprobado = porcentaje >= PorcentajeAprobacion,
            Condicion = DescribirCondicion(nota, porcentaje)
        };
    }

    public static int CalcularNota(double porcentaje)
    {
        foreach (var (minimo, nota) in Escala)
        {
            if (porcentaje >= minimo)
            {
                return nota;
            }
        }

        return 1;
    }

    public static string DescribirCondicion(int nota, double porcentaje)
    {
        if (porcentaje < PorcentajeAprobacion)
        {
            return $"Aplazo ({nota})";
        }

        return nota switch
        {
            4 => "Aprobado (4)",
            5 => "Aprobado (5)",
            6 => "Bueno (6)",
            7 => "Bueno (7)",
            8 => "Muy bueno (8)",
            9 => "Distinguido (9)",
            10 => "Sobresaliente (10)",
            _ => $"Aprobado ({nota})"
        };
    }

    /// <summary>Tabla que se muestra en la pestania Historial.</summary>
    public static IReadOnlyList<string> DescribirEscala() => new[]
    {
        "95 % - 100 %  →  10  Sobresaliente",
        "88 % -  94 %  →   9  Distinguido",
        "81 % -  87 %  →   8  Muy bueno",
        "74 % -  80 %  →   7  Bueno",
        "68 % -  73 %  →   6  Bueno",
        "64 % -  67 %  →   5  Aprobado",
        "60 % -  63 %  →   4  Aprobado (minimo)",
        "40 % -  59 %  →   3  Aplazo",
        "20 % -  39 %  →   2  Aplazo",
        " 0 % -  19 %  →   1  Aplazo"
    };
}
