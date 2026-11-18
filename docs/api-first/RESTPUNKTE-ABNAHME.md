# Restpunkte – Stand dieser Quellgruppe

## Geliefert
- Reiner Vollarchiv-Aufbewahrungsplanner mit gültigem Katalogsnapshot, Anzahl 1–99, Schutz des frisch erstellten Vollarchivs, stabiler Sortierung und read-only Ergebnislisten.
- Legacy/ungültige Einträge werden nicht als Kandidaten ausgegeben; widersprüchliche Snapshot-Identität, doppelte IDs oder fehlendes Fresh-Archiv ergeben keinen Plan.
- Pure Testquellen decken Grenzwerte, Gleichstände, Permutation, Fresh-Schutz, ungültige/Legacy-Einträge und Unveränderlichkeit ab.
- Dokumentation benennt die fehlende sichere Löschprimitive statt ein unsicheres `Delete(path)` zu behaupten.

## Nicht geliefert / nicht abgenommen
- Kein automatischer Retention-Executor, keine echte Löschung und keine Lifecycle-/Hosted-Service-Integration. Das ist durch die nicht belegte identitätsgebundene Löschprimitive blockiert.
- API, Windows-Ziel und Testprojekt wurden am 18.11.2026 remote mit SDK 10.0.402 vollständig und jeweils mit 0 Warnungen / 0 Fehlern gebaut. Keine Tests ausgeführt; keine App/API gestartet und kein Windows-Rendering geprüft. Kein verbundener Windows-Knoten für diese Abnahme verfügbar.
- Restore-Apply bleibt gesperrt; dieses Paket führt keine Wiederherstellung aus.
- Windows-, Geräte-, Karten- und Provider-Laufzeit sowie Installerprüfung sind nicht Gegenstand dieser Quellgruppe und bleiben offen.

## Bestandsübernahme
Bestandsübernahme ist ausdrücklich nicht geplant: Der Nutzer übernimmt den Bestand selbst neu. Kein Online-Import, Netzwerkpfad-Upload oder Live-Restore wird daraus abgeleitet. Das bestehende Entwicklerwerkzeug für Offline-Altdatenkonvertierung wird nicht in den Installer zurückgebracht.

Keine Aussage über Gesamtmigration oder vollständige Parität. Alle Tests bleiben dem Nutzer vorbehalten.
