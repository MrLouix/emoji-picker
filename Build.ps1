# =============================================================================
# EmojiPick — Build Script (Windows / PowerShell 7+)
# =============================================================================

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Publish,

    [switch]$Installer,

    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SolutionPath = Join-Path $ScriptDir "EmojiPick.sln"
$OutputDir = Join-Path $ScriptDir "publish"
$LogPath = Join-Path $ScriptDir "build.log"

# Initialise le log
"`n=== Build démarré le $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Out-File $LogPath -Encoding utf8

# Helper: log à la fois console et fichier
function Write-Log {
    param([string]$Message, [string]$Color = "White")
    $Message | Out-File $LogPath -Encoding utf8 -Append
    Write-Host $Message -ForegroundColor $Color
}

# --- 1. Prérequis ---------------------------------------------------------
Write-Log ""
Write-Log "=== EmojiPick Build Script ===" Cyan
Write-Log ""

# Vérifie dotnet.exe
$dotnet = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Log "[ERREUR] .NET SDK non détecté. Installe le .NET 7.0 SDK :" Red
    Write-Log "  https://dotnet.microsoft.com/download/dotnet/7.0" Yellow
    exit 1
}

$sdkVersion = (dotnet --version) 2>$null
Write-Log "[OK]   dotnet $sdkVersion" Green

# Vérifie la .sln
if (-not (Test-Path $SolutionPath)) {
    Write-Log "[ERREUR] Fichier .sln introuvable : $SolutionPath" Red
    exit 1
}

# --- 2. Nettoyage optionnel ------------------------------------------------
if ($Clean) {
    Write-Log "[...]  Nettoyage (dotnet clean)..." Yellow
    dotnet clean $SolutionPath -c $Configuration --nologo 2>&1 | Out-File $LogPath -Encoding utf8 -Append
    dotnet clean $SolutionPath --nologo 2>&1 | Out-File $LogPath -Encoding utf8 -Append
}

# --- 3. Restauration des packages ------------------------------------------
Write-Log "[...]  Restauration des packages NuGet..." Yellow
$restoreResult = dotnet restore $SolutionPath --nologo 2>&1
$restoreResult | Out-File $LogPath -Encoding utf8 -Append
if ($LASTEXITCODE -ne 0) {
    Write-Log "[ERREUR] dotnet restore a échoué." Red
    exit 1
}
Write-Log "[OK]   Restore terminé." Green

# --- 4. Build ---------------------------------------------------------------
Write-Log "[...]  Compilation ($Configuration)..." Yellow
$buildResult = dotnet build $SolutionPath -c $Configuration --no-restore --nologo 2>&1
$buildResult | Out-File $LogPath -Encoding utf8 -Append
if ($LASTEXITCODE -ne 0) {
    Write-Log "[ERREUR] dotnet build a échoué." Red
    exit 1
}
Write-Log "[OK]   Build réussi." Green

# --- 5. Publish optionnel --------------------------------------------------
if ($Publish) {
    Write-Log "[...]  Publication (publish)..." Yellow
    # Note: --no-build is omitted intentionally; publish needs to rebuild with the correct RID for single-file bundling
    $publishResult = dotnet publish "src\EmojiPick\EmojiPick.csproj" -c $Configuration -o $OutputDir -r win-x64 --nologo 2>&1
    $publishResult | Out-File $LogPath -Encoding utf8 -Append
    if ($LASTEXITCODE -ne 0) {
        Write-Log "[ERREUR] dotnet publish a échoué." Red
        exit 1
    }
    Write-Log "[OK]   Publié vers : $OutputDir" Green
    Write-Log ""
    Get-ChildItem $OutputDir -Recurse | Where-Object { !$_.PSIsContainer } | Sort-Object Length -Descending | Select-Object -First 5 | ForEach-Object {
        Write-Log "       $($_.Name) ($([math]::Round($_.Length / 1KB, 1)) KB)" DarkGray
    }
}

# --- 6. Résumé --------------------------------------------------------------
Write-Log ""
Write-Log "=== Résumé ===" Cyan

$binDir = Join-Path (Join-Path (Join-Path $ScriptDir "src") "EmojiPick") "bin"
if (Test-Path $binDir) {
    $assemblies = Get-ChildItem "$binDir\**\*.exe" -Recurse -ErrorAction SilentlyContinue
    foreach ($exe in $assemblies) {
        Write-Log "       $($exe.Name) -> $($exe.DirectoryName)" DarkGray
    }
}

$buildErrors = $null
if ($buildResult) {
    $buildErrors = ($buildResult | Select-String "error|warning" -CaseSensitive:$false)
}
if ($buildErrors) {
    Write-Log ""
    Write-Log "--- Warnings/Errors ---" Yellow
    $buildErrors | Out-File $LogPath -Encoding utf8 -Append
    $buildErrors | ForEach-Object { Write-Log "  $_" Yellow }
}

Write-Log ""
Write-Log "Log complet : $LogPath" Gray
Write-Log "Tape ./Build.ps1 -Publish pour générer un dossier publish prêt à déployer." Gray
Write-Log "Tape ./Build.ps1 -Installer pour générer l'installeur MSI (WiX v4)." Gray
Write-Log "Tape ./Build.ps1 -Clean pour nettoyer avant la compilation." Gray
Write-Log ""

# --- 7. Installer optionnel (WiX MSI) --------------------------------------
if ($Installer) {
    $InstallerPath = Join-Path $ScriptDir "EmojiPick.Installer\EmojiPick.wixproj"
    if (-not (Test-Path $InstallerPath)) {
        Write-Log "[ERREUR] Projet WiX introuvable : $InstallerPath" Red
        exit 1
    }
    Write-Log ""
    Write-Log "[...]  Génération de l'installeur MSI (WiX v4)..." Yellow
    $installerResult = dotnet build $InstallerPath -c Release --nologo 2>&1
    $installerResult | Out-File $LogPath -Encoding utf8 -Append
    if ($LASTEXITCODE -ne 0) {
        Write-Log "[ERREUR] La génération de l'installeur a échoué." Red
        exit 1
    }
    Write-Log "[OK]   Installeur généré." Green
    $msiPath = Join-Path $ScriptDir "EmojiPick.Installer\bin\Release\netstandard2.0\EmojiPickerSetup.msi"
    if (Test-Path $msiPath) {
        $size = [math]::Round((Get-Item $msiPath).Length / 1MB, 2)
        Write-Log "       MSI : $msiPath ($size MB)" Green
    } else {
        # Alternative path for SDK output
        $msiAlt = Join-Path $ScriptDir "EmojiPick.Installer\bin\Release\EmojiPickerSetup.msi"
        if (Test-Path $msiAlt) {
            $size = [math]::Round((Get-Item $msiAlt).Length / 1MB, 2)
            Write-Log "       MSI : $msiAlt ($size MB)" Green
        } else {
            Write-Log "[WARN] MSI non trouvé dans les chemins habituels. Vérifie le dossier bin." Yellow
        }
    }
}
