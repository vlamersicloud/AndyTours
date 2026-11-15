<#
.SYNOPSIS
    Baut das Installationsprogramm von AndyTours (Spezifikationen 033 und 036).

.DESCRIPTION
    1. Liest die Versionsnummer aus Directory.Build.props.
    2. Veröffentlicht die App und das lokale Backend als Release-Builds für win-x64 mit eingebautem .NET (self-contained),
       damit auf dem Zielrechner nichts weiter installiert sein muss. Das Backend liegt im Unterordner backend.
    3. Übersetzt AndyTours.iss mit dem Inno-Setup-Compiler ISCC.exe.
    Das Umwandlungsprogramm für Altdaten gehört nicht zur Installation (Spezifikation 036).

    Ergebnis: tools/AndyTours.Installation/bin/Setup/AndyTours-Setup-<Version>.exe
    Aufruf im Projektordner: powershell tools/AndyTours.Installation/Erstelle-Installation.ps1
#>

$ErrorActionPreference = 'Stop'

# Ordner relativ zu diesem Skript, damit der Aufruf aus jedem Ordner funktioniert
$Installationsordner = $PSScriptRoot
$Projektordner = (Resolve-Path (Join-Path $Installationsordner '..\..')).Path
$Ausgabe = Join-Path $Installationsordner 'bin'
$AppAusgabe = Join-Path $Ausgabe 'App'
$SetupAusgabe = Join-Path $Ausgabe 'Setup'

# --- 1. Version aus Directory.Build.props ---
[xml]$Eigenschaften = Get-Content -Raw -Encoding UTF8 (Join-Path $Projektordner 'Directory.Build.props')
$Version = ($Eigenschaften.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $Version) {
    throw 'In Directory.Build.props steht keine <Version>.'
}
Write-Host "AndyTours $Version"

# Alte Ausgaben entfernen, damit keine Dateien früherer Fassungen ins Installationsprogramm geraten
if (Test-Path $Ausgabe) {
    Remove-Item -Recurse -Force $Ausgabe
}

# --- 2. App veröffentlichen ---
Write-Host 'App wird veröffentlicht …'
dotnet publish (Join-Path $Projektordner 'src\AndyTours.App\AndyTours.App.csproj') `
    --configuration Release --runtime win-x64 --self-contained true `
    --output $AppAusgabe -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'Die App konnte nicht veröffentlicht werden.' }

# --- 3. Lokales Backend veröffentlichen ---
Write-Host 'Lokales Backend wird veröffentlicht …'
dotnet publish (Join-Path $Projektordner 'src\AndyTours.Api\AndyTours.Api.csproj') `
    --configuration Release --runtime win-x64 --self-contained true `
    --output (Join-Path $AppAusgabe 'backend') -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'Das Backend konnte nicht veröffentlicht werden.' }

# --- 4. Inno-Setup-Compiler suchen und aufrufen ---
$Kandidaten = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$Iscc = $Kandidaten | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Iscc) {
    $Befehl = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($Befehl) { $Iscc = $Befehl.Source }
}
if (-not $Iscc) {
    throw 'Inno Setup 6 ist nicht installiert (ISCC.exe nicht gefunden). Installation: winget install JRSoftware.InnoSetup'
}

Write-Host 'Installationsprogramm wird übersetzt …'
& $Iscc "/DAppVersion=$Version" "/DAppQuelle=$AppAusgabe" "/O$SetupAusgabe" `
    (Join-Path $Installationsordner 'AndyTours.iss')
if ($LASTEXITCODE -ne 0) { throw 'Das Installationsprogramm konnte nicht übersetzt werden.' }

Write-Host ''
Write-Host "Fertig: $(Join-Path $SetupAusgabe "AndyTours-Setup-$Version.exe")"
