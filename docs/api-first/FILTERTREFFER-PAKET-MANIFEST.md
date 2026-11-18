# Filtertreffer-Auswahl – Paketmanifest

Gemeinsam geprüftes 28-Dateien-Paket zur expliziten Auswahl aller Touren eines angewandten Filters. API, Windows-Ziel und Testprojekt wurden auf 192.168.178.32 mit SDK 10.0.402 vollständig gebaut, jeweils 0 Warnungen / 0 Fehler (`--no-restore --no-incremental -c Release -p:EnableWindowsTargeting=true`). Logs: `/tmp/andytours-allfilter-selection-{api,app,tests}-final-20261118.log`. Tests und Anwendungen wurden nicht ausgeführt.

## Neue Dateien

- `src/AndyTours.Contracts/V1/TourenFilterauswahlVertrag.cs`
- `src/AndyTours.Application/Touren/ITourenFilterauswahl.cs`
- `src/AndyTours.Application/Touren/ITourenFilterauswahlClient.cs`
- `src/AndyTours.Api/TourenFilterauswahlEndpunkt.cs`
- `src/AndyTours.Api/TourenFilterauswahlGrenzen.cs`
- `src/AndyTours.Api/TourenFilterauswahlAntwortSerialisierer.cs`
- `src/AndyTours.App/ViewModels/ApiUebersichtViewModel.Filterauswahl.cs`
- `src/AndyTours.App/ViewModels/ApiTourenFilterauswahlCommit.cs`
- `tests/AndyTours.Tests/ApiTourenFilterauswahlStrukturTests.cs`
- `tests/AndyTours.Tests/ApiTourenFilterauswahlHttpClientTests.cs`
- `tests/AndyTours.Tests/ApiTourenFilterauswahlCommitTests.cs`
- `tests/AndyTours.Tests/ApiTourenFilterauswahlGrenzenTests.cs`
- `docs/api-first/FILTERTREFFER-PAKET-MANIFEST.md`

## Geänderte Dateien

- `src/AndyTours.Api/Program.cs` – Portregistrierung und Routenmapping
- `src/AndyTours.Client/TourenHttpClient.cs` – typisierter POST-Client
- `src/AndyTours.Daten/SqliteTourenSpeicher.cs` – read-only SQLite-Verbindung ohne Pooling/Bootstrap, gemeinsame gefilterte Lesetransaktion und fail-closed Treffer-Cap
- `src/AndyTours.App/ViewModels/ApiUebersichtViewModel.cs` – zuletzt erfolgreich angewandten Filter-Snapshot merken
- `src/AndyTours.App/ViewModels/ApiUebersichtViewModel.Auswahl.cs` – Action-Enabled-Benachrichtigung bei geöffneten Dialogen
- `src/AndyTours.App/ViewModels/ApiUebersichtViewModel.Inline.cs` – Inline-Lock-Benachrichtigung für Filtertrefferaktion
- `src/AndyTours.App/ViewModels/ApiTourenauswahl.cs` – atomarer vollständiger Snapshot-Import
- `src/AndyTours.App/Views/ApiHauptfenster.ManuelleAuswahl.cs` – expliziter UI-Aktionshandler
- `src/AndyTours.App/Views/ApiHauptfenster.xaml` – Button, Gesamtanzahl und Auswahl-leeren-Aktion
- `src/AndyTours.App/Views/ApiHauptfenster.xaml.cs` – blockiert Bearbeitendialoge während Auswahl-/Ladeoperation
- `tests/AndyTours.Tests/AndyTours.Tests.csproj` – eindeutige Quellressourcen plus gezielte Helper-Compile-Links für Fixtures
- `tests/AndyTours.Tests/ApiTourenauswahlTests.cs` – atomare Ersetzung/Immutability
- `tests/AndyTours.Tests/Daten/SqliteTourenFilterTests.cs` – Read-only-Schemahash und kein Bootstrap fehlender Datenbank
- `tests/AndyTours.Tests/ApiTourenFilterauswahlHttpClientTests.cs` – Fake-HTTP Route, Cancel, Fehlerbody-Disposition und malformed responses
- `tests/AndyTours.Tests/ApiTourenFilterauswahlCommitTests.cs` – Filterecho, Generation, Close und atomarer Commit
- `tests/AndyTours.Tests/ApiTourenFilterauswahlGrenzenTests.cs` – Pflichtkonfiguration und exakte/überschrittene Antwortbudgets
- `tests/AndyTours.Tests/Daten/SqliteTourenFilterTests.cs` – gefilterter Snapshot, Versionsidentität und Cap-Ablehnung
- `docs/api-first/TOURENAUSWAHL.md` – API, Konfiguration und Fehlersemantik
- `docs/api-first/UMSTELLUNG.md` – Status, Toureninventar und Backend-Konfigurationshinweis

## Konfigurationsvertrag

Erforderlich ohne Defaults: `Backend:Tourenauswahl:MaxTouren` und `Backend:Tourenauswahl:MaxAntwortBytes`. Fehlt oder invalidiert ein Wert, liefert nur der Filterauswahlendpunkt HTTP 503. Überschreiten Trefferzahl oder serialisierte Antwort das jeweilige Budget, liefert der Endpunkt HTTP 413 ohne Teilresultat. Die Trefferfilterung materialisiert derzeit Tourdaten vor der Count-Cap-Prüfung; MaxTouren/MaxAntwortBytes begrenzen nicht den gesamten Prozessspeicherbedarf. Konkrete Produktivwerte sind nicht festgelegt.

## Nicht Bestandteil

Keine Änderung an Mehrfachaktionen, Bestätigungsdialogen, CAS-/Replay-Regeln, Datenbestand oder Aufbewahrung. Keine automatische Auswahl beim Laden und keine automatische Schreibaktion. Keine Projekt-/Deploymentwerte für die beiden Pflichtgrenzen. Build und Quellreview sind abgeschlossen; Tests und Windows-/Mehrclient-Laufzeitabnahme bleiben beim Auftraggeber offen. Keine Laufzeitfreigabe.
