# Vollarchiv-Writer: Phasenplan und Implementierungsstand

1. **Quellinventar (Komponente vorhanden):** `SicherungsquellenInventar` validiert Grenzen/Pfade, inventarisiert Anhangreferenzen und Orphans und schließt Backupordner, SQLite-Sidecars und bekannte Stagingnamen aus. Fehlende/abweichende Referenzen und Reparse-Einträge/-Ancestors schlagen fehl.

2. **Snapshot-Kern (Komponente vorhanden):** `SicherungsdatenbankSnapshot` verlangt ein vorhandenes leeres Stagingverzeichnis, erstellt eine SQLite-Onlinekopie und prüft Schema/Quick-Check sowie begrenzte Anhangpfade und `data_version`. Äußere exklusive Lease ist Caller-Verantwortung; externe Writer/TOCTOU sind nicht ausgeschlossen.

3. **Paketierung (Komponente vorhanden):** `SicherungsarchivPaketierung` packt Snapshot und same-root-Inventardateien größenbegrenzt, schreibt striktes camelCase-v1-Manifest und prüft die Snapshot-Version. Prepared Roundtrip-Fakten sind kompiliert, Tests nicht ausgeführt.

4. **Workspace/Idempotenzvertrag (Komponenten vorhanden):** `Sicherungsarbeitsbereich` verwaltet Claims und private Stages; Contracts/Port binden Retry an Vorgangs-GUID und unveränderliche validierte Ausgabe.

5. **Erstellungskoordinator (Komponente vorhanden):** `DateisystemSicherungsarchivErstellung` koordiniert Snapshot, Inventar, Paketierung, vollständige ZIP-Inspektion, Hashbildung und atomare No-Overwrite-Veröffentlichung. Ein Retry derselben Operations-ID validiert die bestehende Datei und gibt deren unveränderliche Metadaten zurück.

Noch nicht vorhanden: API-/Service-DI-/Client-/UI-Anbindung, Retention und Restore. Die Bestandsliste enthält derzeit weiterhin nur Legacy-Sicherungen und beansprucht nicht, neue Vollarchive vollständig aufzulisten. Kein Schutz gegen externe Writer/TOCTOU; das ist Deployment-Voraussetzung. Prepared Tests sind nicht ausgeführt.
