using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AutoExam.Tests.Scripts;

/// <summary>
/// Infraestructura de invocación de <c>Verificar-Version.ps1</c> como proceso hijo
/// (specs/03-architecture.md Incremento 2 §1.1/§3.1; specs/team-roster.yaml,
/// test-dev-verificacion-version). El script puede no existir todavía en el working tree —
/// esto es trabajo contract-first (ver notas del roster): en ese caso <see cref="Ejecutar"/>
/// lanza <see cref="Win32Exception"/> con un mensaje claro de "archivo no encontrado", no
/// falla silenciosamente.
/// </summary>
internal static class VerificarVersionProceso
{
    /// <summary>
    /// Raíz del repo, resuelta desde la ubicación de este archivo fuente (no del directorio de
    /// salida del build) para que <c>dotnet test</c> funcione sin importar el cwd. Este archivo
    /// vive en <c>&lt;raiz&gt;/AutoExam.Tests/Scripts/</c>.
    /// </summary>
    public static readonly string RaizRepo = ResolverRaizRepo();

    /// <summary>Ruta esperada de Verificar-Version.ps1, junto a publicar.ps1 (§3.1).</summary>
    public static readonly string RutaScript = Path.Combine(RaizRepo, "Verificar-Version.ps1");

    /// <summary>
    /// Interprete de PowerShell con el que se lanza el script.
    ///
    /// Se prefiere <c>pwsh</c> (PowerShell 7) porque es el que usa el pipeline, y asi la suite
    /// mide exactamente lo mismo que corre en CI. Pero pwsh NO viene con Windows: sin
    /// alternativa, estos 22 tests fallan enteros en cualquier maquina que solo tenga Windows
    /// PowerShell, y eso deja al desarrollador sin poder reproducir localmente lo que falla en
    /// CI, que es justo cuando mas falta hace.
    /// </summary>
    private static readonly string Interprete = ResolverInterprete();

    private static string ResolverInterprete()
    {
        foreach (string candidato in new[] { "pwsh", "powershell" })
        {
            if (EstaEnElPath(candidato))
            {
                return candidato;
            }
        }

        // Ninguno: se devuelve pwsh para que el fallo sea el mensaje original y explicito
        // ("no se encuentra el archivo"), en vez de un error raro mas adelante.
        return "pwsh";
    }

    private static bool EstaEnElPath(string ejecutable)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (string directorio in path.Split(Path.PathSeparator))
        {
            try
            {
                if (File.Exists(Path.Combine(directorio, ejecutable + ".exe")))
                {
                    return true;
                }
            }
            catch
            {
                // Una entrada invalida del PATH no puede tumbar la resolucion.
            }
        }

        return false;
    }

    private static string ResolverRaizRepo([CallerFilePath] string archivoFuente = "")
    {
        string directorioScripts = Path.GetDirectoryName(archivoFuente)!;          // .../AutoExam.Tests/Scripts
        string directorioTests = Directory.GetParent(directorioScripts)!.FullName; // .../AutoExam.Tests
        return Directory.GetParent(directorioTests)!.FullName;                     // raíz del repo
    }

    public sealed record Resultado(int CodigoSalida, string Stdout, string Stderr);

    /// <param name="argumentos">
    /// Parámetros posicionales/switch del script (p.ej. "-CsprojPath", ruta, "-ManifiestoPath",
    /// ruta, "-EmitGithubOutput"), pasados vía <see cref="ProcessStartInfo.ArgumentList"/> para
    /// que .NET arme el quoting correcto sin depender de si la ruta temporal tiene espacios.
    /// </param>
    /// <param name="directorioTrabajo">
    /// Cwd del proceso hijo. Se fija a un directorio DISTINTO de la raíz del repo por defecto
    /// para probar que la resolución de rutas por default del script depende de $PSScriptRoot,
    /// no del cwd (contrato §3.1: los parámetros por default usan Join-Path $PSScriptRoot ...).
    /// </param>
    /// <param name="githubOutputPath">
    /// Si no es null, se expone como $env:GITHUB_OUTPUT al proceso hijo (mismo mecanismo que usa
    /// GitHub Actions). Si es null, se remueve explícitamente de las variables heredadas para que
    /// el test de "no side effects" no dependa de si la máquina que corre la suite ya tiene esa
    /// variable seteada en el entorno.
    /// </param>
    public static Resultado Ejecutar(
        IEnumerable<string> argumentos,
        string? directorioTrabajo = null,
        string? githubOutputPath = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Interprete,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // PowerShell 7 (pwsh) escribe stdout en UTF-8; se fija explícito para no depender
            // de la code page heredada de la consola que dispara `dotnet test` (riesgo real: el
            // mensaje de contrato usa em dash "—" y "publicación"/"automática" con tilde).
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = directorioTrabajo ?? Path.GetTempPath(),
        };

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(RutaScript);
        foreach (string argumento in argumentos)
        {
            psi.ArgumentList.Add(argumento);
        }

        if (githubOutputPath is not null)
        {
            psi.Environment["GITHUB_OUTPUT"] = githubOutputPath;
        }
        else
        {
            psi.Environment.Remove("GITHUB_OUTPUT");
        }

        using Process proceso = Process.Start(psi)
            ?? throw new InvalidOperationException("No se pudo iniciar pwsh para Verificar-Version.ps1.");

        string stdout = proceso.StandardOutput.ReadToEnd();
        string stderr = proceso.StandardError.ReadToEnd();
        proceso.WaitForExit();

        return new Resultado(proceso.ExitCode, stdout, stderr);
    }
}

/// <summary>
/// Directorio temporal descartable para un único test, con limpieza best-effort en
/// <see cref="Dispose"/> (mismo criterio que <c>RutasAisladasFixture</c>). Cada test que
/// invoca el script arma su propio par AutoExam.csproj/update.xml acá adentro — nunca contra
/// los archivos reales del repo, para poder correr en paralelo sin interferencia (R-7,
/// specs/03-architecture.md Incremento 2 §5).
/// </summary>
internal sealed class DirectorioTemporal : IDisposable
{
    public string Ruta { get; }

    public DirectorioTemporal()
    {
        Ruta = Path.Combine(Path.GetTempPath(), "AutoExam.Tests.VerificarVersion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Ruta);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Ruta))
            {
                Directory.Delete(Ruta, recursive: true);
            }
        }
        catch
        {
            // Limpieza best-effort: no puede tumbar la corrida de tests.
        }
    }
}

/// <summary>
/// Arma pares de fixture AutoExam.csproj/update.xml con la misma forma que los archivos reales
/// del repo (lo suficiente para que el parseo XML/regex del script funcione tal cual lo hace
/// contra los archivos reales — ver 02-tech-spec.md, "Estado real del código").
/// </summary>
internal static class FixtureVersion
{
    public static (string CsprojPath, string ManifiestoPath) Crear(
        string directorio, string versionCsproj, string versionManifiesto)
    {
        Directory.CreateDirectory(directorio);
        string csprojPath = Path.Combine(directorio, "AutoExam.csproj");
        string manifiestoPath = Path.Combine(directorio, "update.xml");

        File.WriteAllText(csprojPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0-windows</TargetFramework>
                <Version>{versionCsproj}</Version>
              </PropertyGroup>
            </Project>
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        File.WriteAllText(manifiestoPath, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <item>
                <version>{versionManifiesto}</version>
                <url>https://github.com/MatiVillalba015/AutoExam/releases/download/v{versionManifiesto}/AutoExam-v{versionManifiesto}.zip</url>
                <changelog>https://github.com/MatiVillalba015/AutoExam/releases/tag/v{versionManifiesto}</changelog>
                <mandatory>false</mandatory>
            </item>
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return (csprojPath, manifiestoPath);
    }

    /// <summary>Csproj deliberadamente roto: sin la etiqueta &lt;Version&gt; (para AC-T "error").</summary>
    public static string CrearCsprojSinVersion(string directorio)
    {
        Directory.CreateDirectory(directorio);
        string csprojPath = Path.Combine(directorio, "AutoExam.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0-windows</TargetFramework>
              </PropertyGroup>
            </Project>
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return csprojPath;
    }

    /// <summary>Manifiesto deliberadamente roto: sin la etiqueta &lt;version&gt;.</summary>
    public static string CrearManifiestoSinVersion(string directorio)
    {
        Directory.CreateDirectory(directorio);
        string manifiestoPath = Path.Combine(directorio, "update.xml");
        File.WriteAllText(manifiestoPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <item>
                <url>https://github.com/MatiVillalba015/AutoExam/releases/download/v1.0.0/AutoExam-v1.0.0.zip</url>
                <mandatory>false</mandatory>
            </item>
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return manifiestoPath;
    }
}
