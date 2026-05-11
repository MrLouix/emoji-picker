# =============================================================================
# EmojiPick — Build Script (Windows / PowerShell 7+)
# =============================================================================

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Publish,

    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SolutionPath = Join-Path $ScriptDir "EmojiPick.sln"
$OutputDir = Join-Path $ScriptDir "publish"

# --- 1. Prérequis ---------------------------------------------------------
Write-Host ""
Write-Host "=== EmojiPick Build Script ===" -ForegroundColor Cyan
Write-Host ""

# Vérifie dotnet.exe
$dotnet = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "[ERREUR] .NET SDK non détecté. Installe le .NET 7.0 SDK :" -ForegroundColor Red
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/7.0" -ForegroundColor Yellow
    exit 1
}

$sdkVersion = (dotnet --version) 2>$null
Write-Host "[OK]   dotnet $sdkVersion" -ForegroundColor Green

# Vérifie la .sln
if (-not (Test-Path $SolutionPath)) {
    Write-Host "[ERREUR] Fichier .sln introuvable : $SolutionPath" -ForegroundColor Red
    exit 1
}

# --- 2. Nettoyage optionnel ------------------------------------------------
if ($Clean) {
    Write-Host "[...]  Nettoyage (dotnet clean)..." -ForegroundColor Yellow
    dotnet clean $SolutionPath -c $Configuration --nologo
    dotnet clean $SolutionPath --nologo
}

# --- 3. Restauration des packages ------------------------------------------
Write-Host "[...]  Restauration des packages NuGet..." -ForegroundColor Yellow
dotnet restore $SolutionPath --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERREUR] dotnet restore a échoué." -ForegroundColor Red
    exit 1
}
Write-Host "[OK]   Restore terminé." -ForegroundColor Green

# --- 4. Build ---------------------------------------------------------------
Write-Host "[...]  Compilation ($Configuration)..." -ForegroundColor Yellow
dotnet build $SolutionPath -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERREUR] dotnet build a échoué." -ForegroundColor Red
    exit 1
}
Write-Host "[OK]   Build réussi." -ForegroundColor Green

# --- 5. Publish optionnel --------------------------------------------------
if ($Publish) {
    Write-Host "[...]  Publication (publish)..." -ForegroundColor Yellow
    dotnet publish $SolutionPath -c $Configuration --no-build -o $OutputDir --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERREUR] dotnet publish a échoué." -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK]   Publié vers : $OutputDir" -ForegroundColor Green
    Write-Host ""
    Get-ChildItem $OutputDir -Recurse | Where-Object { !$_.PSIsContainer } | Sort-Object Length -Descending | Select-Object -First 5 | ForEach-Object {
        Write-Host "       $($_.Name) ($([math]::Round($_.Length / 1KB, 1)) KB)" -ForegroundColor DarkGray
    }
}

# --- 6. Résumé --------------------------------------------------------------
Write-Host ""
Write-Host "=== Résumé ===" -ForegroundColor Cyan

$binDir = Join-Path (Join-Path $ScriptDir "EmojiPick") "bin"
if (Test-Path $binDir) {
    $assemblies = Get-ChildItem "$binDir\**\*.exe" -Recurse -ErrorAction SilentlyContinue
    foreach ($exe in $assemblies) {
        Write-Host "       $($exe.Name) -> $($exe.DirectoryName)" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Tape ./Build.ps1 -Publish pour générer un dossier publish prêt à déployer." -ForegroundColor Gray
Write-Host "Tape ./Build.ps1 -Clean pour nettoyer avant la compilation." -ForegroundColor Gray
Write-Host ""
