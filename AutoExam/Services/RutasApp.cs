using System.IO;
namespace AutoExam.Services;

/// <summary>Rutas de persistencia bajo %LOCALAPPDATA%\AppEstudioUBA.</summary>
public static class RutasApp
{
    public const string NombreCarpeta = "AppEstudioUBA";

    /// <summary>
    /// Carpeta de datos. Por defecto %LOCALAPPDATA%\AppEstudioUBA; si existe la
    /// variable de entorno AUTOEXAM_DATOS se usa esa, lo que permite llevar la
    /// biblioteca en un pendrive o probar el .exe sin tocar los datos de siempre.
    /// </summary>
    public static string Raiz { get; private set; } = RaizInicial();

    private static string RaizInicial()
    {
        string? propia = Environment.GetEnvironmentVariable("AUTOEXAM_DATOS");

        return string.IsNullOrWhiteSpace(propia)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), NombreCarpeta)
            : propia.Trim();
    }

    /// <summary>
    /// Manda todos los datos a otra carpeta. Existe para que las pruebas corran
    /// contra un directorio descartable: sin esta puerta, cualquier prueba que
    /// llegue a un guardado escribe sobre la biblioteca real del usuario.
    /// La app nunca la llama.
    /// </summary>
    public static void RedirigirRaiz(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            throw new ArgumentException("La raiz de datos no puede quedar vacia.", nameof(ruta));
        }

        Raiz = ruta;
        AsegurarCarpetas();
    }

    public static string Biblioteca => Path.Combine(Raiz, "Biblioteca");

    public static string Imagenes => Path.Combine(Raiz, "Imagenes");

    public static string ArchivoLibros => Path.Combine(Raiz, "libros.json");

    public static string ArchivoPerfil => Path.Combine(Raiz, "perfil.json");

    public static string ArchivoConfig => Path.Combine(Raiz, "config.json");

    public static string ArchivoLog => Path.Combine(Raiz, "errores.log");

    public static void AsegurarCarpetas()
    {
        Directory.CreateDirectory(Raiz);
        Directory.CreateDirectory(Biblioteca);
        Directory.CreateDirectory(Imagenes);
    }

    /// <summary>Carpeta de imagenes de un examen puntual.</summary>
    public static string CarpetaImagenesExamen(string examenId)
    {
        string ruta = Path.Combine(Imagenes, examenId);
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    /// <summary>Borra carpetas de imagenes de examenes viejos para que AppData no crezca sin control.</summary>
    public static void LimpiarImagenesAntiguas(int diasDeVida = 7)
    {
        try
        {
            if (!Directory.Exists(Imagenes))
            {
                return;
            }

            var limite = DateTime.Now.AddDays(-diasDeVida);
            foreach (var dir in Directory.GetDirectories(Imagenes))
            {
                if (Directory.GetCreationTime(dir) < limite)
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
        catch
        {
            // Limpieza best-effort: nunca debe romper el arranque.
        }
    }

    public static void RegistrarError(string contexto, Exception ex)
    {
        try
        {
            AsegurarCarpetas();
            File.AppendAllText(ArchivoLog,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {contexto}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Ignorado a proposito.
        }
    }
}
