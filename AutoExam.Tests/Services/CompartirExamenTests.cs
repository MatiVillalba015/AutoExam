using System.IO;
using System.Text.Json;
using AutoExam.Models;
using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-037 — exportar un examen para que lo rinda un compañero, e importar el suyo.
///
/// El valor de la historia es no volver a gastar cuota de Gemini, así que el archivo tiene que
/// traer TODO lo necesario para rendir y corregir sin conexión. Y RN-45 pone el límite del
/// otro lado: nada del alumno que lo generó.
/// </summary>
public class CompartirExamenTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(), "autoexam-compartir-" + Guid.NewGuid().ToString("N"));

    public CompartirExamenTests() => Directory.CreateDirectory(_carpeta);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_carpeta, recursive: true);
        }
        catch (IOException)
        {
            // Una carpeta temporal que no se pudo borrar no invalida el test.
        }
    }

    private string Ruta(string nombre) => Path.Combine(_carpeta, nombre);

    private static Pregunta Preg(string texto, int correcta = 0)
    {
        var p = new Pregunta
        {
            TextoPregunta = texto,
            Opciones = new List<string> { "a", "b", "c", "d" },
            IndiceRespuestaCorrecta = correcta,
            JustificacionBibliografia = "pag. 12",
            AnalisisOpciones = new AnalisisOpciones
            {
                ExplicacionCorrecta = "porque si",
                AnalisisPorOpcion = new List<string> { "1", "2", "3", "4" },
            },
        };

        // Estado de ESTE alumno: es justamente lo que no tiene que viajar.
        p.IndiceRespuestaUsuario = 2;

        return p;
    }

    private static ExamenEnCurso ExamenDePrueba()
    {
        var examen = new ExamenEnCurso
        {
            LibroTitulo = "Bolilla 4",
            Materia = "Fisiologia",
            AlcanceDescripcion = "capitulos 1 a 3",
        };

        examen.Preguntas.Add(Preg("¿Que es la sinapsis?"));
        examen.Preguntas.Add(Preg("¿Que es el potencial de accion?", 2));

        return examen;
    }

    // ------------------------------------------------------------------
    // RN-45 — nada personal
    // ------------------------------------------------------------------

    [Fact]
    public void ElArchivo_NoLlevaLasRespuestasDeQuienLoExporto_RN45()
    {
        string ruta = Ruta("examen" + CompartirExamenService.Extension);

        CompartirExamenService.Guardar(CompartirExamenService.Empaquetar(ExamenDePrueba()), ruta);

        string json = File.ReadAllText(ruta);

        // Ni la respuesta marcada, ni el estado, ni el resultado del alumno que exporto.
        Assert.DoesNotContain("IndiceRespuestaUsuario", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Resultado", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Estado", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ElArchivo_NoLlevaNadaDelHistorialNiDeLasNotas_RN45()
    {
        // El paquete es un tipo aparte y no una serializacion del modelo interno: eso es lo
        // que garantiza que un campo personal agregado mañana a ExamenRendido no se cuele.
        var propiedades = typeof(ExamenCompartido).GetProperties().Select(p => p.Name).ToList();

        foreach (string prohibido in new[] { "Nota", "NotaUBA", "Aprobado", "Correctas", "Historial", "Revanchas" })
        {
            Assert.DoesNotContain(propiedades, p => p.Contains(prohibido, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void UnExamenDelHistorial_SeExportaSinSuNota_RN45()
    {
        var rendido = new ExamenRendido
        {
            LibroTitulo = "Tp2",
            Materia = "Bioqui",
            NotaUBA = 9,
            Correctas = 9,
            Aprobado = true,
            Preguntas = new List<Pregunta> { Preg("¿Que es una enzima?") },
        };

        string ruta = Ruta("delhistorial" + CompartirExamenService.Extension);
        CompartirExamenService.Guardar(CompartirExamenService.Empaquetar(rendido), ruta);

        string json = File.ReadAllText(ruta);

        Assert.Contains("enzima", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"NotaUBA\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Aprobado\"", json, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Ida y vuelta
    // ------------------------------------------------------------------

    [Fact]
    public void ExportarEImportar_ConservaLoNecesarioParaRendirYCorregir()
    {
        string ruta = Ruta("ida" + CompartirExamenService.Extension);
        var original = ExamenDePrueba();

        CompartirExamenService.Guardar(CompartirExamenService.Empaquetar(original), ruta);

        var leido = CompartirExamenService.Leer(ruta);
        Assert.True(leido.Ok);

        var preguntas = CompartirExamenService.Desempaquetar(leido.Examen!, Ruta("img"));

        Assert.Equal(2, preguntas.Count);
        Assert.Equal("¿Que es la sinapsis?", preguntas[0].TextoPregunta);
        Assert.Equal(4, preguntas[0].Opciones.Count);
        Assert.Equal(2, preguntas[1].IndiceRespuestaCorrecta);

        // Sin las justificaciones el examen se puede rendir pero no se puede aprender de el.
        Assert.Equal("porque si", preguntas[0].AnalisisOpciones.ExplicacionCorrecta);
        Assert.Equal("pag. 12", preguntas[0].JustificacionBibliografia);

        // Y llega en blanco: es un examen para rendir, no el intento de otra persona.
        Assert.All(preguntas, p => Assert.Null(p.IndiceRespuestaUsuario));
    }

    [Fact]
    public void LaPreguntaImportada_TieneIdPropio_NoElDelCompañero()
    {
        // Los Ids son la identidad que usa el repaso inteligente (US-032). Si viajaran, cómo
        // le fue al compañero contaminaría el historial de aciertos de este alumno.
        string ruta = Ruta("ids" + CompartirExamenService.Extension);
        var original = ExamenDePrueba();
        string idOriginal = original.Preguntas[0].Id;

        CompartirExamenService.Guardar(CompartirExamenService.Empaquetar(original), ruta);
        var preguntas = CompartirExamenService.Desempaquetar(
            CompartirExamenService.Leer(ruta).Examen!, Ruta("img"));

        Assert.NotEqual(idOriginal, preguntas[0].Id);
        Assert.False(string.IsNullOrWhiteSpace(preguntas[0].Id));
    }

    [Fact]
    public void LasImagenesDeReferencia_ViajanAdentroDelArchivo_US018()
    {
        // El criterio pide que "las imágenes viajen incluidas, no se pierdan ni queden rotas".
        // Una ruta del disco de quien exporta no existe del otro lado.
        string imagen = Ruta("figura.png");
        File.WriteAllBytes(imagen, new byte[] { 1, 2, 3, 4, 5 });

        var examen = ExamenDePrueba();
        examen.Preguntas[0].RutaImagenAdjunta = imagen;

        string ruta = Ruta("conimagen" + CompartirExamenService.Extension);
        CompartirExamenService.Guardar(CompartirExamenService.Empaquetar(examen), ruta);

        // Se borra el original: si el paquete guardara la ruta, acá se rompería.
        File.Delete(imagen);

        var preguntas = CompartirExamenService.Desempaquetar(
            CompartirExamenService.Leer(ruta).Examen!, Ruta("destino"));

        Assert.NotNull(preguntas[0].RutaImagenAdjunta);
        Assert.True(File.Exists(preguntas[0].RutaImagenAdjunta!));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, File.ReadAllBytes(preguntas[0].RutaImagenAdjunta!));
    }

    // ------------------------------------------------------------------
    // Archivos inválidos: se rechazan con un motivo, sin romper nada
    // ------------------------------------------------------------------

    [Fact]
    public void UnArchivoQueNoExiste_SeRechazaConMotivo()
    {
        var resultado = CompartirExamenService.Leer(Ruta("no-existe.axexam"));

        Assert.False(resultado.Ok);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Error));
    }

    [Fact]
    public void UnJsonRoto_SeRechazaConMotivo_NoTira()
    {
        string ruta = Ruta("roto" + CompartirExamenService.Extension);
        File.WriteAllText(ruta, "{ esto no es json valido ");

        var resultado = CompartirExamenService.Leer(ruta);

        Assert.False(resultado.Ok);
        Assert.Contains("dañado", resultado.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnJsonDeOtraCosa_SeRechazaPorLaMarcaDeFormato()
    {
        // Un JSON perfectamente válido que no es un examen: sin la marca de formato, esto
        // pasaría como examen vacío en vez de decir qué pasó.
        string ruta = Ruta("otracosa" + CompartirExamenService.Extension);
        File.WriteAllText(ruta, JsonSerializer.Serialize(new { Hola = "mundo" }));

        var resultado = CompartirExamenService.Leer(ruta);

        Assert.False(resultado.Ok);
        Assert.Contains("AutoExam", resultado.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnArchivoDeUnaVersionMasNueva_LoDiceEnVezDeLlamarloCorrupto()
    {
        // "De otra versión incompatible" es un caso distinto de "corrupto", y el mensaje útil
        // es distinto: acá hay que actualizar la app, no pedir el archivo de nuevo.
        string ruta = Ruta("futuro" + CompartirExamenService.Extension);

        var paquete = CompartirExamenService.Empaquetar(ExamenDePrueba());
        paquete.Version = CompartirExamenService.VersionActual + 5;

        CompartirExamenService.Guardar(paquete, ruta);

        var resultado = CompartirExamenService.Leer(ruta);

        Assert.False(resultado.Ok);
        Assert.Contains("versión", resultado.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnExamenSinPreguntas_SeRechaza()
    {
        string ruta = Ruta("vacio" + CompartirExamenService.Extension);

        // Con la marca puesta: es un archivo de AutoExam de verdad, sólo que sin preguntas.
        CompartirExamenService.Guardar(
            new ExamenCompartido { Formato = CompartirExamenService.MarcaDeFormato, Titulo = "Vacio" }, ruta);

        var resultado = CompartirExamenService.Leer(ruta);

        Assert.False(resultado.Ok);
        Assert.Contains("pregunta", resultado.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnaPreguntaSinCorrectaValida_SeRechaza()
    {
        // Un examen que no sabe cuál es la correcta no se puede corregir: entra roto y el
        // alumno lo descubre recién al terminar de rendirlo.
        string ruta = Ruta("sincorrecta" + CompartirExamenService.Extension);

        var paquete = CompartirExamenService.Empaquetar(ExamenDePrueba());
        paquete.Preguntas[0].IndiceRespuestaCorrecta = 9;

        CompartirExamenService.Guardar(paquete, ruta);

        var resultado = CompartirExamenService.Leer(ruta);

        Assert.False(resultado.Ok);
        Assert.Contains("correcta", resultado.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ElNombreSugerido_NoTieneCaracteresQueWindowsRechaza()
    {
        string nombre = CompartirExamenService.NombreSugerido("Tp2: Endocrino / parcial?");

        Assert.DoesNotContain(nombre, c => Path.GetInvalidFileNameChars().Contains(c));
        Assert.EndsWith(CompartirExamenService.Extension, nombre, StringComparison.Ordinal);
    }
}
