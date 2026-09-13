using System.Globalization;
using System.IO;

namespace AutoExam.Services;

/// <summary>
/// Lo que ocupa un grupo de archivos. <paramref name="Medido"/> distingue "cero bytes" de
/// "no se pudo medir": el criterio de US-046 pide decirlo en vez de mostrar un numero
/// incorrecto o dejar el renglon en blanco, y sin este campo las dos cosas se ven igual.
/// </summary>
public sealed record UsoDeDisco(string Etiqueta, long Bytes, bool Medido)
{
    public string Texto => Medido ? AlmacenamientoService.Legible(Bytes) : "no se pudo medir";
}

/// <summary>
/// Cuanto ocupa AutoExam en disco y como liberar lo que sobra (US-046).
///
/// Los tres grupos son los que el usuario reconoce —sus libros, su historial y lo temporal—
/// y no las carpetas internas. La division importa por RN-54: "Vaciar cache" borra exactamente
/// el tercer grupo y nada mas, asi que el numero que se ve al lado de "Temporales" es la
/// promesa de cuanto se va a liberar.
///
/// <b>Que cuenta como temporal:</b> las carpetas de imagenes de examenes que ya no estan en el
/// historial. Son intermedios de extraccion de intentos que se abandonaron sin registrarse, y
/// se regeneran solos la proxima vez que se genere ese examen. Las imagenes de un examen que SI
/// sigue en el historial no son temporales por mas viejas que sean: sin ellas, el detalle de
/// US-025 y las preguntas con figura de US-018 quedan con un hueco.
/// </summary>
public static class AlmacenamientoService
{
    /// <summary>
    /// Mide los tres grupos. <paramref name="examenesVivos"/> son los ids de los examenes que
    /// siguen en el historial: es lo unico que permite separar una carpeta de imagenes que hay
    /// que conservar de una huerfana.
    /// </summary>
    public static IReadOnlyList<UsoDeDisco> Medir(IEnumerable<string> examenesVivos)
    {
        var vivos = new HashSet<string>(examenesVivos ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        var (imagenesVivas, imagenesHuerfanas) = MedirImagenes(vivos);

        var libros = Sumar(
            () => TamanioDeCarpeta(RutasApp.Biblioteca)
                  + TamanioDeArchivo(RutasApp.ArchivoLibros)
                  + TamanioDeArchivo(RutasApp.ArchivoMaterias));

        var historial = Sumar(
            () => TamanioDeArchivo(RutasApp.ArchivoPerfil)
                  + TamanioDeCarpeta(RutasApp.Compartidos)
                  + imagenesVivas.Bytes);

        return new[]
        {
            new UsoDeDisco("Libros", libros.Bytes, libros.Medido && imagenesVivas.Medido),
            new UsoDeDisco("Historial", historial.Bytes, historial.Medido && imagenesVivas.Medido),
            imagenesHuerfanas with { Etiqueta = "Temporales" },
        };
    }

    /// <summary>
    /// Borra lo temporal y devuelve cuantos bytes se liberaron. Nunca toca un libro, un examen
    /// del historial ni la configuracion (RN-54): lo unico que borra son las carpetas de
    /// imagenes que no pertenecen a ningun examen guardado.
    /// </summary>
    public static long VaciarCache(IEnumerable<string> examenesVivos)
    {
        var vivos = new HashSet<string>(examenesVivos ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        long liberados = 0;

        foreach (string carpeta in CarpetasHuerfanas(vivos))
        {
            try
            {
                liberados += TamanioDeCarpeta(carpeta);
                Directory.Delete(carpeta, recursive: true);
            }
            catch (Exception ex)
            {
                // Una carpeta que el sistema tiene tomada no puede frenar la limpieza de las
                // demas: se anota y se sigue.
                RutasApp.RegistrarError("VaciarCache", ex);
            }
        }

        return liberados;
    }

    private static (UsoDeDisco Bytes, UsoDeDisco Huerfanas) MedirImagenes(HashSet<string> vivos)
    {
        long deExamenesVivos = 0;
        long huerfanas = 0;
        bool medido = true;

        try
        {
            if (Directory.Exists(RutasApp.Imagenes))
            {
                foreach (string carpeta in Directory.GetDirectories(RutasApp.Imagenes))
                {
                    long tamanio = TamanioDeCarpeta(carpeta);

                    if (vivos.Contains(Path.GetFileName(carpeta)))
                    {
                        deExamenesVivos += tamanio;
                    }
                    else
                    {
                        huerfanas += tamanio;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("MedirImagenes", ex);
            medido = false;
        }

        return (new UsoDeDisco("Imagenes", deExamenesVivos, medido),
                new UsoDeDisco("Huerfanas", huerfanas, medido));
    }

    private static IEnumerable<string> CarpetasHuerfanas(HashSet<string> vivos)
    {
        if (!Directory.Exists(RutasApp.Imagenes))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.GetDirectories(RutasApp.Imagenes)
                .Where(c => !vivos.Contains(Path.GetFileName(c)))
                .ToList();
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("CarpetasHuerfanas", ex);
            return Array.Empty<string>();
        }
    }

    private static (long Bytes, bool Medido) Sumar(Func<long> calculo)
    {
        try
        {
            return (calculo(), true);
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("MedirAlmacenamiento", ex);
            return (0, false);
        }
    }

    private static long TamanioDeCarpeta(string ruta)
    {
        if (!Directory.Exists(ruta))
        {
            return 0;
        }

        long total = 0;

        foreach (string archivo in Directory.EnumerateFiles(ruta, "*", SearchOption.AllDirectories))
        {
            total += TamanioDeArchivo(archivo);
        }

        return total;
    }

    private static long TamanioDeArchivo(string ruta)
    {
        try
        {
            var info = new FileInfo(ruta);
            return info.Exists ? info.Length : 0;
        }
        catch
        {
            // Un archivo suelto ilegible no puede tumbar la medicion entera: cuenta como cero
            // y el total queda apenas corto, que es mucho mejor que no mostrar nada.
            return 0;
        }
    }

    /// <summary>
    /// Bytes en la unidad que una persona usa para hablar de espacio. Sin decimales por debajo
    /// de un mega —"312 KB" es mas claro que "0,3 MB"— y con uno arriba.
    /// </summary>
    public static string Legible(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString("0", CultureInfo.CurrentCulture) + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024d).ToString("0", CultureInfo.CurrentCulture) + " KB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return (bytes / (1024d * 1024)).ToString("0.#", CultureInfo.CurrentCulture) + " MB";
        }

        return (bytes / (1024d * 1024 * 1024)).ToString("0.0", CultureInfo.CurrentCulture) + " GB";
    }
}
