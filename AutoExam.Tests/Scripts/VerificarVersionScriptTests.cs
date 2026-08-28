using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace AutoExam.Tests.Scripts;

/// <summary>
/// Contrato de <c>Verificar-Version.ps1</c> — specs/03-architecture.md Incremento 2 §3.1,
/// specs/02-tech-spec.md Incremento 2 AC-T15/AC-T16/AC-T17/AC-T18, NFR-14/NFR-15/NFR-16.
/// specs/team-roster.yaml, test-dev-verificacion-version.
///
/// Trabajo contract-first: el script puede no existir todavía o estar en curso por
/// devops-verificacion-version en paralelo. Estos tests están escritos contra el formato de
/// mensajes/exit codes/outputs ya cerrado en la arquitectura, no contra una implementación
/// inspeccionada a mano — si el script no existe, la suite entera falla con un error claro de
/// "no se pudo iniciar pwsh"/"archivo no encontrado", no se salta silenciosamente (ver
/// DoD del rol: "no dependen de pasos manuales").
///
/// Cada test usa su propio <see cref="DirectorioTemporal"/> con un par
/// AutoExam.csproj/update.xml de fixture — nunca los archivos reales del repo (R-7).
///
/// Las aserciones de stdout usan <c>Assert.Contains</c> (no igualdad total de toda la salida):
/// el contrato fija el texto EXACTO de la línea de resultado, pero no prohíbe que el script
/// emita diagnóstico adicional (p.ej. un Write-Host de depuración) — igual que ya hace hoy el
/// bloque inline equivalente de publish.yml (Write-Host "csproj: ... | update.xml: ... |
/// publicar: ..."). Sobre-restringir a igualdad total acoplaría el test a un detalle no
/// especificado por el contrato.
/// </summary>
[Trait("Categoria", "Proceso")]
public class VerificarVersionScriptTests
{
    // Plantillas literales del contrato (specs/03-architecture.md Incremento 2 §3.1). Los tres
    // placeholders son: {0} = <Version> leída del csproj, {1} = valor de -CsprojPath tal como se
    // lo pasa el test, {2} = valor de -ManifiestoPath tal como se lo pasa el test — se asume que
    // el script los interpola literal, sin normalizar separadores de ruta (mismo criterio que ya
    // usa el resto del repo en PowerShell, p.ej. $env:CSPROJ en publish.yml). Documentado como
    // supuesto porque el contrato no aclara si se resuelve a ruta absoluta/canónica.
    private const string PlantillaNoSupera =
        "{0} ({1}) NO supera la publicada ({2}) — este push NO va a disparar ninguna publicación nueva.";

    private const string PlantillaSupera =
        "{0} ({1}) supera la publicada ({2}) — este push SI va a disparar la publicación automática (US-001).";

    private static string MensajeNoSupera(string version, string csprojPath, string manifiestoPath) =>
        string.Format(PlantillaNoSupera, version, csprojPath, manifiestoPath);

    private static string MensajeSupera(string version, string csprojPath, string manifiestoPath) =>
        string.Format(PlantillaSupera, version, csprojPath, manifiestoPath);

    // ------------------------------------------------------------------
    // AC-T15 — no supera la publicada: mensaje + exit code 1
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("1.0.1", "1.0.2")] // csproj por debajo de la publicada
    [InlineData("1.0.2", "1.0.2")] // exactamente igual — NO es "mayor", NFR-03/increment 1 usa -gt estricto
    [InlineData("1.9.0", "1.10.0")] // comparación semántica, no lexicográfica: "1.9" < "1.10" en [version]
    public void VersionNoSupera_ExitCode1YMensajeExacto(string versionCsproj, string versionPublicada)
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, versionCsproj, versionPublicada);

        var r = VerificarVersionProceso.Ejecutar(new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(1, r.CodigoSalida);
        Assert.Contains(MensajeNoSupera(versionCsproj, csproj, manifiesto), r.Stdout);
    }

    // ------------------------------------------------------------------
    // AC-T16 — supera la publicada: mensaje + exit code 0
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("1.0.3", "1.0.2")] // patch simple
    [InlineData("2.0.0", "1.9.9")] // salto de major
    [InlineData("1.10.0", "1.9.0")] // idem comparación semántica de la Theory de arriba, en sentido inverso
    public void VersionSupera_ExitCode0YMensajeExacto(string versionCsproj, string versionPublicada)
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, versionCsproj, versionPublicada);

        var r = VerificarVersionProceso.Ejecutar(new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(0, r.CodigoSalida);
        Assert.Contains(MensajeSupera(versionCsproj, csproj, manifiesto), r.Stdout);
    }

    // ------------------------------------------------------------------
    // AC-T17/NFR-16 — sin side effects: no toca los archivos de fixture, no escribe
    // GITHUB_OUTPUT si no se pide, no genera archivos nuevos en el directorio de trabajo
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("1.0.1", "1.0.2")]
    [InlineData("1.0.3", "1.0.2")]
    public void Ejecucion_NoModificaLosArchivosDeFixtureNiCreaArchivosNuevos(string versionCsproj, string versionPublicada)
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, versionCsproj, versionPublicada);

        string csprojAntes = File.ReadAllText(csproj);
        string manifiestoAntes = File.ReadAllText(manifiesto);
        var archivosAntes = Directory.GetFiles(tmp.Ruta).OrderBy(f => f).ToArray();

        VerificarVersionProceso.Ejecutar(new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(csprojAntes, File.ReadAllText(csproj));
        Assert.Equal(manifiestoAntes, File.ReadAllText(manifiesto));

        var archivosDespues = Directory.GetFiles(tmp.Ruta).OrderBy(f => f).ToArray();
        Assert.Equal(archivosAntes, archivosDespues);
    }

    [Fact]
    public void SinEmitGithubOutput_NoTocaElArchivoDeGithubOutput()
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, "1.0.3", "1.0.2");

        string githubOutputPath = Path.Combine(tmp.Ruta, "github_output.txt");
        const string contenidoOriginal = "linea-preexistente-de-otro-step=x\n";
        File.WriteAllText(githubOutputPath, contenidoOriginal);

        VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto },
            tmp.Ruta,
            githubOutputPath: githubOutputPath);

        // Contrato §3.1: "Sin el switch (uso local), no se toca GITHUB_OUTPUT ni ningún otro
        // archivo." — el archivo debe seguir exactamente como estaba, ninguna línea de más.
        Assert.Equal(contenidoOriginal, File.ReadAllText(githubOutputPath));
    }

    // ------------------------------------------------------------------
    // AC-T18/NFR-15 — -EmitGithubOutput produce los mismos 4 outputs que hoy calcula inline el
    // step "Comparar version del proyecto vs. update.xml" de publish.yml (mismos nombres,
    // mismo valor, misma semántica de comparación [version] no lexicográfica)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("1.0.3", "1.0.2", true)]
    [InlineData("1.0.1", "1.0.2", false)]
    [InlineData("1.0.2", "1.0.2", false)] // igualdad -> should_publish=false, ver -gt estricto
    [InlineData("1.10.0", "1.9.0", true)] // semántico, no lexicográfico
    public void EmitGithubOutput_EscribeLosMismosCuatroOutputsQuePublishYml(
        string versionCsproj, string versionPublicada, bool esperaShouldPublish)
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, versionCsproj, versionPublicada);
        string githubOutputPath = Path.Combine(tmp.Ruta, "github_output.txt");

        var r = VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto, "-EmitGithubOutput" },
            tmp.Ruta,
            githubOutputPath: githubOutputPath);

        Assert.True(File.Exists(githubOutputPath), $"No se escribió GITHUB_OUTPUT. Stdout: {r.Stdout}\nStderr: {r.Stderr}");

        var outputs = File.ReadAllLines(githubOutputPath)
            .Where(l => l.Contains('='))
            .Select(l => l.Split('=', 2))
            .ToDictionary(kv => kv[0], kv => kv[1]);

        // Mismos 4 nombres que hoy escribe inline publish.yml (§4.1 increment 1, líneas
        // 83-86): version, tag, zip, should_publish — para no romper steps.version.outputs.*
        // consumido por los pasos 4-11.
        Assert.Equal(versionCsproj, outputs["version"]);
        Assert.Equal($"v{versionCsproj}", outputs["tag"]);
        Assert.Equal($"AutoExam-v{versionCsproj}.zip", outputs["zip"]);
        Assert.Equal(esperaShouldPublish ? "true" : "false", outputs["should_publish"]);

        // El exit code (contrato de uso interactivo) tiene que ser coherente con should_publish
        // (contrato de uso en CI) para el mismo par de versiones — es exactamente lo que exige
        // NFR-15: un único lugar define "la versión subió", consumido por los dos caminos.
        Assert.Equal(esperaShouldPublish ? 0 : 1, r.CodigoSalida);
    }

    [Fact]
    public void EmitGithubOutput_SinVariableDeEntornoGithubOutput_ExitCode2()
    {
        // Confirmado corriendo el script real (sin GITHUB_OUTPUT en el entorno, -EmitGithubOutput):
        // el script no asume que la variable existe fuera de un run real de Actions y sale con
        // el mismo código 2 que el resto de los errores de lectura, en vez de fallar con una
        // excepción no controlada o degradar en silencio a "no escribo nada". Es comportamiento
        // defensivo consistente con NFR-16 (nada de efectos colaterales silenciosos) que vale la
        // pena fijar como regresión, aunque el contrato de §3.1 no detalla este caso puntual.
        using var tmp = new DirectorioTemporal();
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, "1.0.3", "1.0.2");

        var r = VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto, "-EmitGithubOutput" },
            tmp.Ruta,
            githubOutputPath: null);

        Assert.Equal(2, r.CodigoSalida);
    }

    // ------------------------------------------------------------------
    // Errores de lectura — exit code 2 (archivo faltante o versión no parseable)
    // ------------------------------------------------------------------

    [Fact]
    public void CsprojInexistente_ExitCode2()
    {
        using var tmp = new DirectorioTemporal();
        string csprojQueNoExiste = Path.Combine(tmp.Ruta, "no-existe.csproj");
        string manifiesto = FixtureVersion.CrearManifiestoSinVersion(tmp.Ruta); // cualquier manifiesto sirve, no se llega a leerlo
        File.WriteAllText(manifiesto, "<item><version>1.0.0</version></item>");

        var r = VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csprojQueNoExiste, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(2, r.CodigoSalida);
    }

    [Fact]
    public void ManifiestoInexistente_ExitCode2()
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, _) = FixtureVersion.Crear(tmp.Ruta, "1.0.2", "1.0.1");
        string manifiestoQueNoExiste = Path.Combine(tmp.Ruta, "no-existe-update.xml");

        var r = VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiestoQueNoExiste }, tmp.Ruta);

        Assert.Equal(2, r.CodigoSalida);
    }

    [Fact]
    public void CsprojSinEtiquetaVersion_ExitCode2()
    {
        using var tmp = new DirectorioTemporal();
        string csproj = FixtureVersion.CrearCsprojSinVersion(tmp.Ruta);
        var (_, manifiesto) = FixtureVersion.Crear(tmp.Ruta, "1.0.2", "1.0.1");

        var r = VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(2, r.CodigoSalida);
    }

    [Fact]
    public void ManifiestoSinEtiquetaVersion_ExitCode2()
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, _) = FixtureVersion.Crear(tmp.Ruta, "1.0.2", "1.0.1");
        string manifiesto = FixtureVersion.CrearManifiestoSinVersion(tmp.Ruta);

        var r = VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(2, r.CodigoSalida);
    }

    [Fact]
    public void VersionNoParseableComoVersion_ExitCode2()
    {
        using var tmp = new DirectorioTemporal();
        // "no-es-una-version" no castea a [version] en PowerShell -> debe tratarse como error
        // de lectura (mismo caso que hoy hace throw en el step de publish.yml), no como una
        // comparación "NO supera" silenciosa.
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, "no-es-una-version", "1.0.2");

        var r = VerificarVersionProceso.Ejecutar(
            new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(2, r.CodigoSalida);
    }

    // ------------------------------------------------------------------
    // AC-T17 — no side effects tampoco en el caso de error: nada de git add/commit/push, no
    // invoca publicar.ps1 ni gh (verificable por ausencia de esos strings en el árbol de
    // comandos ejecutados no es posible desde afuera del proceso; se verifica lo observable:
    // ningún archivo nuevo, código de salida distinto de "colgado"/timeout).
    // ------------------------------------------------------------------

    [Fact]
    public void ErrorDeLectura_NoDejaArchivosNuevosEnElDirectorioDeFixture()
    {
        using var tmp = new DirectorioTemporal();
        string csprojQueNoExiste = Path.Combine(tmp.Ruta, "no-existe.csproj");
        string manifiesto = Path.Combine(tmp.Ruta, "no-existe-tampoco.xml");

        var archivosAntes = Directory.GetFiles(tmp.Ruta).ToArray();

        VerificarVersionProceso.Ejecutar(new[] { "-CsprojPath", csprojQueNoExiste, "-ManifiestoPath", manifiesto }, tmp.Ruta);

        Assert.Equal(archivosAntes, Directory.GetFiles(tmp.Ruta));
    }

    // ------------------------------------------------------------------
    // NFR-14 — latencia < 3 s (2 archivos locales, sin red)
    // ------------------------------------------------------------------

    [Fact]
    public void Latencia_MenorA3Segundos()
    {
        using var tmp = new DirectorioTemporal();
        var (csproj, manifiesto) = FixtureVersion.Crear(tmp.Ruta, "1.0.3", "1.0.2");

        var cronometro = Stopwatch.StartNew();
        VerificarVersionProceso.Ejecutar(new[] { "-CsprojPath", csproj, "-ManifiestoPath", manifiesto }, tmp.Ruta);
        cronometro.Stop();

        Assert.True(cronometro.ElapsedMilliseconds < 3000,
            $"NFR-14 exige < 3 s desde invocar el script hasta el mensaje de resultado; tardó {cronometro.ElapsedMilliseconds} ms.");
    }

    // ------------------------------------------------------------------
    // Parámetros por default — Join-Path $PSScriptRoot 'AutoExam/AutoExam.csproj' /
    // Join-Path $PSScriptRoot 'update.xml' (§3.1), contra los archivos REALES del repo.
    // No hardcodea números de versión (cambian con el tiempo): recalcula la expectativa leyendo
    // los mismos dos archivos con la misma lógica de comparación que ya usa publish.yml.
    // No asume el formato exacto de la ruta que el script ecoa en el mensaje (Join-Path con un
    // segundo argumento que ya trae '/' puede no normalizar separadores) — por eso NO reusa
    // MensajeSupera/MensajeNoSupera acá, solo valida versión + exit code + should_publish.
    // ------------------------------------------------------------------

    [Fact]
    public void SinParametros_UsaLosArchivosRealesDelRepoViaPSScriptRoot()
    {
        string csprojReal = Path.Combine(VerificarVersionProceso.RaizRepo, "AutoExam", "AutoExam.csproj");
        string manifiestoReal = Path.Combine(VerificarVersionProceso.RaizRepo, "update.xml");

        string versionCsproj = Regex.Match(File.ReadAllText(csprojReal), "<Version>([^<]+)</Version>").Groups[1].Value.Trim();
        string versionPublicada = Regex.Match(File.ReadAllText(manifiestoReal), "<version>([^<]+)</version>").Groups[1].Value.Trim();
        Assert.False(string.IsNullOrEmpty(versionCsproj));
        Assert.False(string.IsNullOrEmpty(versionPublicada));

        bool esperaSupera = new Version(versionCsproj) > new Version(versionPublicada);

        // Cwd deliberadamente distinto de la raíz del repo: si el script default-eara sobre el
        // cwd en vez de $PSScriptRoot, este test lo detectaría.
        using var tmp = new DirectorioTemporal();
        var r = VerificarVersionProceso.Ejecutar(Array.Empty<string>(), tmp.Ruta);

        Assert.Equal(esperaSupera ? 0 : 1, r.CodigoSalida);
        Assert.Contains(versionCsproj, r.Stdout);
        Assert.Contains(esperaSupera ? "supera la publicada" : "NO supera la publicada", r.Stdout);
    }
}
