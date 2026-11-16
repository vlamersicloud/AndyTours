# Garmin Connect Import im API-Modus

**Status: integrierter, aber nicht vollständiger E2E-Ablauf.** API-Modus unterstützt dauerhafte Downloadaufträge, automatische/manuelle Paketfortsetzung, Abbruch und Resume, ZIP-Cache, serverseitige FIT-Prüfung, Equipmentvorschlag/-auswahl und atomare Tourübernahme mit Garmin-Zuordnung, Sensormerkungen und Idempotenzquittung. WPF-Jobstatus und synchrones Review-/Übernahmefenster sind angebunden. Der Legacy-Start ohne `--api` bleibt Standard. USB/MTP, lokaler FIT-Upload und weitere Garmin-/Tourdetails bleiben offen.

Der Garmin Personal-Key bleibt in `Persoenlich.Garmin.PersonalApiKey`; kein Environment-Fallback. Schlüssel, Tokens, Rohantworten, Serverpfade und Exceptiontexte gehören weder in öffentliche DTOs noch Logs/Fehlermeldungen.

## Implementiert

- Contracts in `src/AndyTours.Contracts/V1/GarminImportVertrag.cs` und `GarminImportPruefungVertrag.cs`; Job-, Prüf- und Übernahmeports in Application.
- SQLite-Jobstore mit idempotentem Start/Mutation, Lease-Fencing, bis zu 20 Kandidaten je Paket, Paketstatus und sicheren Quittungen.
- API-gehosteter sequenzieller Worker: Garmin-ID-Auflistung, Resume, ZIP-Cache und FIT-Persistenz unter festem Datenordner; keine Worker-Leases oder Schlüssel als HTTP.
- `GarminImportPruefung` lädt FIT-Dateien serverseitig aus festem Job-/Kandidatenpfad, erstellt isolierte Prüfsnapshots, berechnet Hashes und liefert Preview-DTOs. Der Client übermittelt bei Übernahme nur Auswahl, Equipment und die beobachteten Versionswerte, keine Tourdaten oder Dateipfade.
- Übernahme prüft Versionen erneut, schreibt Touren, Anhänge, Garmin-Zuordnung, Equipment-/Sensorupdates, Kandidatenstatus und Requestquittung in derselben SQLite-Transaktion. Gleiches Request-Replay liefert die gespeicherte Antwort; Duplikate werden erkannt.
- API-Routen `/{jobId}/pruefung` und `/{jobId}/uebernehmen`; typed client verwendet getrennten 10-Minuten-Timeout für FIT-Review; übrige API-Calls behalten ihren kurzen Timeout.
- WPF-Jobfenster und synchrones Review-/Auswahlfenster: unmarkierte Kandidaten werden übersprungen, Originaldateien bleiben bestehen, Fensterabbruch löst keinen serverseitigen Jobabbruch aus.

## Weiterhin offen / Grenzen

Kein vollständiger Abschluss der API-Migration: USB/MTP-Geräteadapter, FIT-Datei-Upload, vollständige Tourdetails (u.a. Puls-/Planungsintegration), Backup-/Restore- und breitere Recovery-/Mehrclient-E2E-Absicherung bleiben offen. Der Legacypfad bleibt verfügbar und Standard.

## Verifikation

Release-Builds für API, Client, WPF und Testprojekt wurden kompiliert; Tests wurden **nie ausgeführt**. Es fanden keine echten Garmin-, API- oder UI-Läufe statt. ZIP/FIT-Testinhalte und Datenbanktests verwenden synthetische Daten/Fake-Schlüssel; Garmin-Netztests sind nicht vorgesehen. Die Build-/Compile-Aussage ist kein Nachweis eines durchgeklickten Produktablaufs.
