using System.Diagnostics;
using System.Text;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Housekeeping;

/// <summary>
/// Infraestructura de invocación de <c>git</c> como proceso hijo — specs/03-architecture.md
/// Incremento 3 §4.2 (US-010), specs/02-tech-spec.md Incremento 3 AC-T32, NFR-29/NFR-30.
/// specs/team-roster.yaml, test-dev-housekeeping-repo: "mismo patrón que
/// test-dev-verificacion-version del incremento 2" (ver AutoExam.Tests/Scripts/
/// VerificarVersionProceso.cs) — invoca el binario real, no reimplementa la lógica de git.
///
/// A diferencia del script de versión, acá no se usan fixtures aisladas: el contrato de
/// NFR-30 es exactamente "cómo se comporta el .gitignore de ESTE repo" (§4.2: "Verificación del
/// contrato: git check-ignore -v .claude ... y git status --porcelain ..."), así que estos tests
/// corren contra el working tree real del checkout, con cwd = raíz del repo (resuelta igual que
/// el resto de la suite, ver <see cref="ArchivoFuenteHelper"/>).
/// </summary>
internal static class GitProceso
{
    public static readonly string RaizRepo = ArchivoFuenteHelper.RaizRepo();

    public sealed record Resultado(int CodigoSalida, string Stdout, string Stderr);

    public static Resultado Ejecutar(params string[] argumentos)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = RaizRepo,
        };

        foreach (string argumento in argumentos)
        {
            psi.ArgumentList.Add(argumento);
        }

        using Process proceso = Process.Start(psi)
            ?? throw new InvalidOperationException("No se pudo iniciar git.");

        string stdout = proceso.StandardOutput.ReadToEnd();
        string stderr = proceso.StandardError.ReadToEnd();
        proceso.WaitForExit();

        return new Resultado(proceso.ExitCode, stdout, stderr);
    }
}
