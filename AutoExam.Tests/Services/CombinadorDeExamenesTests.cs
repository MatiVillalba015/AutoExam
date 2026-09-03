using AutoExam.Models;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-026 / RN-27 — un examen nuevo mezclando preguntas de intentos ya rendidos.
///
/// Lo que hace valioso al repaso es que no gasta nada: las preguntas ya existen, generadas y
/// guardadas en su momento por US-025, asi que armarlo es instantaneo y no consume una sola
/// peticion de la cuota diaria. Por eso el armado es una funcion pura sobre lo que hay en el
/// historial, sin red ni servicios de por medio, y por eso se puede probar entera.
/// </summary>
public class CombinadorDeExamenesTests
{
    private static ExamenRendido Examen(string titulo, int preguntas, bool esRepaso = false, string materia = "Fisiologia")
    {
        var examen = new ExamenRendido
        {
            LibroTitulo = titulo,
            Materia = materia,
            EsRepaso = esRepaso
        };

        for (int i = 0; i < preguntas; i++)
        {
            examen.Preguntas.Add(new Pregunta
            {
                TextoPregunta = $"{titulo} — pregunta {i + 1}",
                Opciones = new List<string> { "a", "b", "c", "d" },
                IndiceRespuestaCorrecta = i % 4,
                IndiceRespuestaUsuario = 0,
                Estado = EstadoPreguntaEnum.Respondida,
                Resultado = ResultadoPreguntaEnum.Incorrecta
            });
        }

        return examen;
    }

    /// <summary>Un examen de antes de US-025: tiene resumen pero no preguntas (RN-26).</summary>
    private static ExamenRendido ExamenViejo(string titulo) => new()
    {
        LibroTitulo = titulo,
        TotalPreguntas = 10,
        Correctas = 6
    };

    // ------------------------------------------------------------------
    // AC — mezcla al azar, sin repetir, con la cantidad pedida
    // ------------------------------------------------------------------

    [Fact]
    public void ElRepaso_SaleConLaCantidadPedida()
    {
        var armado = CombinadorDeExamenes.Armar(
            new[] { Examen("A", 20), Examen("B", 20) }, cantidadPedida: 10, azar: new Random(1));

        Assert.Equal(10, armado.Preguntas.Count);
        Assert.False(armado.SeAjustoLaCantidad);
    }

    [Fact]
    public void NingunaPregunta_SeRepiteDentroDelRepaso_RN27()
    {
        // Tomar al azar de a una sin controlar repetidos daria un examen con la misma
        // pregunta dos veces, que ademas de inutil se ve como un bug evidente.
        for (int semilla = 0; semilla < 50; semilla++)
        {
            var armado = CombinadorDeExamenes.Armar(
                new[] { Examen("A", 5), Examen("B", 5) }, cantidadPedida: 10, azar: new Random(semilla));

            var textos = armado.Preguntas.Select(p => p.TextoPregunta).ToList();

            Assert.Equal(textos.Count, textos.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void LasPreguntas_SalenDeTodosLosExamenesElegidos()
    {
        // Pidiendo todas las disponibles tienen que estar las de los dos examenes: si el
        // barajado tomara solo del primero, el repaso no seria de "varios examenes".
        var armado = CombinadorDeExamenes.Armar(
            new[] { Examen("Guyton", 4), Examen("Lehninger", 4) }, cantidadPedida: 8, azar: new Random(7));

        Assert.Contains(armado.Preguntas, p => p.ExamenOrigen.Contains("Guyton", StringComparison.Ordinal));
        Assert.Contains(armado.Preguntas, p => p.ExamenOrigen.Contains("Lehninger", StringComparison.Ordinal));
    }

    [Fact]
    public void ElOrden_CambiaEntreRepasos()
    {
        // Si saliera siempre igual, "al azar" seria una palabra sin efecto y repasar dos
        // veces daria exactamente el mismo examen.
        var examenes = new[] { Examen("A", 10), Examen("B", 10) };

        var uno = CombinadorDeExamenes.Armar(examenes, 5, new Random(1)).Preguntas.Select(p => p.TextoPregunta);
        var otro = CombinadorDeExamenes.Armar(examenes, 5, new Random(2)).Preguntas.Select(p => p.TextoPregunta);

        Assert.NotEqual(uno, otro);
    }

    // ------------------------------------------------------------------
    // AC — si se piden mas de las que hay, sale con todas y se avisa
    // ------------------------------------------------------------------

    [Fact]
    public void PidiendoMasDeLasQueHay_SaleConTodasYSeAvisa()
    {
        var armado = CombinadorDeExamenes.Armar(
            new[] { Examen("A", 6), Examen("B", 4) }, cantidadPedida: 60, azar: new Random(3));

        Assert.Equal(10, armado.Preguntas.Count);
        Assert.Equal(10, armado.Disponibles);
        Assert.Equal(60, armado.Pedidas);
        Assert.True(armado.SeAjustoLaCantidad,
            "Pediste 60 y recibiste 10: sin este aviso parece que la app fallo.");
    }

    [Fact]
    public void PidiendoExactamenteLasQueHay_NoSeAvisaDeAjuste()
    {
        var armado = CombinadorDeExamenes.Armar(new[] { Examen("A", 8) }, cantidadPedida: 8, azar: new Random(4));

        Assert.False(armado.SeAjustoLaCantidad);
    }

    // ------------------------------------------------------------------
    // RN-26 / fuera de alcance — de que examenes se puede sacar preguntas
    // ------------------------------------------------------------------

    [Fact]
    public void UnExamenSinDetalleGuardado_NoAportaPreguntas_RN26()
    {
        // Es de antes de US-025: solo tiene el resumen numerico y no hay de donde sacarlas.
        var armado = CombinadorDeExamenes.Armar(
            new[] { Examen("Nuevo", 5), ExamenViejo("Viejo") }, cantidadPedida: 20, azar: new Random(5));

        Assert.Equal(5, armado.Disponibles);
        Assert.All(armado.Preguntas, p => Assert.Contains("Nuevo", p.ExamenOrigen, StringComparison.Ordinal));
    }

    [Fact]
    public void UnRepaso_NoAlimentaOtroRepaso()
    {
        // Encadenar combinados de combinados esta fuera de alcance: un repaso se arma solo a
        // partir de examenes "originales" rendidos.
        var repasoViejo = Examen("Repaso anterior", 10, esRepaso: true);

        Assert.False(repasoViejo.PuedeAlimentarRepaso);
        Assert.Equal(0, CombinadorDeExamenes.ContarDisponibles(new[] { repasoViejo }));
    }

    [Fact]
    public void SinNadaDeDondeSacar_DevuelveVacioSinRomper()
    {
        var armado = CombinadorDeExamenes.Armar(new[] { ExamenViejo("Viejo") }, cantidadPedida: 10);

        Assert.Empty(armado.Preguntas);
        Assert.Equal(0, armado.Disponibles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PidiendoCeroOMenos_NoRompe(int cantidad)
    {
        var armado = CombinadorDeExamenes.Armar(new[] { Examen("A", 5) }, cantidad);

        Assert.Empty(armado.Preguntas);
    }

    // ------------------------------------------------------------------
    // Las preguntas del repaso arrancan en blanco
    // ------------------------------------------------------------------

    [Fact]
    public void LasPreguntasDelRepaso_NoTraenLaRespuestaDeLaPrimeraVez()
    {
        // Arrastrarla ademas de estar mal le mostraria de entrada cuales habia acertado.
        var armado = CombinadorDeExamenes.Armar(new[] { Examen("A", 5) }, 5, new Random(6));

        Assert.All(armado.Preguntas, p =>
        {
            Assert.Null(p.IndiceRespuestaUsuario);
            Assert.Equal(EstadoPreguntaEnum.SinResponder, p.Estado);
            Assert.Equal(ResultadoPreguntaEnum.Pendiente, p.Resultado);
        });
    }

    [Fact]
    public void ElRepaso_NoTocaLasPreguntasDelExamenOriginal()
    {
        // Se trabaja sobre copias: si no, armar un repaso reescribiria el detalle del
        // examen viejo que le presto las preguntas.
        var original = Examen("A", 5);
        var antes = original.Preguntas.Select(p => p.IndiceRespuestaCorrecta).ToList();

        CombinadorDeExamenes.Armar(new[] { original }, 5, new Random(8));

        Assert.Equal(antes, original.Preguntas.Select(p => p.IndiceRespuestaCorrecta));
        Assert.All(original.Preguntas, p => Assert.Equal(0, p.IndiceRespuestaUsuario));
    }

    // ------------------------------------------------------------------
    // AC — cada pregunta dice de que examen venia
    // ------------------------------------------------------------------

    [Fact]
    public void CadaPregunta_RecuerdaDeQueExamenVenia()
    {
        var armado = CombinadorDeExamenes.Armar(
            new[] { Examen("Guyton", 3), Examen("Lehninger", 3) }, 6, new Random(9));

        Assert.All(armado.Preguntas, p => Assert.False(string.IsNullOrWhiteSpace(p.ExamenOrigen)));
    }

    [Fact]
    public void ElExamenDeOrigen_SeVeEnLaReferenciaDeLaCorreccion()
    {
        // Es lo unico que ubica una pregunta cuando el repaso mezcla materias distintas.
        var pregunta = new Pregunta { PaginaOrigen = 12, ExamenOrigen = "Guyton — cap. 3" };

        Assert.Contains("Guyton", pregunta.ReferenciaFuente, StringComparison.Ordinal);
        Assert.Contains("12", pregunta.ReferenciaFuente, StringComparison.Ordinal);
    }

    [Fact]
    public void SinExamenDeOrigen_LaReferenciaNoCambia()
    {
        // Guarda de no-regresion: un examen normal no menciona ningun examen de origen.
        var pregunta = new Pregunta { PaginaOrigen = 12 };

        Assert.Equal("Pagina 12 del PDF", pregunta.ReferenciaFuente);
    }

    // ------------------------------------------------------------------
    // AC — se pueden combinar examenes de materias distintas
    // ------------------------------------------------------------------

    [Fact]
    public void SePuedenCombinarExamenesDeMateriasDistintas()
    {
        // A diferencia de US-024, aca no se genera desde material con IA: son preguntas que
        // el alumno ya rindio, asi que la restriccion de una sola materia no aplica.
        var armado = CombinadorDeExamenes.Armar(
            new[] { Examen("Guyton", 4, materia: "Fisiologia"), Examen("Lehninger", 4, materia: "Bioquimica") },
            8, new Random(10));

        Assert.Equal(8, armado.Preguntas.Count);
    }

    // ------------------------------------------------------------------
    // Titulo del repaso
    // ------------------------------------------------------------------

    [Fact]
    public void ConPocosExamenes_ElTituloLosNombra()
    {
        string titulo = CombinadorDeExamenes.TituloDelRepaso(new[] { "Guyton", "Lehninger" });

        Assert.Contains("Guyton", titulo, StringComparison.Ordinal);
        Assert.Contains("Lehninger", titulo, StringComparison.Ordinal);
    }

    [Fact]
    public void ConMuchosExamenes_ElTituloSeResume()
    {
        // La lista completa no entraria en una fila del historial.
        string titulo = CombinadorDeExamenes.TituloDelRepaso(new[] { "A", "B", "C", "D", "E" });

        Assert.Contains("5", titulo, StringComparison.Ordinal);
        Assert.DoesNotContain("+", titulo, StringComparison.Ordinal);
    }
}
