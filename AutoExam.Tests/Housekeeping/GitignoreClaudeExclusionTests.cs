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
/// <c>.gitignore</c> excluye <c>.claude/</c> de forma repetible (NFR-30), leyendo el archivo y
/// consultando <c>git check-ignore -v</c> sobre una ruta interna. Ninguna de las dos cosas
/// depende de que el directorio exista en el disco: no existe en el checkout de CI, porque
/// justamente está ignorado, y esa dependencia hacía fallar la suite ahí y pasar en local. La
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
    public void Gitignore_TieneUnaEntradaActivaQueExcluyeElDirectorioClaude()
    {
        // Se lee .gitignore directamente en vez de preguntarle a 'git check-ignore .claude'.
        //
        // Por qué: el patrón del contrato es '.claude/', y en gitignore una barra final matchea
        // SOLO directorios. Si la carpeta no está en el disco, git no puede saber que la ruta
        // es un directorio y el patrón no matchea: 'check-ignore' devuelve exit 1 y no imprime
        // nada. Eso hacía que el test pasara en una máquina de desarrollo (donde .claude/
        // existe) y fallara en GitHub Actions (donde el checkout no la trae, porque
        // justamente está ignorada). El test medía el sistema de archivos del runner, no el
        // contenido del .gitignore, que es lo único que el contrato §4.2 fija.
        var entradas = EntradasEfectivas();

        var exclusiones = entradas
            .Where(e => e.Patron is ".claude/" or ".claude")
            .ToList();

        Assert.True(exclusiones.Count > 0,
            "No hay ninguna entrada activa que excluya '.claude/' en .gitignore. " +
            "Entradas leídas: " + string.Join(", ", entradas.Select(e => $"{e.Numero}:{e.Patron}")));

        // Una negación posterior volvería a incluir el directorio y dejaría la exclusión sin
        // efecto, aunque la línea de arriba siga estando.
        var negaciones = entradas
            .Where(e => e.Patron is "!.claude/" or "!.claude")
            .ToList();

        Assert.True(negaciones.Count == 0,
            "Hay una negación que reactiva '.claude/' en .gitignore, línea " +
            string.Join(", ", negaciones.Select(e => e.Numero)) + ".");
    }

    /// <summary>
    /// Líneas de .gitignore que git realmente evalúa: sin vacías y sin comentarios. Se
    /// conserva el número de línea para que un fallo diga dónde mirar.
    /// </summary>
    private static List<(int Numero, string Patron)> EntradasEfectivas()
    {
        string ruta = Path.Combine(GitProceso.RaizRepo, ".gitignore");

        return File.ReadAllLines(ruta)
            .Select((texto, indice) => (Numero: indice + 1, Patron: texto.Trim()))
            .Where(e => e.Patron.Length > 0 && !e.Patron.StartsWith('#'))
            .ToList();
    }

    [Fact]
    public void GitAplicaLaRegla_SobreUnaRutaDentroDeClaude()
    {
        // Complementa al test de arriba: aquel lee el archivo, este comprueba que git de verdad
        // aplique la regla. Se pregunta por una ruta DENTRO del directorio y no por el
        // directorio mismo, porque un archivo bajo '.claude/' matchea el patrón exista o no en
        // el disco — a diferencia de '.claude' a secas, que necesita existir para que git
        // sepa que es un directorio.
        //
        // Antes acá se corría 'git status --porcelain -- .claude', que sin la carpeta devuelve
        // vacío y por lo tanto pasaba sin comprobar nada en CI.
        var r = GitProceso.Ejecutar("check-ignore", "-v", ".claude/settings.json");

        Assert.True(r.CodigoSalida == 0,
            "Una ruta dentro de '.claude/' debería estar ignorada (exit code 0). " +
            $"Exit: {r.CodigoSalida}\nStdout: {r.Stdout}\nStderr: {r.Stderr}");

        // Formato de 'git check-ignore -v': "<archivo>:<linea>:<patron>\t<ruta>". Se exige que
        // la regla venga de .gitignore y no de un .git/info/exclude o una config global, que
        // son locales de una máquina y no viajan con el repositorio.
        Assert.Matches(new Regex(@"^\.gitignore:\d+:\.claude/?\t"), r.Stdout);
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
