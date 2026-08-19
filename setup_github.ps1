<#
.SYNOPSIS
    Script para automatizar la inicialización de Git, creación de .gitignore para C#/.NET 8,
    primer commit y subida del proyecto a un nuevo repositorio de GitHub.

.DESCRIPTION
    Este script realiza las siguientes acciones:
    1. Verifica que Git esté instalado en el sistema.
    2. Crea un archivo .gitignore optimizado para proyectos C# y .NET 8 si no existe.
    3. Inicializa el repositorio local con la rama principal 'main'.
    4. Agrega los archivos y realiza el commit inicial.
    5. Permite crear y subir el repositorio a GitHub automáticamente usando GitHub CLI (`gh`)
       o vinculándolo manualmente mediante la URL remota.

.NOTES
    Requisito opcional pero recomendado: GitHub CLI (`gh`).
    Si no está instalado, el script solicitará la URL de un repositorio previamente creado en GitHub.
#>

[CmdletBinding()]
param (
    [string]$RepoName = (Split-Path -Leaf (Get-Location)),
    [ValidateSet("private", "public")]
    [string]$Visibility = "private"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  CONFIGURADOR E INICIALIZADOR AUTOMÁTICO DE GIT Y GITHUB " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar si Git está instalado
Write-Host "[1/5] Verificando instalación de Git..." -ForegroundColor Yellow
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "Git no está instalado o no se encuentra en el PATH. Descárgalo e instálalo desde https://git-scm.com/"
    exit 1
}
Write-Host " -> Git detectado correctamente." -ForegroundColor Green

# 2. Crear archivo .gitignore para C# y .NET
Write-Host "[2/5] Configurando el archivo .gitignore..." -ForegroundColor Yellow
$gitIgnorePath = Join-Path -Path (Get-Location) -ChildPath ".gitignore"

$gitIgnoreContent = @"
## Visual Studio y .NET
bin/
obj/
out/
.vs/
.vscode/
*.user
*.suo
*.userosscache
*.sln.docstates

## Archivos de compilación y paquetes
*.dll
*.exe
*.pdb
*.cache
packages/
*.nupkg

## Registros y archivos temporales
*.log
*.tmp
*.temp
TestResults/
coverage/

## Sistema Operativo
Thumbs.db
ehthumbs.db
Desktop.ini
.DS_Store
"@

if (Test-Path $gitIgnorePath) {
    Write-Host " -> El archivo .gitignore ya existe. Se mantendrá el existente." -ForegroundColor DarkGray
} else {
    Set-Content -Path $gitIgnorePath -Value $gitIgnoreContent -Encoding UTF8
    Write-Host " -> Archivo .gitignore creado exitosamente." -ForegroundColor Green
}

# 3. Inicializar Repositorio Git Local
Write-Host "[3/5] Inicializando el repositorio Git local..." -ForegroundColor Yellow
if (-not (Test-Path ".git")) {
    git init -b main
    Write-Host " -> Repositorio Git local inicializado (rama: main)." -ForegroundColor Green
} else {
    Write-Host " -> El repositorio Git ya estaba inicializado." -ForegroundColor DarkGray
}

# 4. Crear el Primer Commit
Write-Host "[4/5] Creando el primer commit..." -ForegroundColor Yellow
git add .
$status = git status --porcelain
if ($status) {
    git commit -m "Initial commit - Estructura base del proyecto C# .NET"
    Write-Host " -> Commit inicial realizado con éxito." -ForegroundColor Green
} else {
    Write-Host " -> No hay cambios pendientes para incluir en el commit." -ForegroundColor DarkGray
}

# 5. Publicar en GitHub
Write-Host "[5/5] Conectando con GitHub..." -ForegroundColor Yellow

if (Get-Command gh -ErrorAction SilentlyContinue) {
    Write-Host " -> GitHub CLI (`gh`) detectado. Intentando crear el repositorio automáticamente..." -ForegroundColor Cyan
    try {
        gh repo create $RepoName --$Visibility --source=. --remote=origin --push
        Write-Host ""
        Write-Host "¡ÉXITO! Repositorio '$RepoName' creado y subido a GitHub en modo $Visibility." -ForegroundColor Green
    } catch {
        Write-Warning "No se pudo publicar usando GitHub CLI automáticamente (posiblemente falta autenticación 'gh auth login' o el repo ya existe)."
        $useManual = $true
    }
} else {
    $useManual = $true
}

if ($useManual) {
    Write-Host ""
    Write-Host "Paso Manual Requiere Intervención:" -ForegroundColor Yellow
    Write-Host "1. Ve a https://github.com/new y crea un repositorio llamado '$RepoName'." -ForegroundColor White
    Write-Host "2. Copia la URL HTTPS o SSH del repositorio (ej. https://github.com/tu-usuario/$RepoName.git)." -ForegroundColor White
    Write-Host ""
    
    $repoUrl = Read-Host "Pega aquí la URL del repositorio de GitHub (o presione Enter para omitir)"
    if ($repoUrl) {
        git remote remove origin 2>$null
        git remote add origin $repoUrl
        git branch -M main
        git push -u origin main
        Write-Host ""
        Write-Host "¡ÉXITO! Código subido a GitHub en $repoUrl" -ForegroundColor Green
    } else {
        Write-Host "Proceso finalizado localmente. Puedes agregar el remoto más tarde con:" -ForegroundColor DarkGray
        Write-Host "git remote add origin <URL>" -ForegroundColor Gray
        Write-Host "git push -u origin main" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  PROCESO FINALIZADO CON ÉXITO                             " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
