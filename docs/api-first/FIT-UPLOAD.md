# FIT-Upload und USB/MTP – Design

**Nur Entwurf; nicht implementiert oder verifiziert.** Bestehende Importfunktionen bleiben erhalten.

## Sechs Slices

1. **Verträge/Ports:** `AndyTours.Contracts.V1` erhält opake `UploadId`-/`DateiId`-GUIDs, Status, Preview und Commit-DTOs; `IImportUploadSpeicher` kapselt serverseitige Ablage. Keine synthetischen Garmin-IDs oder Garmin-only Tabellen.
2. **Sicherer Upload:** authentifizierter Endpoint `FitUploadEndpunkte` streamt rohe Bytes mit harter Bytezählung und Limits (Vorschlag: 128 MiB/Datei, 20 Dateien, 512 MiB/Satz). Sichere Originalnamen sind nur Metadaten. Gleiche ID plus gleicher Body ist idempotent; anderer Body ergibt Konflikt. Serverpfade werden allein aus opaken IDs gebildet.
3. **SQLite:** `SqliteImportUploadSpeicher` persistiert Owner, IDs, Hash, Revision, Status, Ablauf und Commit-Quittungen; Retry derselben Anfrage replayt, abweichender Payload wird abgelehnt.
4. **Gemeinsame Fachlogik:** `GarminImportPruefung`-Snapshot/Hash/Zeitzonenhelfer source-neutral extrahieren; vorhandenes `LeseFitAufzeichnung`, Backend-Fallback-Zone und Core-Regeln für Datum/Minuten-Dedupe, Equipment, Standardkategorie und Sensoren wiederverwenden.
5. **Review/Commit:** serverseitige Preview zeigt FIT-Daten, Fallback-Zone, Hinweise und Equipment. Confirm validiert Preview-Version und IDs; Tour, FIT-Anhang, Sensor-/Equipmentänderungen, Uploadstatus und Receipt werden atomar committed, mit definierter Datei-/DB-Recovery.
6. **Dünner WPF-Client:** `FindeMtpGeraete`/Laufwerksfinder bleiben lokal; Client kopiert ausgewählte Dateien temporär und streamt sie. Mehrfachauswahl, Fortschritt und Abbruch bleiben erhalten; kein Parser/DB-Zugriff im API-Frontend.

## Compile-only Tests

Grenzgrößen/Abbruch, Dateinamen-Traversal, Upload-/Receipt-Idempotenz, FIT-Zeitzonen- und stale-preview-Versionen, Dedupe/Equipment/Sensorregeln, atomarer Commit/Recovery sowie Architekturtests gegen Clientparser, SQL, Pfade und Fake-Garmin-IDs. Tests nur vorbereiten und kompilieren, nicht ausführen.
