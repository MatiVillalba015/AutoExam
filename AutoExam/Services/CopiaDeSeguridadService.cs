using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace AutoExam.Services;

/// <summary>Lo que trae una copia, para poder decirlo antes de reemplazar nada.</summary>
public sealed record ResumenDeCopia(string Version, DateTime Fecha, int Libros, int Examenes);

/// <summary>Se tira cuando el archivo elegido no es una copia de AutoExam o esta rota.</summary>
public class CopiaInvalidaException : Exception
{
    public CopiaInvalidaException(string mensaje) : base(mensaje)
    {
    }
}

/// <summary>
/// Copia de seguridad manual y local (US-051).
///
/// Es un ZIP con la carpeta de datos entera —libros, materias, historial completo con el
/// detalle de preguntas de US-025, y config.json con los ajustes, los colores por materia y
/// las claves— mas un manifiesto que la identifica como copia de AutoExam.
///
/// <b>Local siempre (RN-59):</b> este servicio no conoce ninguna URL. Escribe en la ruta que
/// eligio el usuario y lee de la que el usuario abrio; no hay cuenta, ni servidor, ni
/// sincronizacion. Que el archivo termine en un pendrive o en una nube de terceros es una
/// decision del usuario sobre un archivo suyo, no algo que haga la app.
///
/// <b>Por que un ZIP y no un JSON gigante:</b> los PDF de la biblioteca y las imagenes de los
/// examenes son binarios de varios MB. Meterlos en base64 dentro de un JSON multiplicaria el
/// tamanio por cuatro tercios y obligaria a tener el archivo entero en memoria para leerlo.
/// </summary>
public static class CopiaDeSeguridadService
{
    /// <summary>Extension propia: es lo que hace que la copia se reconozca de un vistazo en Descargas.</summary>
    public const string Extension = ".axcopia";

    /// <summary>
    /// Nombre del manifiesto adentro del ZIP. Su presencia es lo que distingue una copia de
    /// AutoExam de cualquier otro ZIP: sin el, importar un comprimido cualquiera desparramaria
    /// sus archivos sobre la carpeta de datos.
    /// </summary>
    private const string Manifiesto = "autoexam-copia.json";

    /// <summary>Carpetas de datos que entran en la copia, ademas de los .json sueltos de la raiz.</summary>
    private static readonly string[] Carpetas = { "Biblioteca", "Imagenes", "Compartidos" };

    /// <summary>Archivos sueltos de la raiz que entran en la copia.</summary>
    private static readonly string[] Archivos =
    {
        "libros.json", "materias.json", "perfil.json", "config.json"
    };

    public static string NombreSugerido() =>
        $"AutoExam {DateTime.Now:yyyy-MM-dd}{Extension}";

    // ------------------------------------------------------------------
    // Exportar
    // ------------------------------------------------------------------

    /// <summary>
    /// Escribe la copia en <paramref name="destino"/>, reemplazandola si ya existia.
    /// Devuelve el tamanio del archivo generado.
    /// </summary>
    public static long Exportar(string destino, int libros, int examenes)
    {
        RutasApp.AsegurarCarpetas();

        // Se arma en un temporal y recien al final se mueve al destino: si algo falla a mitad
        // de camino, el usuario no se queda con un .axcopia truncado que parece una copia
        // valida hasta el dia que la necesita.
        string temporal = Path.Combine(Path.GetTempPath(), $"autoexam-{Guid.NewGuid():N}.zip");

        try
        {
            using (var archivo = ZipFile.Open(temporal, ZipArchiveMode.Create))
            {
                EscribirManifiesto(archivo, libros, examenes);

                foreach (string nombre in Archivos)
                {
                    string ruta = Path.Combine(RutasApp.Raiz, nombre);

                    if (File.Exists(ruta))
                    {
                        archivo.CreateEntryFromFile(ruta, nombre, CompressionLevel.Optimal);
                    }
                }

                foreach (string carpeta in Carpetas)
                {
                    AgregarCarpeta(archivo, Path.Combine(RutasApp.Raiz, carpeta), carpeta);
                }
            }

            File.Move(temporal, destino, overwrite: true);

            return new FileInfo(destino).Length;
        }
        finally
        {
            if (File.Exists(temporal))
            {
                try
                {
                    File.Delete(temporal);
                }
                catch
                {
                    // El temporal quedo tomado: lo limpia Windows. No vale la pena romper por esto.
                }
            }
        }
    }

    private static void EscribirManifiesto(ZipArchive archivo, int libros, int examenes)
    {
        var resumen = new ResumenDeCopia(ActualizacionService.VersionActual, DateTime.Now, libros, examenes);

        var entrada = archivo.CreateEntry(Manifiesto, CompressionLevel.Optimal);

        using var flujo = entrada.Open();
        JsonSerializer.Serialize(flujo, resumen, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void AgregarCarpeta(ZipArchive archivo, string origen, string prefijo)
    {
        if (!Directory.Exists(origen))
        {
            return;
        }

        foreach (string ruta in Directory.EnumerateFiles(origen, "*", SearchOption.AllDirectories))
        {
            string relativa = Path.GetRelativePath(origen, ruta).Replace('\\', '/');

            try
            {
                archivo.CreateEntryFromFile(ruta, $"{prefijo}/{relativa}", CompressionLevel.Optimal);
            }
            catch (IOException ex)
            {
                // Un archivo tomado por otro proceso no puede abortar la copia entera: se anota
                // y la copia sale sin el, que es mejor que no tener copia.
                RutasApp.RegistrarError($"CopiaDeSeguridad/{relativa}", ex);
            }
        }
    }

    // ------------------------------------------------------------------
    // Importar
    // ------------------------------------------------------------------

    /// <summary>
    /// Lee el manifiesto sin tocar nada del disco. Es lo que permite mostrar en la
    /// confirmacion QUE se va a restaurar, y lo que detecta un archivo que no es una copia
    /// de AutoExam antes de haber borrado un solo dato del usuario.
    /// </summary>
    public static ResumenDeCopia Inspeccionar(string origen)
    {
        try
        {
            using var archivo = ZipFile.OpenRead(origen);

            var entrada = archivo.GetEntry(Manifiesto)
                ?? throw new CopiaInvalidaException(
                    "El archivo no es una copia de seguridad de AutoExam: le falta el manifiesto interno.");

            using var flujo = entrada.Open();

            return JsonSerializer.Deserialize<ResumenDeCopia>(flujo)
                ?? throw new CopiaInvalidaException("La copia esta danada: el manifiesto interno no se pudo leer.");
        }
        catch (CopiaInvalidaException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw new CopiaInvalidaException(
                "El archivo no es un ZIP valido. Puede estar corrupto o haberse cortado al copiarlo.");
        }
        catch (JsonException)
        {
            throw new CopiaInvalidaException("La copia esta danada: el manifiesto interno no se pudo leer.");
        }
    }

    /// <summary>
    /// Reemplaza los datos actuales por los de la copia. Valida primero
    /// (<see cref="Inspeccionar"/>) y recien despues borra: un archivo invalido tiene que
    /// dejar la biblioteca del usuario exactamente como estaba.
    /// </summary>
    public static ResumenDeCopia Importar(string origen)
    {
        var resumen = Inspeccionar(origen);

        using var archivo = ZipFile.OpenRead(origen);

        // Segunda validacion, esta sobre las rutas: una entrada con ".." o con ruta absoluta
        // escribiria fuera de la carpeta de datos. Se comprueba ANTES de borrar nada, para que
        // un archivo armado a mano no pueda dejar la app a medio restaurar.
        foreach (var entrada in archivo.Entries)
        {
            if (DestinoDe(entrada.FullName) is null && entrada.FullName != Manifiesto)
            {
                throw new CopiaInvalidaException(
                    $"La copia contiene una ruta que apunta fuera de la carpeta de datos ({entrada.FullName}).");
            }
        }

        VaciarDatos();
        RutasApp.AsegurarCarpetas();

        foreach (var entrada in archivo.Entries)
        {
            if (entrada.FullName == Manifiesto || entrada.Length == 0 && entrada.Name.Length == 0)
            {
                continue;
            }

            string? destino = DestinoDe(entrada.FullName);

            if (destino is null)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            entrada.ExtractToFile(destino, overwrite: true);
        }

        return resumen;
    }

    /// <summary>
    /// Ruta absoluta donde va una entrada, o null si cae fuera de la carpeta de datos.
    /// La comparacion es sobre la ruta ya normalizada por el sistema, no sobre el texto: es
    /// lo unico que atrapa tanto "..\\.." como una ruta absoluta como "C:\\Windows\\...".
    /// </summary>
    private static string? DestinoDe(string entrada)
    {
        try
        {
            string raiz = Path.GetFullPath(RutasApp.Raiz);
            string completa = Path.GetFullPath(Path.Combine(raiz, entrada));

            bool adentro = completa.StartsWith(
                raiz.EndsWith(Path.DirectorySeparatorChar) ? raiz : raiz + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

            return adentro ? completa : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Borra lo que la copia va a reemplazar, y solo eso: si quedaran los libros anteriores,
    /// importar seria mezclar dos bibliotecas en vez de restaurar una.
    /// </summary>
    private static void VaciarDatos()
    {
        foreach (string carpeta in Carpetas)
        {
            string ruta = Path.Combine(RutasApp.Raiz, carpeta);

            if (Directory.Exists(ruta))
            {
                Directory.Delete(ruta, recursive: true);
            }
        }

        foreach (string nombre in Archivos)
        {
            string ruta = Path.Combine(RutasApp.Raiz, nombre);

            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
    }
}
