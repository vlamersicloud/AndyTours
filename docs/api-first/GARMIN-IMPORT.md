# Garmin Connect Import im API-Modus

**Status: Teilimplementierung; noch kein vollständiger nutzbarer Importablauf.** Vorhanden sind dauerhafte Job-/Kandidatenverwaltung, API-Jobaktionen und ein serverseitiger Download-/FIT-Vorbereitungsworker. Review, Equipmentauswahl, atomarer Tourimport und WPF-Joboberfläche fehlen. Der Legacy-Start ohne `--api` bleibt Standard. Es wurden keine echten Garmin-Aufrufe ausgeführt.

Der Garmin Personal-Key bleibt in der bestehenden Backend-Einstellung `Persoenlich.Garmin.PersonalApiKey`; kein Environment-Fallback. Schlüssel, Tokens, Garmin-Rohantworten, Dateipfade und Exceptiontexte gehören weder in Jobzeilen/API-Antworten noch in Logs oder Fehlertexte.

## Implementiert

- Verträge in `src/AndyTours.Contracts/V1/GarminImportVertrag.cs` und Jobport `src/AndyTours.Application/Garmin/IGarminImportJobs.cs`: idempotenter Start, Status, Kandidatenseiten, Fortsetzen und Abbrechen.
- SQLite-Schema `src/AndyTours.Daten/GarminImportJobSchema.cs` mit Job-, Kandidaten- und sicheren Steuerungsquittungen. `SqliteGarminImportJobs*.cs` enthält Start/Lesen/Paging, versionierte Mutationen, eindeutige aktive Jobs, Leases mit Owner/Generation/Expiry-Fencing, Kandidatenpersistenz (20 pro Paket) und Paketübergänge. Paketbeobachtungen tragen die dauerhafte Paketnummer.
- Serverdatenport `src/AndyTours.Application/Garmin/IGarminImportServerdaten.cs` und SQLite-Adapter `src/AndyTours.Daten/SqliteGarminImportServerdaten.cs` lesen den konfigurierten Schlüssel nur intern und berücksichtigen bestehende Garmin-Zuordnungen nur für noch vorhandene Touren.
- `src/AndyTours.Import/Garmin/GarminImportArtefakte.cs` speichert ZIPs unter dem festgelegten Datenordner mit temporärer Datei und atomarem Move; FIT-Dateien werden mit dem vorhandenen ZIP-Entpacker und `LeseFitAufzeichnung` geprüft. ZIP-Cache und FIT-Pfade werden nicht über HTTP ausgegeben.
- `src/AndyTours.Import/Garmin/GarminImportPaketWorker.cs` claimt Pakete, erneuert Leases, setzt Kandidaten fort, enumeriert Garmin-IDs ab Anfang und speichert Kandidaten vor Netzwerkdownloads. `src/AndyTours.Api/GarminImportDienst.cs` hosted den sequenziellen Worker. API-DI teilt dieselbe SQLite-Jobstoreinstanz für Job- und Worker-Port.
- `src/AndyTours.Api/GarminImportEndpunkte.cs` bietet Start, Status, Kandidatenseiten, Fortsetzen und Abbrechen. Worker-Leases und Schlüsselzugriff sind nicht als HTTP-Endpunkte gemappt.
- `src/AndyTours.Client/GarminImportJobsHttpClient.cs` implementiert den typisierten Job-Client über die gemeinsame Backend-HTTP-Sitzung.

## Noch offen

- Das WPF-Jobstatusfenster und ViewModel sind angebunden; FIT-Review und Tourimport bleiben offen.
- Keine Review-/Import-Commit-Verträge oder Equipmentauswahl.
- Keine atomare Erstellung einer Trainingseinheit samt `GarminImportZuordnung.Speichern` innerhalb derselben SQLite-Transaktion; Kandidaten werden daher noch nicht als Touren importiert.
- Kein vollständiger Reimport-/Crash-Recovery-Nachweis über Tourcommit und Garmin-Zuordnung hinweg.
- Die vorbereiteten SQLite- und Worker-Tests wurden kompiliert, aber **nie ausgeführt**. Es gab keine echten Garmin-Zugangsdaten oder Netzwerkaufrufe.

## Architektur und nächste Scheibe

Data bleibt unabhängig von Import; SQLite speichert dauerhafte Zustände und fenced Claims. Import/Application orchestriert Garmin-Netzwerkzugriff und FIT-Parsing; API hostet den Worker. Es werden keine Redis-, Cloud- oder sonstigen neuen Infrastrukturkomponenten eingeführt. Der bestehende `GarminImportZuordnung`-Doppelimportschutz und die Aufbewahrung der Original-ZIPs sollen erhalten bleiben; nach Tourlöschung sollen Zuordnungen zu nicht mehr existierenden Touren weiterhin ignoriert werden.

Nächster Schritt: Review-DTOs/Port für explizite FIT-Kandidatenauswahl und Equipment festlegen, FIT serverseitig erneut validieren und Tourinsert plus Garmin-ID-Zuordnung atomar implementieren. Danach API-Reviewaktionen und WPF-Joboberfläche ergänzen. Bis diese Scheibe fertig ist, ist der Garmin-Import im API-Modus ausdrücklich nicht als vollständiger Nutzerablauf verfügbar.

## Tests

Worker-Tests verwenden synthetische FIT-Dateien und einen Fake-HTTP-Handler; SQLite-Tests verwenden ausschließlich Fake-Schlüssel. Sie sind vorbereitet/kompiliert, **nicht ausgeführt**. Noch erforderlich sind insbesondere echte Übergangs-/Restart-Tests für Review und atomaren Import, Duplikat-/Mehrclient-Szenarien sowie Secret-Sentinel-Prüfungen für API, Logs und Fehlerantworten. Garmin-Netztests sollen nicht verwendet werden.
