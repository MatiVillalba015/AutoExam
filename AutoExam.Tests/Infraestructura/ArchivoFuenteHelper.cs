using System.IO;

namespace AutoExam.Tests.Infraestructura;

/// <summary>
/// Ubica archivos fuente del checkout (XAML, principalmente) para los tests estructurales que
/// los parsean directo con <c>System.Xml.Linq</c>, sin levantar runtime WPF — ver
/// team-roster.yaml, <c>test-dev-animaciones-shell</c> (US-007/US-008, contrato en
/// specs/03-architecture.md Incremento 2 §3.4).
///
/// Camina hacia arriba desde <see cref="AppContext.BaseDirectory"/> (la carpeta de salida del
/// test, p.ej. <c>AutoExam.Tests/bin/Debug/net8.0-windows</c>) hasta encontrar
/// <c>AutoExam.sln</c>, que vive en la raiz del repo — no depende de que el working directory
/// del proceso de test sea uno en particular (distinto entre <c>dotnet test</c> local y el
/// runner de CI).
/// </summary>
public static class ArchivoFuenteHelper
{
    private static string? _raizRepo;

    public static string RaizRepo()
    {
        if (_raizRepo is not null)
        {
            return _raizRepo;
        }

        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "AutoExam.sln")))
        {
            directorio = directorio.Parent;
        }

        if (directorio is null)
        {
            throw new InvalidOperationException(
                $"No se encontro AutoExam.sln subiendo desde '{AppContext.BaseDirectory}' — " +
                "¿se movio el proyecto de test fuera del checkout del repo?");
        }

        _raizRepo = directorio.FullName;
        return _raizRepo;
    }

    /// <param name="rutaRelativaAlRepo">Ruta relativa a la raiz del repo, con '/' como
    /// separador (p.ej. "AutoExam/Theme/Estilos.xaml").</param>
    public static string RutaFuente(string rutaRelativaAlRepo)
    {
        var ruta = Path.Combine(RaizRepo(), rutaRelativaAlRepo.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(ruta))
        {
            throw new FileNotFoundException(
                $"No se encontro el archivo fuente esperado en '{ruta}'.", ruta);
        }

        return ruta;
    }
}
