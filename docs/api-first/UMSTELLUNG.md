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

Der normale Start ohne `--api` bleibt Legacy. Der API-Modus bietet Verbindung lokal/zentral und eine Übersicht mit Zeitraum-, Aktivitäts- und Kategorienfiltern, serverseitig gefilterter Tourenliste mit Paging, Statistik, vier Kennzahlen und Diagramm. Touren können im API-Modus angelegt, bearbeitet, kopiert und gelöscht werden; Schreibvorgänge verwenden Versionsprüfung, und Neuanlagen unterstützen Idempotency-Keys. Vier atomare versionierte Mehrfachaktionen (Kategorien, Aktivität, Equipment, Löschen) mit Replay-Beleg, expliziter WPF-Bestätigung und identischer Anfrage bei manuellem Retry sind angebunden; bestätigte Aktionen werden nach Refreshfehler nicht wiederholt. Die Auswahl umfasst nur Zeilen der aktuellen Seite; single edit/copy erfordert genau eine Zeile. Die Tourentabelle bietet sieben serverseitige Text-/Bereichsfilter mit explizitem Anwenden/Zurücksetzen und validierten Lookup-Optionen; Tabellenfilter ändern die Kennzahlen und Rekord-/Pulsdaten nicht. Alle drei Listenabrufe übernehmen Filter und Optionen vor dem UI-Commit atomar. Die Sortierung der 18 API-Anzeigespalten erfolgt mit endlichen typisierten Schlüsseln serverseitig vor dem Paging; Legacy-Initialsortierung und bestehender API-Default bleiben erhalten. Mehrfachaktionen sind auf aktuell geladene Zeilen begrenzt; Auswahl über Seiten/alle Filtertreffer und Inline-Bearbeitung bleiben offen. Das API-Spaltenlayout ist über typisierten CAS-Port, Legacy-Raw-Kompatibilität und WPF-Spaltenauswahl/Reihenfolge/Breite angebunden; API, Windows-App und Testprojekt kompilieren warnungsfrei. Tests, Windows-Bedienung und Mehrclient-Laufzeitprüfung bleiben offen.

Garmin Connect ist im API-Modus über dauerhafte Downloadjobs, Paketresume, ZIP-Cache, serverseitige FIT-Prüfung, Equipmentauswahl und transaktionale Tour-/Garmin-Zuordnung einschließlich Review-UI angebunden. Der Schlüssel bleibt serverseitig in SQLite/Sicherungen und wird nicht an den Client zurückgegeben; kein Env-Fallback. FIT-Upload und lokale USB/MTP-Kopieradapter sind über die API mit Prüfung und atomarer Übernahme angebunden; echte Geräte-/Windows-Laufzeitprüfung bleibt offen. Die API-Planung bietet Routing, versionsgeschütztes Profil, lokale GPX-Dateiabläufe, geplanten Course-Export und gemeinsamen Overlaykontext; Routingkonfiguration ist über Datei → Routing-Einstellungen im API-Modus verfügbar, Windows-/Provider-/Geräte-Laufzeitprüfung und die Ausführung der vorbereiteten FIT-Zeitstempelassertions ist offen. Pulsauswertung ist über Backend, FIT-Messzeit, aktuelle Garmin-Referenz mit CAS, expliziten Abruf, typisierten Client und WPF-Tab verbunden.

Die optionale Konfiguration `Backend__FitZeitzone=<OS_ZONE_ID>` bestimmt die Fallback-Zeitzone; ohne Angabe gilt die Systemzeitzone des Backends. Die lokale WPF-Anwendung übermittelt ihre lokale Zone ausdrücklich. Eine ungültige konfigurierte Zone verhindert den Backendstart. Explizite lokale FIT-Zeitstempel haben Vorrang. Die Review zeigt die Fallback-Zone vor der Bestätigung; der Hash aus Dateiinhalt und Zeitzonenregeln macht eine alte Preview bei geänderter Interpretation ungültig. Bestehende Tourdaten werden nicht umgerechnet oder umgeschrieben.

Über das Stammdatenmenü sind Aktivitätenverwaltung (sieben Abläufe) und Kategorienverwaltung (sechs Abläufe) in der UI angebunden. Bestleistungs- und Jahreszielprojektionen sind serverseitig verbunden; Rekord-Tournavigation über die exakte optionale TourId ist angebunden. Der optionale serverseitige Saisonfortschritt erscheint für exakt 1. Januar bis Stichtag in vier Kennzahlen; andere Zeiträume und ältere Backends ohne Feld bleiben leer. Kategorien filtern Ist-Werte, nicht Ziele; Tabellenfilter wirken nicht. Der bestehende Periodenverlauf bleibt unverändert; eine historische Zielkurve ist nicht belegt und bleibt offen. Tests kompiliert, nicht ausgeführt; Windows-Rendering und Gesamtmigration offen. Saisonzieleditor ist angebunden. Equipmentverwaltung ist mit Backend, API, typisiertem Client und UI für Anlegen, Bearbeiten, Löschen, Ausmustern, Standardkennzeichen und Entfernen gemerkter Gerätekennungen angebunden. Der ungefilterte Equipment-Einsatzbericht ist als read-only API-Projektion mit Client und Modal unter Extras angebunden. Die Wartungsansicht zeigt bereits serverseitige Haltbarkeit (km-Stand, Kilometer und Tage seit letzter gleichartiger Wartung); weitere Inventarabläufe einschließlich Sortierung bleiben offen.

## Funktionsinventar / Paritätsprüfung

Ein Bereich gilt erst dann als migriert, wenn Vertrag, Anwendungsfall und benutzbarer WPF-Clientablauf vorhanden sind. Ein API-Endpunkt allein bedeutet keine Funktionsparität.

| Bereich | Zu erhaltende Abläufe | Status |
|---|---|---|
| Start / Verbindung | lokal/zentral, Fehler- und Erreichbarkeitsanzeige | teilweise; kein Windows-Start-/Renderingtest |
| Touren | Lesen, Filtern, Sortieren, CRUD, Kopieren, Mehrfachbearbeitung | teilweise: serverseitige Filter/Paging; sieben Tabellenfilter mit explizitem Anwenden/Zurücksetzen, per-bound Fehleranzeigen und Lookup-Optionen aus der Datums-/Aktivitätsbasis angebunden; Tabellenfilter verändern Kennzahlen, Rekorde und Puls nicht; Antworten werden vor UI-Übernahme geprüft; typisierte globale Sortierung vor Paging für 18 schreibgeschützte Anzeigespalten; API-CRUD mit Versionen und vier atomare CAS-/Replay-Mehrfachaktionen samt WPF-Auswahl auf aktueller Seite, Aktionsdialog und sicherem Retry; Inline-Bearbeitung, Spaltenausblendung/-reihenfolge/-breite, Auswahl über alle Seiten/off-page, Windows-Rendering und Mehrclient-Laufzeitprüfung offen; Teststatus: siehe [Verifikation](VERIFIKATION.md) |
| Aktivitätsstatistik | Datums-/Aktivitäts-/Kategorienfilter, Kennzahlen, Saisonziele, Bestleistungen | teilweise: Filter, KPIs und Periodendiagramm; Bestleistungen und Kalenderjahresziel-Referenz serverseitig und in der UI angebunden; Rekorde respektieren Zeitraum-, Aktivitäts- und Kategorienfilter (Jahresrekorde bei Teilauswahl ausdrücklich keine vollen Jahreswerte); Ziele beziehen sich auf das volle Kalenderjahr, sind nicht proratiert und unabhängig vom Kategorienfilter; kein Fahrzeitziel. Optionaler serverseitiger YTD-Saisonfortschritt für exakt 1. Januar bis Stichtag in vier Kennzahlen; andere Zeiträume/ältere Backends ohne Feld bleiben leer, Kategorien begrenzen Ist-Werte nicht Ziele, Tabellenfilter wirken nicht. Saisonzieleditor und Rekord-Tournavigation über exakte optionale TourId angebunden; historische Zielkurve nicht belegt/offen; Tests kompiliert, nicht ausgeführt; Windows-Rendering ungeprüft |
| Aktivitäten | Stammdatenverwaltung | UI angebunden; sieben Abläufe vorhanden |
| Kategorien | Stammdatenverwaltung und Zusammenführen | UI angebunden; sechs Abläufe vorhanden |
| Zuordnungen / Inventar | Equipment, Trainingsbereiche, Reihenfolgen, Zusammenführen | teilweise; Equipment-CRUD und Stammdaten-UI angebunden; typisierte Trainingsbereichsliste angebunden; ungefilterter read-only Einsatzbericht mit Modal und lokaler Sortierung angebunden; serverseitige Wartungs-Haltbarkeitswerte in der Wartungsansicht sichtbar; weitere Abläufe und Bulk-Sortierung offen |
| Pulsauswertung | FIT-Messzeiten, Garmin-Referenz, Lückenkennzeichnung | verbunden: Backend/FIT-Messzeit, aktuelle Referenz mit CAS, expliziter Garmin-Abruf, typisierter Client und WPF-Tab; keine historischen Zonen/Interpolation, Serverperiodenschlüssel und gemessene Zeitanteile. Tests vorbereitet und kompiliert, nicht ausgeführt; Windows-/Laufzeitprüfung offen |
| Tourdetail | Höhenprofil, Messreihen, Karte, Mehrtagestouren, Trackbearbeitung | teilweise: serverseitige FIT-Messwerte und Mehrfach-Etappen mit Diagrammen, Karte und Trackbearbeitung angebunden; GPX und Backend-Course-Export angebunden; API-Overlaykontext an Tourdetailkarte angeschlossen; Laufzeitabnahme offen |
| Garmin Connect | Einstellungen/Key-Status, Abruf, Import, Duplikatschutz | teilweise: dauerhafte 20er-Downloadjobs, Resume, ZIP-Cache, serverseitige FIT-Prüfung/Hash, Equipmentauswahl, atomarer Tour-/Garmin-Mapping-Commit und Review-UI implementiert; Schlüssel verbleibt plaintext im Backend (SQLite/Backups), kein Env-Fallback; echte Laufzeit-/Garmin-Verifikation offen |
| USB / FIT | Geräteadapter, Upload, Vorauswahl, Equipment-Zuordnung, Import | teilweise: Upload, serverseitige FIT-Prüfung, Equipmentwahl, atomare Übernahme, Wiederladen gespeicherter Uploads und lokale USB/MTP-Kopie angebunden; Laufzeitabnahme offen |
| Equipment | Stammdaten, Rad-/Sensorzuordnung, Ausmusterung, Einsatz/Haltbarkeit | teilweise: Equipment-CRUD, Ausmustern, Standard und Entfernen gemerkter Kennungen im API-Modus; ungefilterter read-only Einsatzbericht und serverseitige Haltbarkeitswerte in der Wartungsansicht angebunden; XAML-/Clienttests kompiliert, nicht ausgeführt; Windows-Rendering ungeprüft |
| Wartung | Erfassen, Bearbeiten, Löschen, Alarme | API und UI angebunden: serverseitige Kilometer-/Haltbarkeitsprojektionen, Alarmverwaltung und atomare Bulkaktionen; Startup-Fälligkeitswarnung und Tätigkeitslistenverwaltung angebunden; leere Wartungslisten konfigurierbar, keine Parität |
| Persönliche Daten | Fahrerprofil und zugehörige Werte | API-Lesen/Speichern mit CAS und UI angebunden; sieben Regressionstests bereitgestellt und kompiliert, nicht ausgeführt |
| Planung / Courses / GPX | Routing, Kurse, Export und Geräteübertragung | teilweise: API-Routing, CAS-Fahrprofil, lokale GPX-Öffnen/Speichern, geplanter Backend-Course-Export und USB/MTP-Geräteadapter angebunden; gemeinsamer Overlaykontext und Startausschnitt aus lokalen Grenzen bzw. bis zu fünf vorhandenen FIT-GPS-Tracks; Routingkonfiguration ist über Datei → Routing-Einstellungen im API-Modus verfügbar; Windows-/Provider-/Geräte-Laufzeitprüfung offen |
| Overlays | Ordner, Inhalte, Farben und Verwaltung | API-Client, per-Root-Host/Manager, Tree-/Farben-/Sichtbarkeits-/Move-/Zoom- und GPX-Queue-Flows angeschlossen; Windows-/Kartenlaufzeit und vollständige Migrationsabnahme offen |
| Einstellungen / Sicherung | Einstellungen, Backup und Wiederherstellung | teilweise: versionierte Sicherungseinstellungen, Vollarchiv-Erstellung, validierter gemischter Katalog und nichtdestruktive Metadatenvorschau mit API-Client/UI angebunden; Download, Starttrigger, Retention/Löschpolicy und tatsächliches Anwenden samt Betriebs-Guard, Staging, Vorab-Sicherung, Bestätigung und Recovery offen; Produktionslimits nicht gesetzt, keine Runtime-/Windows-Verifikation. Details: [Sicherungseinstellungen](EINSTELLUNGEN-SICHERUNG.md), [Vollarchiv-Katalog](VOLLARCHIV-KATALOG.md), [Wiederherstellungsvorschau](WIEDERHERSTELLUNGSVORSCHAU.md) |
| Bestandsübernahme | SQLite- und Anhangpfade erhalten, expliziter Import | offen |
| Altdatenwerkzeug | kompatibel halten oder explizit ersetzen | offen |

Für mehrere Bereiche wurden Tests vorbereitet und kompiliert; das ist kein vollständiger Suite-Nachweis. Am 16.11.2026 wurde versehentlich ein vollständiger Testlauf gestartet; die Ausgabe ist abgeschnitten und zeigt unter anderem Fehler bei Handler-Eigentumsannahmen sowie zwei Analyzer-Warnungen. Die betroffenen Quellen wurden anschließend korrigiert und neu gebaut, aber nicht erneut ausgeführt. Es liegt daher kein vollständiges grünes Testergebnis vor. Details: [Verifikation](VERIFIKATION.md). Die Tests sind durch den Auftraggeber auszuführen.

## Direkte Frontend-Abhängigkeiten

Der produktive WPF-Client darf weder `AndyTours.Daten` noch `AndyTours.Import` bzw. `Microsoft.Data.Sqlite` referenzieren oder beliebige Server-Dateipfade/SQL-Aufträge über HTTP übertragen. Die Geräteadapter-Ausnahme gilt ausschließlich für USB/MTP und lokale Benutzerauswahl, nicht für Fachlogik.

## Backend-Konfiguration (Platzhalter)

```text
Backend__Datenordner=<ABSOLUTER_SERVERPFAD>
Backend__FitZeitzone=<OS_ZONE_ID; optional, Standard: Backend-Systemzeitzone>
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

- Testserver-Builds und Windows-`win-x64`-Cross-Publish waren für einen früheren Stand vor den aktuellen Garmin-Änderungen erfolgreich; das belegt weder deren aktuellen Cross-Publish noch Laufzeitverhalten. Für die aktuellen Garmin-Änderungen liegt Compile-Evidenz vor.
- Zum versehentlichen Testlauf und zur begrenzten Ausgabe siehe [Verifikation](VERIFIKATION.md); daraus folgt kein vollständiges Suite-Ergebnis. Neue XAML-StaticResource-Verweise müssen eine definierte, erreichbare lokale Ressource besitzen; der Build allein validiert keine Laufzeitauflösung. Dafür sind strukturelle XAML-Tests bereitzustellen. Windows-Rendering und visuelle Bedienung wurden nicht geprüft.
- Installer/ISCC sowie Windows-Start, Dialogverbindung und UI-Rendering wurden nicht geprüft.
- Saisonziele- und Rekordprojektionstests wurden ergänzt/vorbereitet und kompiliert; die Verifikation des vollständigen Teststatus steht in [Verifikation](VERIFIKATION.md).
- Jahreszielreferenzen sind volle Kalenderjahreswerte und werden nicht proportional auf den Filterzeitraum umgerechnet.
- Statistikdiagramm-Achsen zeigen bei langen Zeiträumen höchstens ungefähr zwölf Beschriftungen. Ein Tracker mit Zeitraum, Wert und Einheit ist noch offen; das verfügbare Paket-XML bestätigt die Formatstring-Eigenschaft, aber nicht zuverlässig die Platzhalter für Rechteckbalken.
- Keine vollständige Migration behaupten, bis alle offenen Paritätsbereiche umgesetzt und verifiziert sind.

Keine EXE-Dateien einchecken.
