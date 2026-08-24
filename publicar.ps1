<#
.SYNOPSIS
    Prepara y cierra la publicacion de una version de AutoExam.

.DESCRIPTION
    Publicar una version tiene tres pasos y los tres ya salieron mal alguna vez:

      1. Compilar el .exe   -> una vez se subio un binario 1.0.0 dentro de un ZIP
                               llamado v1.0.1, y la app quedo pidiendo actualizar en bucle.
      2. Subir el Release   -> se hace a mano en GitHub, y si no se hace, el manifiesto
                               anuncia un paquete que devuelve 404.
      3. Mover update.xml   -> si se mueve antes de que el Release exista, rompe a todos
                               los que ya tienen la app.

    Este script hace el 1 y el 3, y verifica el 2 antes de dejar avanzar.

.EXAMPLE
    .\publicar.ps1
    Compila, empaqueta y abre GitHub para que subas el ZIP.

.EXAMPLE
    .\publicar.ps1 -Publicar
    Despues de subir el Release: comprueba que el ZIP se descargue de verdad y recien
    entonces mueve update.xml y lo sube.
#>

[CmdletBinding()]
param(
    # Cierra la publicacion: verifica el Release y actualiza update.xml.
    [switch]$Publicar
)

$ErrorActionPreference = 'Stop'

$raiz    = $PSScriptRoot
$csproj  = Join-Path $raiz 'AutoExam\AutoExam.csproj'
$destino = Join-Path $raiz 'release'
$repo    = 'MatiVillalba015/AutoExam'

function Escribir($texto, $color = 'Gray') { Write-Host $texto -ForegroundColor $color }

# --- Version que dice el proyecto -------------------------------------------------
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ }
$version = "$version".Trim()

if (-not $version) { throw "No se pudo leer <Version> de $csproj." }

$tag = "v$version"
$zip = Join-Path $destino "AutoExam-$tag.zip"
$url = "https://github.com/$repo/releases/download/$tag/AutoExam-$tag.zip"

Escribir ""
Escribir "AutoExam $version" 'Cyan'
Escribir ("-" * 50)

# ==================================================================================
#  Cierre: el Release ya esta subido y hay que mover el manifiesto
# ==================================================================================
if ($Publicar) {
    Escribir "Comprobando que el paquete se pueda descargar..." 'Yellow'

    $codigo = 0
    try {
        $r = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 30
        $codigo = [int]$r.StatusCode
    } catch {
        if ($_.Exception.Response) { $codigo = [int]$_.Exception.Response.StatusCode }
    }

    if ($codigo -ne 200) {
        Escribir ""
        Escribir "  El paquete responde HTTP $codigo, no 200." 'Red'
        Escribir "  $url" 'DarkGray'
        Escribir ""
        Escribir "  No se toca update.xml. Si lo moviera ahora, todo el que tenga AutoExam" 'Red'
        Escribir "  recibiria un aviso de actualizacion cuya descarga le va a fallar." 'Red'
        Escribir ""
        Escribir "  Subi el Release primero: .\publicar.ps1  (sin -Publicar)" 'Yellow'
        exit 1
    }

    Escribir "  HTTP 200: el paquete esta." 'Green'

    # --- update.xml -----------------------------------------------------------
    $manifiesto = Join-Path $raiz 'update.xml'
    $xml = Get-Content $manifiesto -Raw

    $xml = [regex]::Replace($xml, '<version>[^<]+</version>', "<version>$version</version>")
    $xml = [regex]::Replace($xml, 'releases/download/v[0-9.]+/AutoExam-v[0-9.]+\.zip',
                            "releases/download/$tag/AutoExam-$tag.zip")
    $xml = [regex]::Replace($xml, 'releases/tag/v[0-9.]+', "releases/tag/$tag")

    Set-Content -Path $manifiesto -Value $xml -Encoding UTF8 -NoNewline

    Escribir "  update.xml -> $version" 'Green'

    git -C $raiz add update.xml
    git -C $raiz commit -m "update.xml: $version"
    if ($?) { git -C $raiz push }

    Escribir ""
    Escribir "Listo. Quien tenga una version anterior va a recibir la $version." 'Green'
    exit 0
}

# ==================================================================================
#  Preparacion: compilar, verificar y empaquetar
# ==================================================================================
Escribir "Compilando..." 'Yellow'

$publish = Join-Path $raiz 'AutoExam\bin\Release\net8.0-windows\win-x64\publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish $csproj -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion." }

$exe = Join-Path $publish 'AutoExam.exe'

# --- La verificacion que evita el bucle infinito ----------------------------------
# AutoUpdater compara el manifiesto contra la version del ENSAMBLADO, no contra el
# nombre del archivo ni contra el tag. Un ZIP llamado v1.0.2 con un binario que dice
# 1.0.1 adentro se instala, reinicia, se ve desactualizado y vuelve a pedir la
# actualizacion. Sin salida. Ya paso una vez.
$fileVersion = (Get-Item $exe).VersionInfo.FileVersion
$esperada    = ([version]$version)
$real        = ([version]$fileVersion)

Escribir "  binario compilado: $fileVersion"

if ($real.Major -ne $esperada.Major -or $real.Minor -ne $esperada.Minor -or $real.Build -ne $esperada.Build) {
    Escribir ""
    Escribir "  El binario dice $fileVersion pero el proyecto dice $version." 'Red'
    Escribir "  Publicarlo asi deja la app pidiendo actualizar en bucle." 'Red'
    exit 1
}

Escribir "  coincide con <Version>." 'Green'

# --- Empaquetado ------------------------------------------------------------------
# Un ZIP, no el .exe suelto: AutoUpdater trata un .exe como instalador y terminaria
# abriendo una segunda copia de AutoExam en vez de reemplazar la primera.
if (-not (Test-Path $destino)) { New-Item -ItemType Directory -Path $destino | Out-Null }

Compress-Archive -Path $exe -DestinationPath $zip -Force

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Escribir "  ZIP: $zip ($mb MB)" 'Green'

# --- Que falta ---------------------------------------------------------------------
Escribir ""
Escribir "Falta subir el Release a GitHub (es el unico paso manual):" 'Cyan'
Escribir ""
Escribir "  1. Se abre el navegador en la pagina de publicacion."
Escribir "  2. Arrastra este archivo a la zona de adjuntos:"
Escribir "       $zip" 'White'
Escribir "  3. Apreta 'Publish release'."
Escribir "  4. Volve aca y ejecuta:  .\publicar.ps1 -Publicar" 'White'
Escribir ""

$nueva = "https://github.com/$repo/releases/new?tag=$tag&title=AutoExam%20$version"
Start-Process $nueva

Escribir "Navegador abierto en:" 'DarkGray'
Escribir "  $nueva" 'DarkGray'
Escribir ""
