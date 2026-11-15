# AndyTours – einzelne Windows-EXE

[AndyTours.App.exe herunterladen](https://raw.githubusercontent.com/vlamersicloud/AndyTours/main/releases/win-x64/AndyTours.App.exe)

## Voraussetzungen und Start

- Windows 10 (ab Build 19041) oder Windows 11, **x64**.
- **.NET 10 Desktop Runtime (x64)** installieren:
  https://dotnet.microsoft.com/download/dotnet/10.0
  Dort „.NET Desktop Runtime“ → Windows x64 wählen; die reine ASP.NET/Core Runtime reicht nicht.
- EXE herunterladen, in einem eigenen Ordner speichern und per Doppelklick starten.
  Keine Installation von AndyTours und kein SDK nötig.
- Keine digitale Signatur: Windows/SmartScreen kann deshalb einen Hinweis anzeigen.
- Garmin-Key unter Extras → Einstellungen → Persönlich → Garmin Connect hinterlegen.
  Die EXE enthält keinen echten API-Key, Zugangstoken oder persönlichen Datenbestand.

Die frameworkabhängige Single-File-EXE enthält die Anwendungsbibliotheken sowie
native Bibliotheken. Letztere werden beim Start in ein temporäres Verzeichnis
entpackt. Die .NET-Laufzeit ist bewusst nicht enthalten, damit die EXE unter dem
GitHub-Limit von 100 MiB pro Datei bleibt. Die vorherigen self-contained ZIP-Pakete
bzw. das Installationsskript enthalten dagegen die Laufzeit.

## Stand und Prüfung

Enthält Quellstand **d1f815e7f12496ba334dcda163c8ad34f3fe74a5**, einschließlich
persönlichem API-Key-Feld, automatischem Laden aller Pakete zu je höchstens 20,
einmaliger abschließender Importauswahl und transaktionalem Duplikatschutz.

Release-Publish auf dem Linux-Testserver für win-x64 erfolgreich.
Windows-PE-Format und SHA-256 geprüft. **Windows-Start und Garmin-Live-Abnahme
stehen weiterhin aus.** Für diesen Paketierungsschritt wurden keine Tests ausgeführt.
Gebaut mit SDK 10.0.401; die Auswahl wurde nur vorübergehend angepasst und die
versionierte global.json (10.0.402) unverändert belassen.

Hash prüfen (PowerShell im Downloadordner):
```powershell
Get-FileHash .\AndyTours.App.exe -Algorithm SHA256
```
Erwarteter Hash: siehe [SHA256SUMS](SHA256SUMS).

## Neu bauen

Im Projektordner unter Windows mit dem in global.json geforderten .NET SDK:
```powershell
powershell -File tools/AndyTours.Installation/Erstelle-EinzelExe.ps1
```

Das Skript aktualisiert EXE und SHA256SUMS. Danach beide Dateien gemeinsam committen.
Für ein vollständiges Installationsprogramm mit eingebauter .NET-Laufzeit bleibt
`tools/AndyTours.Installation/Erstelle-Installation.ps1` zuständig.
