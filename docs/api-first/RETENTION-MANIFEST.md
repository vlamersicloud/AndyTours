# Vollarchiv-Aufbewahrung – Manifest

## Paketdateien
- `src/AndyTours.Application/Sicherung/VollarchivAufbewahrungsPlaner.cs` — immutable pure preview; keine Löschberechtigung.
- `tests/AndyTours.Tests/Sicherung/VollarchivAufbewahrungsPlanerTests.cs` — Quellfixtures für Anzahl 1/99, Fresh-Schutz, ties/permutation, doppelte IDs, fehlende/abweichende Fresh-Metadaten, Legacy/invalid und defensive Unveränderlichkeit.
- `docs/api-first/RETENTION.md` — Semantik und expliziter Executorblocker.
- `docs/api-first/RESTPUNKTE-ABNAHME.md` — geliefert/offen und Abnahmegrenzen.
- `docs/api-first/RETENTION-MANIFEST.md` — dieses Manifest.
- `docs/api-first/UMSTELLUNG.md` — Inventar-/Restpunktkorrektur.

## Status
Quellpaket gemeinsam geprüft. API, Windows-Ziel und Testprojekt am 18.11.2026 auf dem Testserver mit SDK 10.0.402 vollständig gebaut: jeweils 0 Warnungen / 0 Fehler. Keine Tests, Apps oder Dateilöschungen ausgeführt. Automatic Retention, Restore-Apply, Windows-/Geräte-/Providerprüfung und Gesamtabnahme bleiben offen.
