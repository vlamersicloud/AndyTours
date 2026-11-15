# API_FIRST – vollständige Umstellung

## Gesicherter Ausgangspunkt

- Branch: API_FIRST, ab main / 89473998afba8d3c950193eed3f776c79be4d0cf.
- Der Branch wurde separat auf origin veröffentlicht; main wird nicht verändert.
- Keine kompilierten EXE-Dateien im Repository.
- Beschlossen: lokales und zentrales Backend, zunächst keine bidirektionale Offline-Synchronisierung.
- Wahl der Hauptoberfläche (Web oder modernisierte WPF-App) ist noch offen.

## Architekturziel

- Contracts: versionierte Requests, Responses und Fehlerverträge, keine SQLite-/WPF-Typen.
- Application: Anwendungsfälle, Transaktionsgrenzen, Validierung, Import-/Analyseaufträge.
- Core: vorhandene Fachregeln und Berechnungen, unabhängig von HTTP und Oberfläche.
- Infrastructure: SQLite, Dateiablage, FIT, Garmin, Routing und Sicherungen nur hinter Application-Schnittstellen.
- API: ASP.NET Core, /api/v1, OpenAPI, strukturierte Fehler, explizite Zugriffssicherung.
- Client: typisierte HTTP-Schnittstelle; kein SQL, keine Backend-Dateipfade oder Garmin-Secrets im Frontend.
- Geräteadapter: nur lokale USB-/MTP-Kommunikation, Datei-Upload/Download über die API.
- Lokaler Betrieb: derselbe Backend-Code mit eigener lokaler Datenablage.
- Zentraler Betrieb: derselbe Backend-Code mit serverseitiger Datenablage und authentifizierten Clients.
- Änderungen an parallelen Clients dürfen nicht durch unversioniertes Vollbestands-Speichern überschrieben werden.

## Funktionsinventar / Paritätsprüfung

Jeder Punkt benötigt einen fachlichen API-Vertrag, einen Anwendungsfall und einen
benutzbaren Clientablauf. Ein angelegter Endpunkt allein bedeutet nicht migriert.

| Bereich | Zu erhaltende Abläufe | Status |
|---|---|---|
| Start / Verbindung | lokales/zentral konfiguriertes Backend, Fehler-/Erreichbarkeitsanzeige | offen |
| Touren | Lesen, Filtern, Sortieren, Anlegen, Bearbeiten, Kopieren, Löschen, Mehrfachbearbeitung | offen |
| Zuordnungen | Kategorien, Equipment, Aktivitäten, Trainingsbereiche, Zusammenführen | offen |
| Aktivitätsstatistik | freie Datumsgrenzen, Kalender-/Kennzahlauswahl, Saisonziele, Bestleistungen | offen |
| Pulsauswertung | FIT-Messzeiten, aktuelle Garmin-Referenz mit Abrufdatum, Lückenkennzeichnung | offen |
| Tourdetail | Höhenprofil, Messreihen, Karte, Mehrtagestouren, Trackbearbeitung | offen |
| Garmin Connect | Key-Verwaltung ohne Rückgabe des Werts, 20er-Pakete, Hintergrundauftrag, Duplikatschutz | offen |
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

## Erkannte direkte Abhängigkeiten im Ausgangsfrontend

Inventar beim Branchstart (ohne bin/obj):
- 11 Dateien rufen OeffneDatenbank.Ausfuehren auf.
- 12 Dateien verwenden SqliteConnection.
- 4 Dateien verwenden FIT-Lesefunktionen.
- 2 Dateien verwenden GarminPersonalClient.
- Je 6 Dateien verwenden direkte File-/Directory-Zugriffe; Geräte- und bewusst
  lokale Export-/Downloadoperationen sind getrennt von Backendzugriffen zu bewerten.

Abschlussprüfung: produktives Frontend darf weder AndyTours.Daten noch
AndyTours.Import bzw. Microsoft.Data.Sqlite referenzieren oder beliebige
Server-Dateipfade/SQL-Aufträge über HTTP übertragen. Die Geräteadapter-Ausnahme
gilt ausschließlich für USB/MTP und lokale Benutzerauswahl, nicht für Fachlogik.

## Geplante Arbeitsblöcke

1. Betriebs-/Frontendentscheidung und Abhängigkeitsinventar abschließen.
2. Contracts, Application und Backend-Host mit lokal/zentraler Konfiguration.
3. Ressourcen, Validierung, Dateiablage und Hintergrundaufträge migrieren.
4. Sämtliche bisherigen Clientabläufe umstellen, Geräteadapter abgrenzen.
5. Garmin-Connect-orientierte Darstellung: klare Navigation, KPI-Karten,
   sichtbare Zeitraum-/Raster-/Kennzahlfilter, fokussierte Diagramme, konsistente
   Leer-, Lade- und Fehlerzustände, Tastaturbedienung.
6. Funktionsparität und Architekturgrenzen statisch prüfen, Builds auf Testserver.
7. Tests bereitstellen (Ausführung durch Anwender), Windows-/Browser-Abnahme offen kennzeichnen.

Nach jedem wesentlichen Block committen und diesen Status anhand der tatsächlich
umgesetzten Abläufe aktualisieren. Teilstände nicht als vollständige Umstellung ausgeben.

## Umgesetzter Zwischenstand (API_FIRST)

- ✅ Grundlage: getrennte Contracts-, Application-, API- und Client-Projekte; API-Statusressource `/api/v1/health`; typisierter HTTP-Verbindungsclient.
- ⏳ Nicht migriert: sämtliche fachlichen Ressourcen und Anwendungsfälle, Authentifizierung/Zugriffsschutz, Datenbank- und Dateiinfrastruktur, Importaufträge, lokale/zentral betriebene Konfiguration, WPF-Start/Ansichten sowie Geräteadapter.
- ⏳ Alle Abläufe der obigen Paritätstabelle bleiben offen. Ein Statusendpunkt ist kein Nachweis fachlicher Parität.
- Buildprüfung auf dem Testserver wurde versucht, scheiterte vor dem Build: `global.json` fordert .NET SDK 10.0.402, installiert ist 10.0.401. Tests wurden entsprechend Vorgabe nicht ausgeführt.
