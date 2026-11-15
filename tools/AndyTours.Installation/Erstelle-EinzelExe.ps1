<#
.SYNOPSIS
    Baut eine einzelne Windows-x64-EXE für die Ablage im Repository.
.DESCRIPTION
    Die EXE enthält die App und ihre Bibliotheken, benötigt aber die .NET 10
    Desktop Runtime (x64). Native Bibliotheken werden beim Start temporär entpackt.
    Die vollständige self-contained Variante bleibt im vorhandenen Installationsskript.
#>
$ErrorActionPreference = 'Stop'

$Projektordner = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Zwischenausgabe = Join-Path $PSScriptRoot 'bin\EinzelExe'
$Zielordner = Join-Path $Projektordner 'releases\win-x64'

dotnet publish (Join-Path $Projektordner 'src\AndyTours.App\AndyTours.App.csproj') `
    --configuration Release --runtime win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableWindowsTargeting=true -p:DebugType=none -p:GenerateDocumentationFile=false `
    --output $Zwischenausgabe
if ($LASTEXITCODE -ne 0) { throw 'Die Einzel-EXE konnte nicht gebaut werden.' }

New-Item -ItemType Directory -Force -Path $Zielordner | Out-Null
$Exe = Join-Path $Zielordner 'AndyTours.App.exe'
Copy-Item -Force (Join-Path $Zwischenausgabe 'AndyTours.App.exe') $Exe
# Native NuGet-Pakete liefern teilweise eigene PDB-Dateien; diese sind nicht zum Start nötig
# und werden nicht in die Repository-Ablage übernommen.
$Hash = (Get-FileHash -Algorithm SHA256 $Exe).Hash.ToLowerInvariant()
Set-Content -Encoding ascii (Join-Path $Zielordner 'SHA256SUMS') "$Hash  AndyTours.App.exe"
Write-Host "Fertig: $Exe"
Write-Host '.NET 10 Desktop Runtime (x64) wird auf dem Zielrechner benötigt.'
