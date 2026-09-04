using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>Una pregunta dentro de un archivo compartido, con su imagen embebida si tenia.</summary>
public sealed class PreguntaCompartida
{
    public string TextoPregunta { get; set; } = string.Empty;
    public List<string> Opciones { get; set; } = new();
    public int IndiceRespuestaCorrecta { get; set; }
    public string JustificacionBibliografia { get; set; } = string.Empty;
    public string ExplicacionCorrecta { get; set; } = string.Empty;
    public List<string> AnalisisPorOpcion { get; set; } = new();
    public int PaginaOrigen { get; set; }
    public string DocumentoOrigen { get; set; } = string.Empty;

    /// <summary>
    /// La imagen de referencia (US-018) viaja adentro del archivo en base64, no como una ruta.
    /// Una ruta del disco de quien exporto no existe del otro lado: la imagen llegaria rota,
    /// que es justo lo que el criterio pide evitar.
    /// </summary>
    public string? ImagenBase64 { get; set; }

    public string? ImagenExtension { get; set; }
}

/// <summary>
/// El archivo que se comparte (US-037).
///
/// RN-45: contiene el examen y nada mas. No hay historial, ni notas, ni progreso, ni quien lo
/// genero, ni la clave de nadie. Es una decision de forma y no de filtrado: al ser un tipo
/// aparte en vez de una serializacion de <see cref="ExamenEnCurso"/>, no existe la posibilidad
/// de que un campo personal se cuele al agregarse mañana al modelo interno.
/// </summary>
public sealed class ExamenCompartido
{
    /// <summary>
    /// Marca de formato. Lo primero que se valida al importar.
    ///
    /// Deliberadamente SIN valor por defecto: con un inicializador, un JSON cualquiera que no
    /// trae el campo se deserializa con la marca puesta y pasa el control como si fuera un
    /// examen de AutoExam. El archivo termina rechazado igual, pero por "no tiene preguntas",
    /// que no le dice nada a quien abrio el archivo equivocado. Quien exporta la escribe
    /// explicitamente en <see cref="CompartirExamenService.Empaquetar(ExamenEnCurso)"/>.
    /// </summary>
    public string? Formato { get; set; }

    /// <summary>Version del formato, para poder rechazar con un mensaje util (no "corrupto").</summary>
    public int Version { get; set; } = CompartirExamenService.VersionActual;

    public string Titulo { get; set; } = string.Empty;
    public string Materia { get; set; } = string.Empty;
    public string AlcanceDescripcion { get; set; } = string.Empty;

    /// <summary>Cuando se exporto. Es del examen, no del alumno.</summary>
    public DateTime Exportado { get; set; } = DateTime.Now;

    public List<PreguntaCompartida> Preguntas { get; set; } = new();
}

/// <summary>Por que no se pudo importar un archivo.</summary>
public sealed record ResultadoImportacion(ExamenCompartido? Examen, string? Error)
{
    public bool Ok => Examen is not null;
}

/// <summary>
/// Exporta e importa examenes para compartirlos entre instalaciones de AutoExam (US-037).
///
/// El valor de la historia es no volver a gastar cuota de Gemini: el compañero rinde el mismo
/// examen sin generarlo de nuevo. Por eso el archivo lleva todo lo que hace falta para rendir
/// y corregir sin conexion —enunciados, opciones, cual es la correcta, las justificaciones y
/// las imagenes—, y nada mas que eso.
/// </summary>
public static class CompartirExamenService
{
    public const string MarcaDeFormato = "autoexam.examen";
    public const int VersionActual = 1;
    public const string Extension = ".axexam";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ------------------------------------------------------------------
    // Exportar
    // ------------------------------------------------------------------

    /// <summary>Arma el paquete compartible a partir de un examen ya generado.</summary>
    public static ExamenCompartido Empaquetar(ExamenEnCurso examen)
    {
        var paquete = new ExamenCompartido
        {
            Formato = MarcaDeFormato,
            Titulo = examen.LibroTitulo,
            Materia = examen.Materia,
            AlcanceDescripcion = examen.AlcanceDescripcion,
        };

        foreach (var pregunta in examen.Preguntas)
        {
            paquete.Preguntas.Add(Empaquetar(pregunta));
        }

        return paquete;
    }

    /// <summary>Igual, pero desde un examen del historial que si guardo su detalle (US-025).</summary>
    public static ExamenCompartido Empaquetar(ExamenRendido examen)
    {
        var paquete = new ExamenCompartido
        {
            Formato = MarcaDeFormato,
            Titulo = examen.TituloTexto,
            Materia = examen.Materia,
            AlcanceDescripcion = examen.AlcanceDescripcion,
        };

        foreach (var pregunta in examen.Preguntas)
        {
            paquete.Preguntas.Add(Empaquetar(pregunta));
        }

        return paquete;
    }

    private static PreguntaCompartida Empaquetar(Pregunta p)
    {
        var salida = new PreguntaCompartida
        {
            TextoPregunta = p.TextoPregunta,
            Opciones = new List<string>(p.Opciones),
            IndiceRespuestaCorrecta = p.IndiceRespuestaCorrecta,
            JustificacionBibliografia = p.JustificacionBibliografia,
            ExplicacionCorrecta = p.AnalisisOpciones.ExplicacionCorrecta,
            AnalisisPorOpcion = new List<string>(p.AnalisisOpciones.AnalisisPorOpcion),
            PaginaOrigen = p.PaginaOrigen,
            DocumentoOrigen = p.DocumentoOrigen,
        };

        // Nunca se copia IndiceRespuestaUsuario, Estado ni Resultado: eso es como le fue a
        // quien exporto, no parte del examen (RN-45).
        if (p.TieneImagen && p.RutaImagenAdjunta is string ruta)
        {
            try
            {
                salida.ImagenBase64 = Convert.ToBase64String(File.ReadAllBytes(ruta));
                salida.ImagenExtension = Path.GetExtension(ruta);
            }
            catch (Exception ex)
            {
                // Una figura ilegible no puede impedir compartir el examen: la pregunta viaja
                // sin imagen, que es peor que con ella pero muchisimo mejor que nada.
                RutasApp.RegistrarError("Compartir.Imagen", ex);
            }
        }

        return salida;
    }

    public static void Guardar(ExamenCompartido paquete, string ruta)
    {
        string carpeta = Path.GetDirectoryName(ruta) ?? string.Empty;

        if (carpeta.Length > 0)
        {
            Directory.CreateDirectory(carpeta);
        }

        File.WriteAllText(ruta, JsonSerializer.Serialize(paquete, Opciones));
    }

    // ------------------------------------------------------------------
    // Importar
    // ------------------------------------------------------------------

    /// <summary>
    /// Lee un archivo compartido. Nunca tira: todo error vuelve como texto explicable, porque
    /// el criterio pide rechazar con un mensaje claro "sin romperse" y el archivo lo eligio
    /// una persona en un selector, no la app.
    /// </summary>
    public static ResultadoImportacion Leer(string ruta)
    {
        try
        {
            if (!File.Exists(ruta))
            {
                return new ResultadoImportacion(null, "No se encuentra el archivo.");
            }

            var paquete = JsonSerializer.Deserialize<ExamenCompartido>(File.ReadAllText(ruta), Opciones);

            if (paquete is null)
            {
                return new ResultadoImportacion(null, "El archivo está vacío o no es un examen de AutoExam.");
            }

            if (!string.Equals(paquete.Formato, MarcaDeFormato, StringComparison.Ordinal))
            {
                return new ResultadoImportacion(null,
                    "Ese archivo no es un examen de AutoExam. Pedile a tu compañero que lo exporte " +
                    "desde la pantalla de examen.");
            }

            if (paquete.Version > VersionActual)
            {
                return new ResultadoImportacion(null,
                    $"El examen se exportó con una versión más nueva de AutoExam (formato {paquete.Version}). " +
                    "Actualizá la app para poder abrirlo.");
            }

            if (paquete.Preguntas.Count == 0)
            {
                return new ResultadoImportacion(null, "El archivo no tiene ninguna pregunta adentro.");
            }

            var malas = paquete.Preguntas
                .Where(p => p.Opciones.Count < 2 ||
                            p.IndiceRespuestaCorrecta < 0 ||
                            p.IndiceRespuestaCorrecta >= p.Opciones.Count ||
                            string.IsNullOrWhiteSpace(p.TextoPregunta))
                .ToList();

            if (malas.Count > 0)
            {
                return new ResultadoImportacion(null,
                    $"El archivo está dañado: {malas.Count} de {paquete.Preguntas.Count} preguntas no " +
                    "tienen opciones válidas o no dicen cuál es la correcta.");
            }

            return new ResultadoImportacion(paquete, null);
        }
        catch (JsonException)
        {
            return new ResultadoImportacion(null, "El archivo está dañado y no se pudo leer como un examen.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Compartir.Leer", ex);
            return new ResultadoImportacion(null, $"No se pudo abrir el archivo: {ex.Message}");
        }
    }

    /// <summary>
    /// Convierte el paquete en preguntas rendibles. Las imagenes se vuelcan a disco bajo
    /// <paramref name="carpetaDeImagenes"/>, porque el resto de la app dibuja figuras desde
    /// una ruta y no desde bytes en memoria.
    /// </summary>
    public static IReadOnlyList<Pregunta> Desempaquetar(ExamenCompartido paquete, string carpetaDeImagenes)
    {
        var preguntas = new List<Pregunta>();

        foreach (var origen in paquete.Preguntas)
        {
            var pregunta = new Pregunta
            {
                // Id nuevo a proposito: es una pregunta distinta de la del compañero para
                // todo lo que mira Ids (el repaso inteligente de US-032, por ejemplo). Que a
                // el le haya ido bien o mal no dice nada de este alumno.
                TextoPregunta = origen.TextoPregunta,
                Opciones = new List<string>(origen.Opciones),
                IndiceRespuestaCorrecta = origen.IndiceRespuestaCorrecta,
                JustificacionBibliografia = origen.JustificacionBibliografia,
                PaginaOrigen = origen.PaginaOrigen,
                DocumentoOrigen = origen.DocumentoOrigen,
                AnalisisOpciones = new AnalisisOpciones
                {
                    ExplicacionCorrecta = origen.ExplicacionCorrecta,
                    AnalisisPorOpcion = new List<string>(origen.AnalisisPorOpcion),
                },
            };

            if (!string.IsNullOrWhiteSpace(origen.ImagenBase64))
            {
                pregunta.RutaImagenAdjunta = VolcarImagen(origen, carpetaDeImagenes);
            }

            preguntas.Add(pregunta);
        }

        return preguntas;
    }

    private static string? VolcarImagen(PreguntaCompartida origen, string carpeta)
    {
        try
        {
            Directory.CreateDirectory(carpeta);

            string extension = string.IsNullOrWhiteSpace(origen.ImagenExtension) ? ".png" : origen.ImagenExtension!;
            string ruta = Path.Combine(carpeta, $"{Guid.NewGuid():N}{extension}");

            File.WriteAllBytes(ruta, Convert.FromBase64String(origen.ImagenBase64!));

            return ruta;
        }
        catch (Exception ex)
        {
            // Igual que al exportar: la pregunta sale sin figura antes que caerse el examen.
            RutasApp.RegistrarError("Compartir.VolcarImagen", ex);
            return null;
        }
    }

    /// <summary>Nombre de archivo sugerido, sin caracteres que Windows rechace.</summary>
    public static string NombreSugerido(string titulo)
    {
        string limpio = string.Join("_",
            (string.IsNullOrWhiteSpace(titulo) ? "examen" : titulo).Split(Path.GetInvalidFileNameChars()));

        return limpio.Length > 60 ? limpio[..60] + Extension : limpio + Extension;
    }
}
