# Textlisten-Nutzung – Paketmanifest

## Geänderte/ergänzte Quellen
- `src/AndyTours.Contracts/V1/TextlistenVertrag.cs`
- `src/AndyTours.Application/Stammdaten/ITextlistenVerwaltung.cs`
- `src/AndyTours.Daten/SqliteTextlistenVerwaltung.cs`
- `src/AndyTours.Api/TextlistenEndpunkte.cs`
- `src/AndyTours.Client/TextlistenVerwaltungHttpClient.cs`
- `src/AndyTours.App/ViewModels/ApiTextlisteViewModel.cs`
- `tests/AndyTours.Tests/Daten/SqliteTextlistenVerwaltungTests.cs`
- `tests/AndyTours.Tests/Presentation/ApiTextlisteNutzungViewModelTests.cs`
- `tests/AndyTours.Tests/Client/TextlistenNutzungHttpClientTests.cs`
- `tests/AndyTours.Tests/AndyTours.Tests.csproj`
- `docs/api-first/TEXTLISTEN-NUTZUNG.md`
- `docs/api-first/TEXTLISTEN-NUTZUNG-MANIFEST.md`
- `docs/api-first/UMSTELLUNG.md`

## Geprüfte Grenzen
API/AppWindows/Testprojekt vollständig mit SDK 10.0.402 gebaut, jeweils 0 Warnungen/0 Fehler; Buildlogs siehe TEXTLISTEN-NUTZUNG.md. Keine Tests/Apps ausgeführt. Keine API-/HTTP-/UI-Laufzeitprüfung. Bestehende Textlisten-CRUD- und Reihenfolgeoperationen bleiben erforderlich und wurden nicht als ersetzt bewertet.
