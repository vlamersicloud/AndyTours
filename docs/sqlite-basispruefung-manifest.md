# Änderungsmanifest – SQLite-Basisprüfung

Alle Quellen liegen unter `next/`; Zielpfade gelten relativ zum Repository. Unveränderte Referenzkopien sind nicht Teil des Pakets.

| Quelle / Repositoryziel | Art |
|---|---|
| `docs/api-first/UMSTELLUNG.md` | geändert |
| `docs/sqlite-basispruefung-manifest.md` | neu |
| `docs/sqlite-basispruefung.md` | neu |
| `src/AndyTours.Api/Program.cs` | geändert |
| `src/AndyTours.Api/SqliteBasispruefungDienst.cs` | neu |
| `src/AndyTours.Api/SqliteBasispruefungEndpunkte.cs` | neu |
| `src/AndyTours.Api/SqliteBasispruefungKonfiguration.cs` | neu |
| `src/AndyTours.App/App.Api.cs` | geändert |
| `src/AndyTours.App/ViewModels/ApiWiederherstellungsvorschauViewModel.cs` | geändert |
| `src/AndyTours.App/Views/ApiWiederherstellungsvorschauFenster.xaml` | geändert |
| `src/AndyTours.Application/Sicherung/ISqliteBasispruefung.cs` | neu |
| `src/AndyTours.Client/BackendClientSitzung.cs` | geändert |
| `src/AndyTours.Client/SqliteBasispruefungHttpClient.cs` | neu |
| `src/AndyTours.Contracts/V1/SqliteBasispruefungVertrag.cs` | neu |
| `tests/AndyTours.Tests/AndyTours.Tests.csproj` | geändert |
| `tests/AndyTours.Tests/Sicherung/SqliteBasispruefungAbschlussFakeTests.cs` | neu |
| `tests/AndyTours.Tests/Sicherung/SqliteBasispruefungAdapterTests.cs` | neu |
| `tests/AndyTours.Tests/Sicherung/SqliteBasispruefungHttpClientTests.cs` | neu |
| `tests/AndyTours.Tests/Sicherung/SqliteBasispruefungIntegrationsquellenTests.cs` | neu |
| `tests/AndyTours.Tests/Sicherung/SqliteBasispruefungKonfigurationTests.cs` | neu |
| `tests/AndyTours.Tests/Sicherung/SqliteBasispruefungViewModelTests.cs` | neu |
| `tools/AndyTours.Installation/Erstelle-Installation.ps1` | geändert |

Gemeinsames Quellreview abgeschlossen; Checker, API, Windows-Ziel und Testprojekt auf 192.168.178.32 mit SDK 10.0.402 vollständig gebaut, jeweils 0 Warnungen / 0 Fehler. Logs: `/tmp/andytours-rest-integration-{checker,api,app,tests}-final-20261118.log`. Tests, Anwendungen, Installer und Checker wurden nicht ausgeführt. Keine Restore-/Apply-Freigabe, keine gesetzten Produktivbudgets oder Retention-Regeln.
