using AutoExam.Models;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-032 — repaso de las preguntas que el alumno viene fallando.
///
/// La regla central es "cuenta el ÚLTIMO intento": una pregunta está fallada si la vez más
/// reciente que la respondió le salió mal o la salteó. De ahí salen dos criterios de la
/// historia sin guardar ningún contador: acertarla en un repaso la saca de la lista, y volver
/// a errarla la devuelve.
///
/// La identidad entre intentos es <see cref="Pregunta.Id"/>, que <see cref="Pregunta.Clonar"/>
/// preserva. Sin eso habría que comparar por texto, que se rompe con el primer retoque de
/// redacción.
/// </summary>
public class RepasoInteligenteTests
{
    private static Pregunta Preg(string id, ResultadoPreguntaEnum resultado, string texto = "¿Cual es?")
    {
        var p = new Pregunta
        {
            Id = id,
            TextoPregunta = texto,
            Opciones = new List<string> { "a", "b", "c", "d" },
            IndiceRespuestaCorrecta = 0,
        };

        // Así queda una pregunta guardada en el historial: el evaluador escribe Estado y
        // Resultado al corregir, y ClonarParaHistorial los copia. Marcar sólo la respuesta
        // dejaría Resultado en su valor por defecto y ninguna pregunta contaría como fallada.
        switch (resultado)
        {
            case ResultadoPreguntaEnum.Correcta:
                p.IndiceRespuestaUsuario = 0;
                p.Estado = EstadoPreguntaEnum.Respondida;
                break;
            case ResultadoPreguntaEnum.Incorrecta:
                p.IndiceRespuestaUsuario = 1;
                p.Estado = EstadoPreguntaEnum.Respondida;
                break;
            default:
                p.Estado = EstadoPreguntaEnum.Salteada;
                break;
        }

        p.Resultado = resultado;

        return p;
    }

    private static ExamenRendido Examen(
        DateTime fecha, string materia, string libroId, params Pregunta[] preguntas) =>
        new()
        {
            Fecha = fecha,
            Materia = materia,
            LibroId = libroId,
            LibroTitulo = libroId,
            TotalPreguntas = preguntas.Length,
            Preguntas = preguntas.ToList(),
        };

    // ------------------------------------------------------------------
    // Que cuenta como fallada
    // ------------------------------------------------------------------

    [Fact]
    public void UnaPreguntaErrada_EntraAlRepaso()
    {
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1",
                Preg("p1", ResultadoPreguntaEnum.Incorrecta),
                Preg("p2", ResultadoPreguntaEnum.Correcta)),
        };

        var falladas = RepasoInteligente.Falladas(historial);

        Assert.Equal(new[] { "p1" }, falladas.Select(p => p.Id));
    }

    [Fact]
    public void UnaPreguntaSalteada_TambienCuentaComoFallada()
    {
        // El criterio dice "las que marqué incorrectas (o salteadas)": saltear es no saberla.
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Salteada)),
        };

        Assert.Single(RepasoInteligente.Falladas(historial));
    }

    [Fact]
    public void UnaPreguntaFalladaEnVariosExamenes_EntraUnaSolaVez_RN40()
    {
        // RN-40: "nunca repite la misma pregunta dos veces dentro de un mismo repaso".
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
            Examen(new DateTime(2026, 5, 2), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
            Examen(new DateTime(2026, 5, 3), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Salteada)),
        };

        Assert.Single(RepasoInteligente.Falladas(historial));

        var armado = RepasoInteligente.Armar(historial, 10, azar: new Random(1));

        Assert.Single(armado.Preguntas);
        Assert.Single(armado.Preguntas.Select(p => p.Id).Distinct());
    }

    [Fact]
    public void SiDespuesLaAcierta_DejaDeEstarFallada()
    {
        // Criterio explícito de US-032: "si esa misma pregunta vuelve a aparecer en un futuro
        // repaso, ya no cuenta como fallada".
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
            Examen(new DateTime(2026, 5, 10), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Correcta)),
        };

        Assert.Empty(RepasoInteligente.Falladas(historial));
    }

    [Fact]
    public void SiLaVuelveAErrar_VuelveAContarComoFallada()
    {
        // La otra mitad del mismo criterio: "salvo que la vuelva a errar".
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
            Examen(new DateTime(2026, 5, 10), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Correcta)),
            Examen(new DateTime(2026, 5, 20), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
        };

        Assert.Single(RepasoInteligente.Falladas(historial));
    }

    [Fact]
    public void ElOrdenDelHistorial_NoCambiaElResultado()
    {
        // Lo que manda es la fecha, no el orden en que vengan en la lista: el historial se
        // reconstruye entero al refrescar y nada garantiza que llegue ordenado.
        var viejo = Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta));
        var nuevo = Examen(new DateTime(2026, 5, 10), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Correcta));

        Assert.Empty(RepasoInteligente.Falladas(new[] { viejo, nuevo }));
        Assert.Empty(RepasoInteligente.Falladas(new[] { nuevo, viejo }));
    }

    [Fact]
    public void UnExamenSinDetalleGuardado_NoParticipa_RN41()
    {
        // RN-41: un examen de antes de US-025 solo tiene el resumen numérico. No se puede
        // saber qué preguntas falló, así que no aporta ninguna.
        var sinDetalle = new ExamenRendido
        {
            Fecha = new DateTime(2026, 5, 1),
            Materia = "Fisio",
            TotalPreguntas = 10,
            Incorrectas = 7,
        };

        Assert.False(sinDetalle.TieneDetalle);
        Assert.Empty(RepasoInteligente.Falladas(new[] { sinDetalle }));
    }

    [Fact]
    public void LosRepasosSiParticipan_ADiferenciaDelCombinado()
    {
        // Diferencia deliberada con CombinadorDeExamenes, que excluye los repasos porque un
        // repaso no puede ser FUENTE de otro. Acá lo que se lee de un repaso no son sus
        // preguntas sino cómo le fue al alumno, y ese dato vale igual. Sin esto, acertar en un
        // repaso no sacaría la pregunta del pozo y el criterio quedaría incumplido.
        var original = Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta));

        var repaso = Examen(new DateTime(2026, 5, 5), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Correcta));
        repaso.EsRepaso = true;

        Assert.False(repaso.PuedeAlimentarRepaso);
        Assert.Empty(RepasoInteligente.Falladas(new[] { original, repaso }));
    }

    // ------------------------------------------------------------------
    // Foco: materia o documento
    // ------------------------------------------------------------------

    [Fact]
    public void SePuedeAcotarAUnaMateria()
    {
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
            Examen(new DateTime(2026, 5, 2), "Bioqui", "l2", Preg("p2", ResultadoPreguntaEnum.Incorrecta)),
        };

        Assert.Equal(new[] { "p1" },
            RepasoInteligente.Falladas(historial, "Fisio").Select(p => p.Id));
    }

    [Fact]
    public void SePuedeAcotarAUnDocumento()
    {
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
            Examen(new DateTime(2026, 5, 2), "Fisio", "l2", Preg("p2", ResultadoPreguntaEnum.Incorrecta)),
        };

        Assert.Equal(new[] { "p2" },
            RepasoInteligente.Falladas(historial, "l2", esMateria: false).Select(p => p.Id));
    }

    [Fact]
    public void LosFocos_SoloOfrecenLoQueTieneAlgoQueRepasar()
    {
        // Ofrecer una materia con cero falladas daría un examen de cero preguntas: se listan
        // sólo las que hoy tienen algo.
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1",
                Preg("p1", ResultadoPreguntaEnum.Incorrecta),
                Preg("p2", ResultadoPreguntaEnum.Incorrecta)),
            Examen(new DateTime(2026, 5, 2), "Bioqui", "l2", Preg("p3", ResultadoPreguntaEnum.Correcta)),
        };

        var focos = RepasoInteligente.Focos(historial);

        Assert.DoesNotContain(focos, f => f.Nombre == "Bioqui");

        // Y el más fallado primero, que es el orden en que uno quiere ver esta lista.
        Assert.Equal("Fisio", focos[0].Nombre);
        Assert.Equal(2, focos[0].Falladas);
    }

    // ------------------------------------------------------------------
    // Armado
    // ------------------------------------------------------------------

    [Fact]
    public void SiPideMasDeLasQueHay_SaleConTodasYAvisa()
    {
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1",
                Preg("p1", ResultadoPreguntaEnum.Incorrecta),
                Preg("p2", ResultadoPreguntaEnum.Incorrecta)),
        };

        var armado = RepasoInteligente.Armar(historial, 30, azar: new Random(7));

        Assert.Equal(2, armado.Preguntas.Count);
        Assert.Equal(2, armado.Disponibles);
        Assert.Equal(30, armado.Pedidas);
        Assert.True(armado.SeAjustoLaCantidad);
    }

    [Fact]
    public void LasPreguntasSalenEnBlanco_NoConLaRespuestaDeLaVezAnterior()
    {
        // Arrastrar la respuesta marcada mostraría de entrada qué había contestado, y además
        // dejaría el examen "ya respondido" antes de empezar.
        var historial = new[]
        {
            Examen(new DateTime(2026, 5, 1), "Fisio", "l1", Preg("p1", ResultadoPreguntaEnum.Incorrecta)),
        };

        var armado = RepasoInteligente.Armar(historial, 5, azar: new Random(3));

        var pregunta = Assert.Single(armado.Preguntas);

        Assert.Null(pregunta.IndiceRespuestaUsuario);
        Assert.Equal(EstadoPreguntaEnum.SinResponder, pregunta.Estado);
    }

    [Fact]
    public void ElRepaso_NoTocaLasPreguntasDelHistorial()
    {
        // Se trabaja sobre copias: si el repaso mezclara las opciones del objeto guardado, el
        // detalle del examen viejo (US-025) mostraría un orden distinto del que se rindió.
        var original = Preg("p1", ResultadoPreguntaEnum.Incorrecta);
        var historial = new[] { Examen(new DateTime(2026, 5, 1), "Fisio", "l1", original) };

        RepasoInteligente.Armar(historial, 5, azar: new Random(11));

        Assert.Equal(1, original.IndiceRespuestaUsuario);
        Assert.Equal(ResultadoPreguntaEnum.Incorrecta, original.Resultado);
    }

    [Fact]
    public void SinPreguntasFalladas_ElArmadoDevuelveVacio_NoTira()
    {
        var armado = RepasoInteligente.Armar(Array.Empty<ExamenRendido>(), 10);

        Assert.Empty(armado.Preguntas);
        Assert.Equal(0, armado.Disponibles);
    }
}
