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

Die neue API-WPF-Migrationsansicht kann mit folgendem Kommando gestartet werden:

```powershell
dotnet run --project src/AndyTours.App -c Release -- --api
```

Der normale Start ohne `--api` bleibt Legacy und unverändert. Für Touren-Schreibzugriffe verlangt die API vollständige PUT-Repräsentationen einschließlich aller Pflichtfelder (nullable Felder müssen als Schlüssel vorhanden sein, dürfen aber `null` sein); POST benötigt zusätzlich den `Idempotency-Key`-Header. Der API-Modus öffnet einen Verbindungsdialog für lokales oder zentrales Backend und danach eine Übersicht mit:

- Zeitraumfiltern, Raster- und Kennzahlauswahl
- vier Kennzahlen (Distanz, Höhenmeter, Fahrzeit, Anzahl)
- Aktivitätsstatistik-Diagramm samt Zukunftshinweis
- nur lesender Tourentabelle mit serverseitiger Seitennavigation

Die Ansicht deckt noch keine Schreibabläufe oder übrigen Fachmodule ab. Sie ist eine Migrationsansicht, keine abgeschlossene Migration.

## Funktionsinventar / Paritätsprüfung

Ein Bereich gilt erst dann als migriert, wenn Vertrag, Anwendungsfall und benutzbarer WPF-Clientablauf vorhanden sind. Ein API-Endpunkt allein bedeutet keine Funktionsparität.

| Bereich | Zu erhaltende Abläufe | Status |
|---|---|---|
| Start / Verbindung | lokal/zentral, Fehler- und Erreichbarkeitsanzeige | teilweise: `--api`-Start und Verbindungsdialog; kein Windows-Start-/Renderingtest |
| Touren | Lesen, Filtern, Sortieren, Anlegen, Bearbeiten, Kopieren, Löschen, Mehrfachbearbeitung | teilweise: API-CRUD mit Versionen; API-Ansicht liest und paginiert; Bearbeitungsabläufe noch offen |
| Aktivitätsstatistik | freie Datumsgrenzen, Kalender-/Kennzahlauswahl, Saisonziele, Bestleistungen | teilweise: Statistikroute/typisierter Client und API-Übersicht für Zeitraum, Raster, Kennzahl, KPIs und Diagramm; Saisonziele/Bestleistungen noch offen |
| Zuordnungen | Kategorien, Equipment, Aktivitäten, Trainingsbereiche, Zusammenführen | offen |
| Pulsauswertung | FIT-Messzeiten, aktuelle Garmin-Referenz mit Abrufdatum, Lückenkennzeichnung | offen |
| Tourdetail | Höhenprofil, Messreihen, Karte, Mehrtagestouren, Trackbearbeitung | offen |
| Garmin Connect | Key-Verwaltung ohne Rückgabe des Werts, 20er-Pakete, Hintergrundauftrag, Duplikatschutz | offen; separates Garmin-Key-/Secret-Management nicht in die API-Ansicht migriert |
| USB / FIT | lokaler Geräteadapter, Datei-Upload, Vorauswahl, Equipment-Zuordnung, atomarer Import | offen |
| Equipment | Stammdaten, Rad-/Sensorzuordnung, Ausmusterung, Einsatz/Haltbarkeit | offen |
| Wartung | Erfassen, Bearbeiten, Löschen, Alarme, Kilometer-/Zeitbezug | offen |
| Persönliche Daten | Fahrerprofil und zugehörige Werte | offen |
| Auswahllisten | Aktivitäten, Kategorien, Trainingsbereiche, Reihenfolgen, Zusammenführen | offen |
| Planung | Routing, Profile, Wegpunkte, Teilstücke, Abbiegehinweise, Speichern/Laden | offen |
| Courses / GPX | FIT-Course-Erstellung, GPX-Export, lokale Geräteübertragung | offen |
| Overlays | Ordnerbaum, GPX-/FIT-Inhalte, Farben, Ein-/Ausblenden, Verschieben/Löschen | offen |
| Einstellungen | Datenbestandseinstellungen getrennt von reinem Darstellungszustand | offen |
| Sicherung | Erstellen, Auflisten, Herunterladen, Wiederherstellen, atomarer Austausch | offen |
| Bestandsübernahme | SQLite und relative FIT-/Anhangpfade erhalten; explizit importieren, keine ungefragte Migration | offen |
| Altdatenwerkzeug | vorhandenes Entwicklerwerkzeug weiter kompatibel halten oder explizit ersetzen | offen |

Die vier Statistik-Regressionsfälle sind noch offen. Sieben SQLite-Tests wurden ergänzt und kompiliert, aber nicht ausgeführt. Tests bleiben durch den Auftraggeber auszuführen.

## Direkte Frontend-Abhängigkeiten

Inventar beim Branchstart (ohne `bin/obj`):
- 11 Dateien rufen `OeffneDatenbank.Ausfuehren` auf.
- 12 Dateien verwenden `SqliteConnection`.
- 4 Dateien verwenden FIT-Lesefunktionen.
- 2 Dateien verwenden `GarminPersonalClient`.
- Je 6 Dateien verwenden direkte File-/Directory-Zugriffe; Geräte- und bewusst lokale Export-/Downloadoperationen sind getrennt von Backendzugriffen zu bewerten.

Abschlussprüfung: Das produktive WPF-Frontend darf weder `AndyTours.Daten` noch `AndyTours.Import` bzw. `Microsoft.Data.Sqlite` referenzieren oder beliebige Server-Dateipfade/SQL-Aufträge über HTTP übertragen. Die Geräteadapter-Ausnahme gilt ausschließlich für USB/MTP und lokale Benutzerauswahl, nicht für Fachlogik.

## Backend-Konfiguration (Platzhalter)

Das Backend benötigt einen absoluten Datenordner und ein Geheimnis mit mindestens 32 Bytes. Werte sind ausschließlich in der Serverkonfiguration/Umgebung zu setzen, nicht in Repositorydateien:

```text
Backend__Datenordner=<ABSOLUTER_SERVERPFAD>
Backend__Zugriffstoken=<ZUFÄLLIGES_GEHEIMNIS_MIT_MINDESTENS_32_BYTES>
ASPNETCORE_URLS=<OPTIONALE_BINDUNG; Standard: http://localhost:5000>
```

Die API verlangt Bearer-Authentifizierung; Health ist anonym erreichbar. Nicht-loopback-Zugriffe sind nur über HTTPS zulässig. Das Zugriffstoken gewährt vollen Zugriff auf den konfigurierten Datenbestand; es gibt derzeit keine Mehrbenutzerverwaltung. Es ist kein Garmin-Schlüssel.

## Build, lokaler Start und Laufzeit

Voraussetzung für Source-Builds: Windows und ein zur `global.json` passendes .NET SDK (10.0.402 im verifizierten Testserver-Build). Unter Windows:

```powershell
dotnet build AndyTours.slnx -c Release
dotnet run --project src/AndyTours.App -c Release -- --api
```

Der Installer-Publish veröffentlicht App und API als self-contained `win-x64`; im Installer ist daher keine separate .NET Runtime erforderlich. In einem Source-Checkout startet die lokale API aus `src/AndyTours.Api/bin/{Debug|Release}/net10.0/AndyTours.Api.dll` und benötigt die passende .NET Runtime bzw. das SDK. Ein zentrales Backend kann auch dann gewählt werden, wenn die lokale Backenddatei fehlt.

Für ein zentrales Backend sind HTTPS und ein Zugriffstoken mit mindestens 32 Bytes erforderlich. Das Token ist datasetweit voll berechtigt. Das separat verwendete Garmin-Key-Secret ist nicht in die Frontendmigration einbezogen. Keine echten Tokens oder Schlüssel in Dokumentation oder Repository eintragen.

## Verifikation und verbleibende Lücken

- Testserver-Builds und Windows-`win-x64`-Cross-Publish von App und API waren erfolgreich.
- Der Installer (einschließlich ISCC) wurde nicht ausgeführt.
- Windows-Start, Dialogverbindung und Rendering der API-Ansicht wurden nicht getestet.
- Die sieben neuen SQLite-Tests wurden kompiliert, aber nicht ausgeführt; vier Statistik-Regressionsfälle fehlen noch.
- Keine vollständige Migration behaupten, bis alle offenen Paritätsbereiche umgesetzt und verifiziert sind.

Keine EXE-Dateien werden eingecheckt.
