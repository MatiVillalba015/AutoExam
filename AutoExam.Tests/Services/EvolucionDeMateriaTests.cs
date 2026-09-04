using AutoExam.Models;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-033 — evolución de notas por materia.
///
/// El cálculo vive separado del dibujo a propósito: así se puede verificar sin levantar WPF,
/// que es lo único que hace verificable un gráfico. Lo que estos tests fijan es que la línea
/// diga la verdad — el orden temporal, la escala del eje, y que con un solo intento se avise
/// en vez de dibujar.
/// </summary>
public class EvolucionDeMateriaTests
{
    private static ExamenRendido Rendido(string materia, DateTime fecha, int nota) => new()
    {
        Materia = materia,
        Fecha = fecha,
        NotaUBA = nota,
        PorcentajeAciertos = nota * 10,
        LibroTitulo = $"Tp {nota}",
        TotalPreguntas = 10,
        Correctas = nota,
    };

    [Fact]
    public void LosPuntos_VanDelIntentoMasViejoAlMasNuevo()
    {
        // Una línea de tiempo al revés muestra una mejora como un empeoramiento.
        var historial = new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 20), 9),
            Rendido("Fisio", new DateTime(2026, 5, 1), 4),
            Rendido("Fisio", new DateTime(2026, 5, 10), 6),
        };

        var evolucion = EvolucionDeMateria.De(historial, "Fisio");

        Assert.Equal(new[] { 4, 6, 9 }, evolucion.Puntos.Select(p => p.Nota));
    }

    [Fact]
    public void SoloEntranLosExamenesDeEsaMateria()
    {
        var historial = new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 4),
            Rendido("Bioqui", new DateTime(2026, 5, 2), 8),
        };

        Assert.Single(EvolucionDeMateria.De(historial, "Fisio").Puntos);
    }

    [Fact]
    public void ConUnSoloExamen_SeAvisaEnVezDeGraficar()
    {
        // Criterio explícito: "rendí al menos dos exámenes de esta materia para ver tu
        // evolución", en vez de un gráfico vacío o roto.
        var evolucion = EvolucionDeMateria.De(
            new[] { Rendido("Fisio", new DateTime(2026, 5, 1), 7) }, "Fisio");

        Assert.False(evolucion.SePuedeGraficar);
        Assert.Contains("dos exámenes", evolucion.Aviso, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SinNingunExamenDeEsaMateria_TambienSeAvisa()
    {
        var evolucion = EvolucionDeMateria.De(Array.Empty<ExamenRendido>(), "Fisio");

        Assert.False(evolucion.SePuedeGraficar);
        Assert.False(string.IsNullOrWhiteSpace(evolucion.Aviso));
    }

    [Fact]
    public void ConDosOMas_SePuedeGraficarYNoHayAviso()
    {
        var evolucion = EvolucionDeMateria.De(new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 4),
            Rendido("Fisio", new DateTime(2026, 5, 2), 8),
        }, "Fisio");

        Assert.True(evolucion.SePuedeGraficar);
        Assert.Equal(string.Empty, evolucion.Aviso);
    }

    [Fact]
    public void ElEjeVerticalVaSiempreDe1A10_NoSeAutoajusta()
    {
        // Con un eje que se acomoda al rango de la serie, pasar de 7 a 8 se ve igual de
        // dramático que pasar de 2 a 9: el gráfico terminaría mintiendo sobre lo único que
        // tiene que contar.
        var apretada = EvolucionDeMateria.De(new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 7),
            Rendido("Fisio", new DateTime(2026, 5, 2), 8),
        }, "Fisio");

        var amplia = EvolucionDeMateria.De(new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 2),
            Rendido("Fisio", new DateTime(2026, 5, 2), 9),
        }, "Fisio");

        double saltoApretado = Math.Abs(apretada.Relativos()[1].Y - apretada.Relativos()[0].Y);
        double saltoAmplio = Math.Abs(amplia.Relativos()[1].Y - amplia.Relativos()[0].Y);

        Assert.True(saltoAmplio > saltoApretado * 3,
            "El eje se está autoajustando: una mejora chica se dibuja igual que una enorme.");
    }

    [Fact]
    public void LasCoordenadas_QuedanDentroDelCuadro()
    {
        var evolucion = EvolucionDeMateria.De(new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 1),
            Rendido("Fisio", new DateTime(2026, 5, 2), 10),
            Rendido("Fisio", new DateTime(2026, 5, 3), 5),
        }, "Fisio");

        Assert.All(evolucion.Relativos(), p =>
        {
            Assert.InRange(p.X, 0, 1);
            Assert.InRange(p.Y, 0, 1);
        });

        // El primero pegado a la izquierda y el último a la derecha: la línea usa todo el ancho.
        Assert.Equal(0, evolucion.Relativos()[0].X, 3);
        Assert.Equal(1, evolucion.Relativos()[^1].X, 3);
    }

    [Fact]
    public void HayUnMarcadorPorIntento_ConSuDatoAlLado()
    {
        var evolucion = EvolucionDeMateria.De(new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 4),
            Rendido("Fisio", new DateTime(2026, 5, 2), 8),
        }, "Fisio");

        Assert.Equal(2, evolucion.Marcadores.Count);
        Assert.Equal(4, evolucion.Marcadores[0].Dato.Nota);
        Assert.Equal(8, evolucion.Marcadores[1].Dato.Nota);
    }

    [Fact]
    public void ElProgreso_ComparaElPrimerIntentoConElUltimo()
    {
        // Es el dato que responde la pregunta que motiva la historia: ¿estoy mejorando o
        // estancado? Leerlo de la línea es más trabajo que leerlo de una frase.
        var subiendo = EvolucionDeMateria.De(new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 4),
            Rendido("Fisio", new DateTime(2026, 5, 2), 7),
        }, "Fisio");

        Assert.Equal(3, subiendo.Progreso);
        Assert.Contains("Subiste", subiendo.TextoProgreso, StringComparison.Ordinal);

        var bajando = EvolucionDeMateria.De(new[]
        {
            Rendido("Fisio", new DateTime(2026, 5, 1), 9),
            Rendido("Fisio", new DateTime(2026, 5, 2), 6),
        }, "Fisio");

        Assert.Equal(-3, bajando.Progreso);
        Assert.Contains("Bajaste", bajando.TextoProgreso, StringComparison.Ordinal);
    }

    [Fact]
    public void LasMateriasSeListanPorCantidadDeExamenes()
    {
        var historial = new[]
        {
            Rendido("Bioqui", new DateTime(2026, 5, 1), 4),
            Rendido("Fisio", new DateTime(2026, 5, 2), 5),
            Rendido("Fisio", new DateTime(2026, 5, 3), 6),
        };

        Assert.Equal(new[] { "Fisio", "Bioqui" }, EvolucionDeMateria.MateriasConExamenes(historial));
    }

    [Fact]
    public void UnExamenSinMateria_NoInventaUnaMateriaVacia()
    {
        var historial = new[] { Rendido(string.Empty, new DateTime(2026, 5, 1), 4) };

        Assert.Empty(EvolucionDeMateria.MateriasConExamenes(historial));
    }

    [Fact]
    public void ElGraficoFuncionaConExamenesSinDetalle_RN42()
    {
        // RN-42: se arma con lo que ExamenRendido ya persiste. Un examen de antes de US-025 no
        // tiene preguntas guardadas y aun así tiene fecha, nota y materia: entra al gráfico.
        var viejo = new ExamenRendido
        {
            Materia = "Fisio",
            Fecha = new DateTime(2025, 1, 1),
            NotaUBA = 6,
            TotalPreguntas = 10,
        };

        Assert.False(viejo.TieneDetalle);

        var evolucion = EvolucionDeMateria.De(
            new[] { viejo, Rendido("Fisio", new DateTime(2026, 5, 1), 8) }, "Fisio");

        Assert.True(evolucion.SePuedeGraficar);
        Assert.Equal(2, evolucion.Puntos.Count);
    }
}
