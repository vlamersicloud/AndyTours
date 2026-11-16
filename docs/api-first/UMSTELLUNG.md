# API_FIRST – Umstellung mit schrittweiser Funktionsparität

## Gesicherter Ausgangspunkt

- Branch: `API_FIRST`, getrennt von `main`; Änderungen werden ausschließlich hier veröffentlicht.
- Keine kompilierten EXE-Dateien im Repository.
- WPF bleibt die Hauptoberfläche und wird schrittweise zum API-Client umgestellt.
- **Die Gesamtmigration ist nicht abgeschlossen.** Ohne `--api` startet weiterhin die bisherige Legacy-Oberfläche, damit vorhandene Abläufe erhalten bleiben.
- Lokales und zentrales Backend verwenden denselben Backendcode; bidirektionale Offline-Synchronisierung gibt es nicht.

## Architekturziel

- Contracts: versionierte Requests, Responses und Fehlerverträge, ohne SQLite-/WPF-Typen.
- Application/Core: Anwendungsfälle und Fachregeln hinter Ports, unabhängig von HTTP und Oberfläche.
- API: ASP.NET Core unter `/api/v1`, OpenAPI, ProblemDetails und Zugriffssicherung.
- Client: typisierte HTTP-Schnittstelle; keine SQL-Aufträge oder beliebigen Backend-Dateipfade über HTTP.
- Geräteadapter bleiben lokal auf USB-/MTP-Kommunikation beschränkt; Dateien werden über die API übertragen.
- Lokaler Betrieb startet einen eigenen Backendprozess mit lokaler Datenablage. Zentralbetrieb nutzt authentifizierte Clients und serverseitige Datenablage.
- Änderungen paralleler Clients dürfen nicht durch unversioniertes Vollbestands-Speichern überschrieben werden.

## Aktueller benutzbarer Einstieg

```powershell
dotnet run --project src/AndyTours.App -c Release -- --api
```

Der normale Start ohne `--api` bleibt Legacy. Der API-Modus bietet Verbindung lokal/zentral und eine Übersicht mit Zeitraum-, Aktivitäts- und Kategorienfiltern, serverseitig gefilterter Tourenliste mit Paging, Statistik, vier Kennzahlen und Diagramm. Touren können im API-Modus angelegt, bearbeitet, kopiert und gelöscht werden; Schreibvorgänge verwenden Versionsprüfung, und Neuanlagen unterstützen Idempotency-Keys. Liste und Statistik verwenden dieselben Datums-, Aktivitäts- und Kategorienfilter.

Garmin Connect ist im API-Modus über dauerhafte Downloadjobs, Paketresume, ZIP-Cache, serverseitige FIT-Prüfung, Equipmentauswahl und transaktionale Tour-/Garmin-Zuordnung einschließlich Review-UI angebunden. Der Schlüssel bleibt serverseitig in SQLite/Sicherungen und wird nicht an den Client zurückgegeben; kein Env-Fallback. USB/MTP und FIT-Upload sowie Puls-/Planungsintegration bleiben offen.

Über das Stammdatenmenü sind Aktivitätenverwaltung (sieben Abläufe) und Kategorienverwaltung (sechs Abläufe) in der UI angebunden. Bestleistungs- und Jahreszielprojektionen sind serverseitig verbunden; Rekord-Tournavigation und Zielverlaufsgrafik bleiben offen. Saisonzieleditor ist angebunden. Equipmentverwaltung ist mit Backend, API, typisiertem Client und UI für Anlegen, Bearbeiten, Löschen, Ausmustern, Standardkennzeichen und Entfernen gemerkter Gerätekennungen angebunden. Einsatz/Haltbarkeit und weitere Inventarabläufe, einschließlich Sortierung, sind noch offen.

## Funktionsinventar / Paritätsprüfung

Ein Bereich gilt erst dann als migriert, wenn Vertrag, Anwendungsfall und benutzbarer WPF-Clientablauf vorhanden sind. Ein API-Endpunkt allein bedeutet keine Funktionsparität.

| Bereich | Zu erhaltende Abläufe | Status |
|---|---|---|
| Start / Verbindung | lokal/zentral, Fehler- und Erreichbarkeitsanzeige | teilweise; kein Windows-Start-/Renderingtest |
| Touren | Lesen, Filtern, Sortieren, CRUD, Kopieren, Mehrfachbearbeitung | teilweise: serverseitige Filter/Paging und API-CRUD mit Versionen; Sortierung und Mehrfachbearbeitung offen |
| Aktivitätsstatistik | Datums-/Aktivitäts-/Kategorienfilter, Kennzahlen, Saisonziele, Bestleistungen | teilweise: Filter, KPIs und Diagramm; Bestleistungen und Kalenderjahresziel-Referenz serverseitig und in der UI angebunden; Rekorde respektieren Zeitraum-, Aktivitäts- und Kategorienfilter (Jahresrekorde bei Teilauswahl ausdrücklich keine vollen Jahreswerte); Ziele beziehen sich auf das volle Kalenderjahr, sind nicht proratiert und unabhängig vom Kategorienfilter; kein Fahrzeitziel. Saisonzieleditor angebunden. Tourendetail-Navigation für Rekorde und Zielverlauf/-diagramm offen |
| Aktivitäten | Stammdatenverwaltung | UI angebunden; sieben Abläufe vorhanden |
| Kategorien | Stammdatenverwaltung und Zusammenführen | UI angebunden; sechs Abläufe vorhanden |
| Zuordnungen / Inventar | Equipment, Trainingsbereiche, Reihenfolgen, Zusammenführen | teilweise; Equipment-CRUD und Stammdaten-UI angebunden; typisierte Trainingsbereichsliste angebunden; Einsatz/Haltbarkeit und Bulk-Sortierung offen |
| Pulsauswertung | FIT-Messzeiten, Garmin-Referenz, Lückenkennzeichnung | offen |
| Tourdetail | Höhenprofil, Messreihen, Karte, Mehrtagestouren, Trackbearbeitung | offen |
| Garmin Connect | Einstellungen/Key-Status, Abruf, Import, Duplikatschutz | teilweise: dauerhafte 20er-Downloadjobs, Resume, ZIP-Cache, serverseitige FIT-Prüfung/Hash, Equipmentauswahl, atomarer Tour-/Garmin-Mapping-Commit und Review-UI implementiert; Schlüssel verbleibt plaintext im Backend (SQLite/Backups), kein Env-Fallback; echte Laufzeit-/Garmin-Verifikation offen |
| USB / FIT | Geräteadapter, Upload, Vorauswahl, Equipment-Zuordnung, Import | offen |
| Equipment | Stammdaten, Rad-/Sensorzuordnung, Ausmusterung, Einsatz/Haltbarkeit | teilweise: Equipment-CRUD, Ausmustern, Standard und Entfernen gemerkter Kennungen im API-Modus; Einsatz/Haltbarkeit offen |
| Wartung | Erfassen, Bearbeiten, Löschen, Alarme | API und UI angebunden: serverseitige Kilometer-/Haltbarkeitsprojektionen, Alarmverwaltung und atomare Bulkaktionen; Startup-Fälligkeitswarnung und Tätigkeitslistenverwaltung angebunden; leere Wartungslisten konfigurierbar, keine Parität |
| Persönliche Daten | Fahrerprofil und zugehörige Werte | API-Lesen/Speichern mit CAS und UI angebunden; sieben Regressionstests bereitgestellt und kompiliert, nicht ausgeführt |
| Planung / Courses / GPX | Routing, Kurse, Export und Geräteübertragung | offen |
| Overlays | Ordner, Inhalte, Farben und Verwaltung | offen |
| Einstellungen / Sicherung | Einstellungen, Backup und Wiederherstellung | offen |
| Bestandsübernahme | SQLite- und Anhangpfade erhalten, expliziter Import | offen |
| Altdatenwerkzeug | kompatibel halten oder explizit ersetzen | offen |

Tests wurden bereitgestellt und kompiliert, aber nicht ausgeführt: sieben Touren-Store-, vier Statistik-, drei Idempotenz-, Aktivitäten-, Kategorien-, Filter- und XAML-Vertragstests. Diese Aufzählung ist keine Aussage über Gesamtparität; keine Gesamtzahl wird daraus abgeleitet. Tests sind durch den Auftraggeber auszuführen.

## Direkte Frontend-Abhängigkeiten

Der produktive WPF-Client darf weder `AndyTours.Daten` noch `AndyTours.Import` bzw. `Microsoft.Data.Sqlite` referenzieren oder beliebige Server-Dateipfade/SQL-Aufträge über HTTP übertragen. Die Geräteadapter-Ausnahme gilt ausschließlich für USB/MTP und lokale Benutzerauswahl, nicht für Fachlogik.

## Backend-Konfiguration (Platzhalter)

```text
Backend__Datenordner=<ABSOLUTER_SERVERPFAD>
Backend__Zugriffstoken=<ZUFÄLLIGES_GEHEIMNIS_MIT_MINDESTENS_32_BYTES>
ASPNETCORE_URLS=<OPTIONALE_BINDUNG; Standard: http://localhost:5000>
```

Die API verlangt Bearer-Authentifizierung; Health ist anonym erreichbar. Nicht-loopback-Zugriffe sind nur über HTTPS zulässig. Das Zugriffstoken gewährt datasetweit vollen Zugriff; es gibt keine Mehrbenutzerverwaltung. Keine echten Tokens oder Schlüssel in Repositorydateien eintragen.

## Build, lokaler Start und Laufzeit

Source-Builds benötigen ein zur `global.json` passendes .NET SDK (10.0.402 im Testserver-Build). Unter Windows:

```powershell
dotnet build AndyTours.slnx -c Release
dotnet run --project src/AndyTours.App -c Release -- --api
```

Der Installer-Publish ist self-contained `win-x64`; Source-Checkouts benötigen die passende .NET Runtime bzw. das SDK. Ein zentrales Backend kann auch gewählt werden, wenn die lokale Backenddatei fehlt.

## Verifikation und verbleibende Lücken

- Testserver-Builds und Windows-`win-x64`-Cross-Publish waren erfolgreich.
- Tests wurden kompiliert, nicht ausgeführt. Neue XAML-StaticResource-Verweise müssen eine definierte, erreichbare lokale Ressource besitzen; der Build allein validiert keine Laufzeitauflösung. Dafür sind strukturelle XAML-Tests bereitzustellen. Windows-Rendering und visuelle Bedienung wurden nicht geprüft.
- Installer/ISCC sowie Windows-Start, Dialogverbindung und UI-Rendering wurden nicht geprüft.
- Saisonziele- und Rekordprojektionstests wurden ergänzt/vorbereitet und kompiliert, aber nicht ausgeführt; dies ist kein Testergebnis.
- Jahreszielreferenzen sind volle Kalenderjahreswerte und werden nicht proportional auf den Filterzeitraum umgerechnet.
- Statistikdiagramm-Achsen zeigen bei langen Zeiträumen höchstens ungefähr zwölf Beschriftungen. Ein Tracker mit Zeitraum, Wert und Einheit ist noch offen; das verfügbare Paket-XML bestätigt die Formatstring-Eigenschaft, aber nicht zuverlässig die Platzhalter für Rechteckbalken.
- Keine vollständige Migration behaupten, bis alle offenen Paritätsbereiche umgesetzt und verifiziert sind.

Keine EXE-Dateien einchecken.
