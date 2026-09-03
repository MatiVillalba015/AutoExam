using System.Text.Json;
using AutoExam.Models;

namespace AutoExam.Tests.Models;

/// <summary>
/// US-025 / RN-25 / RN-26 — el historial guarda el detalle de cada intento, no solo el
/// resumen numerico.
///
/// Hasta este incremento un examen del historial se podia mirar pero no repasar: a la semana
/// siguiente el registro decia "6/10" y nada mas, asi que no quedaba forma de saber en que te
/// habias equivocado. Guardar las preguntas es lo que convierte al historial en material de
/// estudio, y de paso es la fuente de la que US-026 saca preguntas para un repaso.
/// </summary>
public class DetalleDeExamenTests
{
    private static Pregunta Preg(string texto, int correcta = 0, int? marcada = null)
    {
        var p = new Pregunta
        {
            TextoPregunta = texto,
            Opciones = new List<string> { "a", "b", "c", "d" },
            IndiceRespuestaCorrecta = correcta,
            IndiceRespuestaUsuario = marcada,
            Estado = marcada is null ? EstadoPreguntaEnum.Salteada : EstadoPreguntaEnum.Respondida,
            Resultado = marcada == correcta ? ResultadoPreguntaEnum.Correcta : ResultadoPreguntaEnum.Incorrecta
        };

        p.AnalisisOpciones = new AnalisisOpciones
        {
            ExplicacionCorrecta = "Porque si.",
            AnalisisPorOpcion = new List<string> { "uno", "dos", "tres", "cuatro" }
        };

        return p;
    }

    // ------------------------------------------------------------------
    // RN-25 — el detalle se persiste con el examen
    // ------------------------------------------------------------------

    [Fact]
    public void ElDetalleDelIntento_SobreviveAlGuardadoYRelectura()
    {
        var examen = new ExamenRendido { TotalPreguntas = 2, Correctas = 1 };
        examen.Preguntas.Add(Preg("¿Que es el potencial de accion?", correcta: 0, marcada: 0));
        examen.Preguntas.Add(Preg("¿Que es la bomba sodio-potasio?", correcta: 1, marcada: 3));

        string json = JsonSerializer.Serialize(examen);
        var releido = JsonSerializer.Deserialize<ExamenRendido>(json);

        Assert.NotNull(releido);
        Assert.Equal(2, releido!.Preguntas.Count);
        Assert.Equal("¿Que es el potencial de accion?", releido.Preguntas[0].TextoPregunta);
    }

    [Fact]
    public void SeGuardaQueContesto_CualEraLaCorrectaYElAnalisis()
    {
        // Los tres datos que hacen falta para repasar. Sin el que marco el alumno, el detalle
        // dice cual era la correcta pero no si la habia acertado.
        var examen = new ExamenRendido();
        examen.Preguntas.Add(Preg("Pregunta", correcta: 2, marcada: 3));

        var releido = JsonSerializer.Deserialize<ExamenRendido>(JsonSerializer.Serialize(examen))!;
        var p = releido.Preguntas[0];

        Assert.Equal(3, p.IndiceRespuestaUsuario);
        Assert.Equal(2, p.IndiceRespuestaCorrecta);
        Assert.Equal(ResultadoPreguntaEnum.Incorrecta, p.Resultado);
        Assert.Equal(4, p.AnalisisOpciones.AnalisisPorOpcion.Count);
    }

    [Fact]
    public void LaImagenDeReferencia_SigueApuntandoASuArchivo()
    {
        // US-018 + US-025: la figura tiene que seguir estando al revisar el examen meses
        // despues. Lo que se guarda es la ruta; que el archivo sobreviva a la limpieza
        // periodica lo cubre LimpiezaDeImagenesDelHistorialTests.
        var examen = new ExamenRendido();
        var pregunta = Preg("Sobre el esquema");
        pregunta.RutaImagenAdjunta = @"C:\datos\Imagenes\abc\fig_01.png";
        examen.Preguntas.Add(pregunta);

        var releido = JsonSerializer.Deserialize<ExamenRendido>(JsonSerializer.Serialize(examen))!;

        Assert.Equal(@"C:\datos\Imagenes\abc\fig_01.png", releido.Preguntas[0].RutaImagenAdjunta);
    }

    // ------------------------------------------------------------------
    // RN-26 — los examenes anteriores a US-025 se informan como tales
    // ------------------------------------------------------------------

    [Fact]
    public void UnExamenViejoSinDetalle_SeReconoceComoTal_RN26()
    {
        // Un registro de antes de US-025 no trae el campo: deserializa con la lista vacia.
        // No se intenta reconstruirlo (RN-25), se lo informa.
        const string viejo = """
            { "Id": "x1", "LibroTitulo": "Guyton", "TotalPreguntas": 10, "Correctas": 6, "NotaUBA": 6 }
            """;

        var examen = JsonSerializer.Deserialize<ExamenRendido>(viejo)!;

        Assert.False(examen.TieneDetalle);
        Assert.Empty(examen.Preguntas);

        // El resumen numerico si esta: es lo unico que aquella version guardaba.
        Assert.Equal(10, examen.TotalPreguntas);
        Assert.Equal(6, examen.NotaUBA);
    }

    [Fact]
    public void UnExamenNuevo_SiTieneDetalle()
    {
        var examen = new ExamenRendido();
        examen.Preguntas.Add(Preg("Una"));

        Assert.True(examen.TieneDetalle);
    }

    // ------------------------------------------------------------------
    // El registro es una foto del intento, no un puntero al examen vivo
    // ------------------------------------------------------------------

    [Fact]
    public void ClonarParaHistorial_ConservaLaRespuestaYElResultado()
    {
        // Clonar() los descarta a proposito, porque su unico uso es armar una revancha donde
        // la pregunta vuelve a empezar en blanco. El historial necesita lo contrario.
        var original = Preg("Pregunta", correcta: 1, marcada: 2);

        var copia = original.ClonarParaHistorial();

        Assert.Equal(2, copia.IndiceRespuestaUsuario);
        Assert.Equal(ResultadoPreguntaEnum.Incorrecta, copia.Resultado);
        Assert.Equal(EstadoPreguntaEnum.Respondida, copia.Estado);
    }

    [Fact]
    public void ClonarParaHistorial_DevuelveUnaInstanciaAparte()
    {
        // Es lo que evita que encadenar revanchas sobre el examen que sigue en pantalla
        // reescriba el detalle ya registrado.
        var original = Preg("Pregunta", correcta: 1, marcada: 1);

        var copia = original.ClonarParaHistorial();
        original.IndiceRespuestaUsuario = 3;
        original.Resultado = ResultadoPreguntaEnum.Incorrecta;

        Assert.Equal(1, copia.IndiceRespuestaUsuario);
        Assert.Equal(ResultadoPreguntaEnum.Correcta, copia.Resultado);
    }

    [Fact]
    public void ClonarNormal_SigueLimpiandoLaRespuesta_ParaLaRevancha()
    {
        // Guarda de no-regresion: si Clonar() empezara a copiar la respuesta, la revancha
        // arrancaria con las preguntas ya contestadas.
        var original = Preg("Pregunta", correcta: 1, marcada: 2);

        Assert.Null(original.Clonar().IndiceRespuestaUsuario);
    }

    // ------------------------------------------------------------------
    // AC — al revisar el detalle se ve el analisis TAMBIEN en las falladas
    // ------------------------------------------------------------------

    [Fact]
    public void AlCorregirEnElMomento_UnaFallada_NoRevelaLaRespuesta()
    {
        // Si el error mostrara la respuesta, el Modo Revancha no serviria para nada.
        var fallada = Preg("Pregunta", correcta: 1, marcada: 2);

        Assert.False(fallada.MuestraAnalisisCompleto);
        Assert.False(fallada.MuestraRespuestaCorrecta);
    }

    [Fact]
    public void EnElDetalleDelHistorial_UnaFallada_SiRevelaElAnalisis()
    {
        // Es el motivo por el que existe la pantalla: entrar a ver en que te equivocaste.
        var fallada = Preg("Pregunta", correcta: 1, marcada: 2);

        fallada.RevelarAnalisis = true;

        Assert.True(fallada.MuestraAnalisisCompleto);
        Assert.True(fallada.MuestraRespuestaCorrecta);
    }

    [Fact]
    public void RevelarAnalisis_NoSePersiste()
    {
        // Es como se esta mirando la pregunta, no un dato del intento: guardarlo dejaria
        // examenes que revelan y otros que no segun por donde se los haya abierto.
        var pregunta = Preg("Pregunta");
        pregunta.RevelarAnalisis = true;

        string json = JsonSerializer.Serialize(pregunta);

        Assert.DoesNotContain("RevelarAnalisis", json, StringComparison.Ordinal);
    }
}
