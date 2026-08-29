<#
.SYNOPSIS
    Chequea si <Version> de AutoExam.csproj supera la version publicada en update.xml.

.DESCRIPTION
    Es informativo, nunca invasivo: no escribe archivos, no hace git add/commit/push, no
    invoca publicar.ps1 ni gh. Corre 100% local (sin red) leyendo los dos archivos del
    propio checkout. Reusa exactamente la misma comparacion que hoy corre dentro del step
    "Comparar version del proyecto vs. update.xml" de .github/workflows/publish.yml -- este
    script ES esa logica, factorizada para poder correrla a mano antes de pushear
    (specs/03-architecture.md, Incremento 2, §1.2/§3.1).

    Con -EmitGithubOutput (uso exclusivo del step de publish.yml) ademas escribe a
    $env:GITHUB_OUTPUT las 4 lineas que el resto del pipeline consume via
    steps.version.outputs.* (version, tag, zip, should_publish). En uso local (sin el
    switch) no se toca GITHUB_OUTPUT ni ningun otro archivo.

.EXAMPLE
    .\Verificar-Version.ps1
    Corrida local, antes de pushear: imprime si esta version dispararia una publicacion.

.EXAMPLE
    .\Verificar-Version.ps1 -CsprojPath fixtures\AutoExam.csproj -ManifiestoPath fixtures\update.xml
    Corrida contra un par de archivos de fixture (uso desde tests).
#>

param(
    [string]$CsprojPath     = (Join-Path $PSScriptRoot 'AutoExam/AutoExam.csproj'),
    [string]$ManifiestoPath = (Join-Path $PSScriptRoot 'update.xml'),
    # Uso exclusivo del step de publish.yml; se omite en uso local.
    [switch]$EmitGithubOutput
)

# El texto de salida lleva tildes y un em dash. pwsh escribe UTF-8 por defecto, pero Windows
# PowerShell usa la code page de la consola y los mutila cuando la salida esta redirigida, que
# es como la leen los tests y el pipeline. Fijarlo aca hace que el script diga lo mismo sin
# importar con cual de los dos se lo invoque.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

function Escribir($texto, $color = 'Gray') { Write-Host $texto -ForegroundColor $color }

# Codigos de salida: 0 = supera (publicaria), 1 = no supera (informativo, no es error),
# 2 = error de lectura (archivo faltante o version no parseable).
try {
    if (-not (Test-Path $CsprojPath)) {
        throw "No se encontro el csproj en $CsprojPath."
    }
    [xml]$csprojXml = Get-Content $CsprojPath
    $version = ($csprojXml.Project.PropertyGroup.Version | Where-Object { $_ }) -join ''
    $version = "$version".Trim()
    if (-not $version) { throw "No se pudo leer <Version> de $CsprojPath." }

    if (-not (Test-Path $ManifiestoPath)) {
        throw "No se encontro el manifiesto en $ManifiestoPath."
    }
    $manifiesto = Get-Content $ManifiestoPath -Raw
    if ($manifiesto -notmatch '<version>([^<]+)</version>') {
        throw "No se pudo leer <version> de $ManifiestoPath."
    }
    $versionPublicada = $Matches[1].Trim()

    $versionParseada   = [version]$version
    $publicadaParseada = [version]$versionPublicada
} catch {
    Escribir "Error: $($_.Exception.Message)" 'Red'
    exit 2
}

$esMayor = $versionParseada -gt $publicadaParseada

if ($esMayor) {
    Escribir "$version ($CsprojPath) supera la publicada ($ManifiestoPath) — este push SI va a disparar la publicación automática (US-001)." 'Green'
} else {
    Escribir "$version ($CsprojPath) NO supera la publicada ($ManifiestoPath) — este push NO va a disparar ninguna publicación nueva." 'Yellow'
}

if ($EmitGithubOutput) {
    try {
        if (-not $env:GITHUB_OUTPUT) {
            throw "EmitGithubOutput requiere la variable de entorno GITHUB_OUTPUT (disponible solo en GitHub Actions)."
        }
        Add-Content -Path $env:GITHUB_OUTPUT -Value "version=$version"
        Add-Content -Path $env:GITHUB_OUTPUT -Value "tag=v$version"
        Add-Content -Path $env:GITHUB_OUTPUT -Value "zip=AutoExam-v$version.zip"
        Add-Content -Path $env:GITHUB_OUTPUT -Value "should_publish=$($esMayor.ToString().ToLower())"
    } catch {
        Escribir "Error: $($_.Exception.Message)" 'Red'
        exit 2
    }
}

exit $(if ($esMayor) { 0 } else { 1 })
