# AndyTours

Windows-Desktop-App zur Verwaltung der eigenen Fahrradtouren.

Umgesetzt sind die Funktionen, die ich selbst tatsächlich nutze. Die App ist für
einen einzelnen Benutzer gedacht.

## Funktionen

- **Touren** in einer Tabelle mit Filter, Suche und Mehrfachauswahl, Bearbeiten einzelner Touren
- **Import vom Garmin Edge 1040** per USB (Mediengerät/MTP), FIT-Dateien über das Garmin FIT SDK
- **Garmin Connect live**: eigener Personal API-Key, inkrementeller Radaktivitäten-Import mit lokalem Originalarchiv, optional automatisch alle 20er-Pakete laden ([Einrichtung](docs/garmin-connect.md))
- **Höhenprofil und Streckenkarte**, Mehrtagestouren, Track bearbeiten, GPX-Export
- **Aktivitätsübersicht**: Zeitraum frei wählbar, Jahr/Monat/ISO-Woche/Tag, km/hm/Fahrzeit/Anzahl und Bestleistungen im Zeitraum
- **Pulsbereiche** aus FIT-Messpunkten mit aktuellen Garmin-Referenzzonen (bpm-Grenzen und Abrufdatum), alternativ 10-bpm-Bereiche; Saisonziele bleiben erhalten
- **Wartung der Räder**: Wartungen erfassen, Wartungsalarme, Haltbarkeit von Teilen, Einsatz der Equipments
- **Tourenplanung** auf der Karte mit BRouter oder openrouteservice, Overlays, Übertragen von Courses auf den Edge
- **Datensicherung** und Wiederherstellung
- **Einmalige Übernahme** von Altdaten mit einem eigenen Kommandozeilenprogramm (Werkzeug für den Entwickler, nicht Teil der Installation)

## Verteilung

Keine fertige EXE im Repository. Unter Windows aus dem Quellcode bauen und starten
(siehe unten). Die neue [Aktivitäts- und Pulsbereichsauswertung](docs/aktivitaetsauswertung.md)
ist dort ebenfalls dokumentiert.

## Benutzerhandbuch

Installation und Bedienung beschreibt das [Benutzerhandbuch](HANDBUCH.md).

## Technik

- C# mit .NET 10, WPF mit MVVM (`CommunityToolkit.Mvvm`) im Fluent-Design
- SQLite über `Microsoft.Data.Sqlite`
- `Garmin.FIT.Sdk`, `OxyPlot.Wpf` (Diagramme), `Mapsui.Wpf` (Karten), `MediaDevices` (MTP)
- Tests mit xUnit

Einzelheiten stehen in [`docs/tech.md`](docs/tech.md), der Aufbau des Projekts in
[`docs/structure.md`](docs/structure.md), die Architekturentscheidungen in [`docs/adr/`](docs/adr/).
Für jedes Feature gibt es unter [`specs/`](specs/) Spec, Plan und Aufgaben.

## Bauen und Starten

Voraussetzung: Windows und das .NET SDK 10.0.4xx (`winget install Microsoft.DotNet.SDK.10`).

```powershell
dotnet build AndyTours.slnx                 # Bauen
dotnet test AndyTours.slnx                  # Testen
dotnet run --project src/AndyTours.App      # Starten
```

Installationsprogramm bauen (benötigt Inno Setup 6):

```powershell
powershell tools/AndyTours.Installation/Erstelle-Installation.ps1
```

## Hinweise

Dies ist ein privates Hobbyprojekt. Es steht in keiner Verbindung zu Garmin. Die genannten
Marken gehören ihren jeweiligen Inhabern.

## Lizenz

[MIT](LICENSE) © Andreas Hensel
