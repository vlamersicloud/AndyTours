# Garmin Connect Import im API-Modus

**Status: integrierter, aber nicht vollständig verifizierter API-E2E-Ablauf; Gesamtmigration offen.** Dauerhafte Downloadjobs, Resume, ZIP-Cache, serverseitige FIT-Prüfung, Equipmentauswahl, atomarer Tourcommit mit Garmin-Zuordnung/Sensorupdates und WPF-Review sind implementiert. USB/MTP, FIT-Upload, vollständige Tourdetails und weitere Produktabläufe bleiben offen. Legacy ohne `--api` bleibt Standard.

Der Garmin Personal-Key bleibt in der bestehenden Backend-Einstellung `Persoenlich.Garmin.PersonalApiKey`; kein Environment-Fallback. Schlüssel, Tokens, Garmin-Rohantworten, Dateipfade und Exceptiontexte gehören weder in Jobzeilen/API-Antworten noch in Logs oder Fehlertexte.

Die optionale Konfiguration `Backend__FitZeitzone=<OS_ZONE_ID>` verwendet ohne Angabe die Systemzeitzone des Backends; die lokale WPF-Anwendung übermittelt ihre lokale Zone ausdrücklich. Eine ungültige Zone verhindert den Backendstart. Explizite lokale FIT-Zeitstempel haben Vorrang. Die Review zeigt die Fallback-Zone vor der Bestätigung; der Inhalts-/Zeitzonenregel-Hash macht veraltete Preview-Interpretationen ungültig. Bestehende Tourdaten werden nicht umgerechnet oder umgeschrieben.

## Implementiert

- Verträge in `src/AndyTours.Contracts/V1/GarminImportVertrag.cs` und Jobport `src/AndyTours.Application/Garmin/IGarminImportJobs.cs`: idempotenter Start, Status, Kandidatenseiten, Fortsetzen und Abbrechen.
- SQLite-Schema `src/AndyTours.Daten/GarminImportJobSchema.cs` mit Job-, Kandidaten- und sicheren Steuerungsquittungen. `SqliteGarminImportJobs*.cs` enthält Start/Lesen/Paging, versionierte Mutationen, eindeutige aktive Jobs, Leases mit Owner/Generation/Expiry-Fencing, Kandidatenpersistenz (20 pro Paket) und Paketübergänge. Paketbeobachtungen tragen die dauerhafte Paketnummer.
- Serverdatenport `src/AndyTours.Application/Garmin/IGarminImportServerdaten.cs` und SQLite-Adapter `src/AndyTours.Daten/SqliteGarminImportServerdaten.cs` lesen den konfigurierten Schlüssel nur intern und berücksichtigen bestehende Garmin-Zuordnungen nur für noch vorhandene Touren.
- `src/AndyTours.Import/Garmin/GarminImportArtefakte.cs` speichert ZIPs unter dem festgelegten Datenordner mit temporärer Datei und atomarem Move; FIT-Dateien werden mit dem vorhandenen ZIP-Entpacker und `LeseFitAufzeichnung` geprüft. ZIP-Cache und FIT-Pfade werden nicht über HTTP ausgegeben.
- `src/AndyTours.Import/Garmin/GarminImportPaketWorker.cs` claimt Pakete, erneuert Leases, setzt Kandidaten fort, enumeriert Garmin-IDs ab Anfang und speichert Kandidaten vor Netzwerkdownloads. `src/AndyTours.Api/GarminImportDienst.cs` hosted den sequenziellen Worker. API-DI teilt dieselbe SQLite-Jobstoreinstanz für Job- und Worker-Port.
- `src/AndyTours.Api/GarminImportEndpunkte.cs` bietet Start, Status, Kandidatenseiten, Fortsetzen und Abbrechen. Worker-Leases und Schlüsselzugriff sind nicht als HTTP-Endpunkte gemappt.
- `src/AndyTours.Client/GarminImportJobsHttpClient.cs` implementiert den typisierten Job-Client. `GarminImportPruefungHttpClient` nutzt einen separaten längeren Timeout für FIT-Review.
- `src/AndyTours.Import/Garmin/GarminImportPruefung*.cs` baut sichere Preview-DTOs aus serverseitigen FIT-Snapshots und legt geprüfte Dateien unveränderlich ab. `SqliteGarminImportJobs.Uebernahme.cs` übernimmt Touren/Anhänge, Garmin-Mapping, Sensor-/Equipmentupdates, Kandidaten und Idempotenzquittung in einer SQLite-Transaktion.
- WPF-Jobstatus und synchrones Review-/Auswahlfenster sind verbunden; Fensterabbruch bricht den serverseitigen Downloadauftrag nicht ab.

## Noch offen

- Kein vollständiger Abschluss der Migration: USB/MTP und FIT-Upload, vollständige Tourdetail-/Puls-/Planungsintegration, Backup-/Restore-/Crash-Recovery und breite Mehrclient-E2E-Verifikation bleiben offen.
- Kein vollständiger Reimport-/Crash-Recovery-Nachweis über Tourcommit und Garmin-Zuordnung hinweg.
- Die vorbereiteten SQLite- und Worker-Tests wurden kompiliert, aber **nie ausgeführt**. Es gab keine echten Garmin-Zugangsdaten oder Netzwerkaufrufe.

## Architektur und nächste Scheibe

Data bleibt unabhängig von Import; SQLite speichert dauerhafte Zustände und fenced Claims. Import/Application orchestriert Garmin-Netzwerkzugriff und FIT-Parsing; API hostet den Worker. Es werden keine Redis-, Cloud- oder sonstigen neuen Infrastrukturkomponenten eingeführt. Der bestehende `GarminImportZuordnung`-Doppelimportschutz und die Aufbewahrung der Original-ZIPs sollen erhalten bleiben; nach Tourlöschung sollen Zuordnungen zu nicht mehr existierenden Touren weiterhin ignoriert werden.

Der Download-/Review-/Übernahmeablauf ist implementiert, aber weder echte Garmin/API-Aufrufe noch UI-Durchklick wurden ausgeführt. Nicht als vollständige API-First-Migration oder Laufzeitverifikation ausgeben; Legacy bleibt Standard.

## Tests

Worker-Tests verwenden synthetische FIT-Dateien und einen Fake-HTTP-Handler; SQLite-Tests verwenden ausschließlich Fake-Schlüssel. Sie sind vorbereitet/kompiliert, **nicht ausgeführt**. Noch erforderlich sind insbesondere echte Übergangs-/Restart-Tests für Review und atomaren Import, Duplikat-/Mehrclient-Szenarien sowie Secret-Sentinel-Prüfungen für API, Logs und Fehlerantworten. Garmin-Netztests sollen nicht verwendet werden.
