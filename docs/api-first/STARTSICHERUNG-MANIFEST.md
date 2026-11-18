# Startsicherung – Paketmanifest

Source-only package for a single awaited backend-start backup attempt. No runtime verification is claimed.

## Production sources
- `src/AndyTours.Application/Sicherung/IBackendStartsicherung.cs` — typed counter/archive ports and result contract.
- `src/AndyTours.Application/Sicherung/BackendStartsicherungsKoordinator.cs` — one exclusive lease, due check, one archive attempt, shared catalog-shape validation, counter CAS reset.
- `src/AndyTours.Application/Sicherung/WiederherstellungsvorschauRegeln.cs` — existing shared validator reused unchanged.
- `src/AndyTours.Daten/Datensicherung/SqliteBackendStartsicherungsZaehler.cs` — existing-file-only SQLite counter updates.
- `src/AndyTours.Api/SicherungsarchivBereitstellung.cs` — adapter to existing archive preparation.
- `src/AndyTours.Api/Program.cs` — hosted-service registration only; no direct backup invocation.
- `src/AndyTours.Api/BackendStartsicherungsHostedService.cs` — awaited `IHostedLifecycleService.StartingAsync` before any hosted service `StartAsync`.

## Fixtures
- `tests/AndyTours.Tests/Sicherung/BackendStartsicherungsKoordinatorTests.cs` — due/not-due, failures, independently malformed catalog metadata, AlreadyPresent, CAS, and deterministically gated cancellation-ignoring writer.
- `tests/AndyTours.Tests/Sicherung/BackendStartsicherungsStrukturTests.cs` — lifecycle-phase wiring, no legacy-trigger structure, and shared validator reuse.
- `tests/AndyTours.Tests/Sicherung/BackendStartsicherungsHostedServiceTests.cs` — lifecycle cancellation and gated awaited-completion contract without starting a real host.
- `tests/AndyTours.Tests/Datensicherung/SqliteBackendStartsicherungsZaehlerTests.cs` — raw-setting preservation, CAS, missing DB/schema, invalid policy/counter, saturation, and precancel.
- `tests/AndyTours.Tests/AndyTours.Tests.csproj` — embeds Program, lifecycle service, counter, coordinator, and shared validator for structural assertions.

Fixtures are source only and were not executed. API, Windows app, and test project were fully compiled remotely with SDK 10.0.402, each with 0 warnings and 0 errors. No tests, process startup, database fixture execution, or real backup was performed. See STARTSICHERUNG.md for build evidence.

## Documentation
- `docs/api-first/STARTSICHERUNG.md`
- `docs/api-first/EINSTELLUNGEN-SICHERUNG.md`
- `docs/api-first/UMSTELLUNG.md`

Retention/deletion, restore/apply, external-writer coordination, and runtime verification remain outside this package.
