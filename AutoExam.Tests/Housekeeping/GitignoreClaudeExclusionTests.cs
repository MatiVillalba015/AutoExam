using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoExam.Tests.Housekeeping;

/// <summary>
/// Contrato de US-010 (housekeeping de repo) — specs/03-architecture.md Incremento 3 §4.2,
/// specs/02-tech-spec.md Incremento 3 AC-T32, NFR-29/NFR-30. specs/team-roster.yaml,
/// test-dev-housekeeping-repo.
///
/// Alcance deliberado (contrato ya cerrado, no a criterio de este test): solo se verifica que
/// <c>.gitignore</c> excluye <c>.claude/</c> de forma repetible (NFR-30), invocando
/// <c>git check-ignore -v</c> / <c>git status --porcelain</c> como proceso hijo — mismo patrón
/// que ya usa <c>AutoExam.Tests/Scripts/VerificarVersionProceso.cs</c> (increment 2). La
/// búsqueda amplia de menciones a "claude"/"anthropic" en el árbol (AC-T33) es responsabilidad
/// puntual de devops-housekeeping-repo, no un test permanente de CI (specs/team-roster.yaml,
/// notas de test-dev-housekeeping-repo). El gate de dotnet build/dotnet test (NFR-31) ya lo
/// cubre el pipeline existente (increment 1) — no se duplica acá.
///
/// Trabajo contract-first: si <c>.gitignore</c> todavía no tiene la línea corregida (en curso
/// en paralelo por devops-housekeeping-repo), esta suite falla con una aserción clara en vez de
/// saltarse silenciosamente — cumple el DoD del rol ("no dependen de pasos manuales").
/// </summary>
[Trait("Categoria", "Proceso")]
public class GitignoreClaudeExclusionTests
{
    // ------------------------------------------------------------------
    // NFR-30 / AC-T32 — git check-ignore -v .claude devuelve una regla real (no vacío, no
    // error), no la línea corrupta que existía antes de la corrección.
    // ------------------------------------------------------------------

    [Fact]
    public void ClaudeDirectory_EstaIgnorado_CheckIgnoreDevuelveReglaRealDeGitignore()
    {
        var r = GitProceso.Ejecutar("check-ignore", "-v", ".claude");

        Assert.True(r.CodigoSalida == 0,
            $"'.claude' debería estar ignorado por .gitignore (exit code 0). " +
            $"Exit: {r.CodigoSalida}\nStdout: {r.Stdout}\nStderr: {r.Stderr}");

        // Formato de 'git check-ignore -v': "<archivo>:<linea>:<patron>\t<ruta>". Se exige que
        // la regla venga de .gitignore (no de un .git/info/exclude ni de una regla global ajena
        // al repo) y que el patrón sea el esperado por el contrato (§4.2): entrada '.claude/'.
        Assert.Matches(new Regex(@"^\.gitignore:\d+:\.claude/?\t"), r.Stdout);
    }

    [Fact]
    public void ClaudeDirectory_NoApareceEnGitStatusPorcelain()
    {
        // '.claude/skills' tiene contenido real (symlinks) en este checkout — si la exclusión
        // no funcionara, 'git status --porcelain' listaría entradas '??' para ese contenido.
        var r = GitProceso.Ejecutar("status", "--porcelain", "--", ".claude");

        Assert.Equal(string.Empty, r.Stdout.Trim());
    }

    // ------------------------------------------------------------------
    // Control negativo — confirma que el harness realmente distingue "ignorado" de "no
    // ignorado" (si este test fallara, los dos de arriba no serían confiables).
    // ------------------------------------------------------------------

    [Fact]
    public void ArchivoVitalDelRepo_NoEstaIgnorado_CheckIgnoreDevuelveExitCode1()
    {
        var r = GitProceso.Ejecutar("check-ignore", "-v", "AutoExam/AutoExam.csproj");

        Assert.Equal(1, r.CodigoSalida);
        Assert.Equal(string.Empty, r.Stdout.Trim());
    }

    // ------------------------------------------------------------------
    // NFR-30 — "no la línea corrupta actual": la entrada de .gitignore es texto UTF-8 válido,
    // sin el caracter de reemplazo Unicode que delataba la corrupción original.
    // ------------------------------------------------------------------

    [Fact]
    public void Gitignore_EntradaClaudeEsUtf8ValidoSinCaracterDeReemplazo()
    {
        string ruta = Path.Combine(GitProceso.RaizRepo, ".gitignore");
        string contenido = File.ReadAllText(ruta, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false));

        Assert.DoesNotContain('�', contenido);
        Assert.Contains(".claude/", contenido);
    }
}
