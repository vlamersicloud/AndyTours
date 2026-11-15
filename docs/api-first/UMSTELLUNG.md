# API_FIRST – vollständige Umstellung

## Gesicherter Ausgangspunkt

- Branch: API_FIRST, getrennt von main; Änderungen werden ausschließlich hier veröffentlicht.
- Keine kompilierten EXE-Dateien im Repository.
- **Entscheidung: WPF bleibt die Hauptoberfläche und wird zum API-Client umgestellt.**
- Lokales und zentrales Backend sind vorgesehen; derzeit gibt es keine bidirektionale Offline-Synchronisierung.

## Architekturziel

- Contracts: versionierte Requests, Responses und Fehlerverträge, ohne SQLite-/WPF-Typen.
- Application: Anwendungsfälle, Transaktionsgrenzen, Validierung und Import-/Analyseaufträge.
- Core: vorhandene Fachregeln und Berechnungen, unabhängig von HTTP und Oberfläche.
- Infrastructure: SQLite, Dateiablage, FIT, Garmin, Routing und Sicherungen hinter Application-Schnittstellen.
- API: ASP.NET Core unter `/api/v1`, OpenAPI, ProblemDetails und Zugriffssicherung.
- Client: typisierte HTTP-Schnittstelle; kein SQL, keine Backend-Dateipfade oder Garmin-Secrets im Frontend.
- Geräteadapter: ausschließlich lokale USB-/MTP-Kommunikation; Dateien werden über die API übertragen.
- Lokaler Betrieb nutzt denselben Backend-Code mit eigener lokaler Datenablage.
- Zentraler Betrieb nutzt denselben Backend-Code mit authentifizierten Clients und serverseitiger Datenablage.
- Änderungen paralleler Clients dürfen nicht durch unversioniertes Vollbestands-Speichern überschrieben werden.

## Funktionsinventar / Paritätsprüfung

Ein Bereich gilt erst dann als migriert, wenn Vertrag, Anwendungsfall und benutzbarer WPF-Clientablauf vorhanden sind. Ein API-Endpunkt allein bedeutet keine Funktionsparität.

| Bereich | Zu erhaltende Abläufe | Status |
|---|---|---|
| Start / Verbindung | lokal/zentral konfiguriertes Backend, Fehler-/Erreichbarkeitsanzeige | offen |
| Touren | Lesen, Filtern, Sortieren, Anlegen, Bearbeiten, Kopieren, Löschen, Mehrfachbearbeitung | teilweise: Backend-CRUD mit Versionen, WPF-Abläufe noch nicht migriert |
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

## Erkennbarer Ausgangsbestand direkter Frontend-Abhängigkeiten

Inventar beim Branchstart (ohne bin/obj):
- 11 Dateien rufen OeffneDatenbank.Ausfuehren auf.
- 12 Dateien verwenden SqliteConnection.
- 4 Dateien verwenden FIT-Lesefunktionen.
- 2 Dateien verwenden GarminPersonalClient.
- Je 6 Dateien verwenden direkte File-/Directory-Zugriffe; Geräte- und bewusst
  lokale Export-/Downloadoperationen sind getrennt von Backendzugriffen zu bewerten.

Abschlussprüfung: Das produktive WPF-Frontend darf weder AndyTours.Daten noch
AndyTours.Import bzw. Microsoft.Data.Sqlite referenzieren oder beliebige
Server-Dateipfade/SQL-Aufträge über HTTP übertragen. Die Geräteadapter-Ausnahme
gilt ausschließlich für USB/MTP und lokale Benutzerauswahl, nicht für Fachlogik.

## Backend-Konfiguration (Platzhalter)

Das Backend benötigt einen absoluten Datenordner und ein Geheimnis mit mindestens 32 Bytes. Werte sind ausschließlich in der Serverkonfiguration/Umgebung zu setzen, nicht in Repositorydateien:

```text
Backend__Datenordner=<ABSOLUTER_SERVERPFAD>
Backend__Zugriffstoken=<ZUFÄLLIGES_GEHEIMNIS_MIT_MINDESTENS_32_BYTES>
ASPNETCORE_URLS=<OPTIONALE_BINDUNG; Standard: http://localhost:5000>
```

Die OpenAPI-Beschreibung und alle Fachrouten verlangen Bearer-Authentifizierung; Health ist anonym erreichbar. Nicht-loopback Zugriffe sind nur über HTTPS zulässig. Das Zugriffstoken gewährt vollen Zugriff auf den konfigurierten Datenbestand; es gibt derzeit keine Mehrbenutzerverwaltung. Es ist kein Garmin-Schlüssel. Für den Backendbetrieb wird die .NET 10 ASP.NET Core Runtime benötigt; die Desktop Runtime allein reicht nicht.

## Umgesetzter Zwischenstand (API_FIRST)

- ✅ Versionierte Touren-DTOs ohne Core-/SQLite-Typen oder Dateipfade.
- ✅ Application-Port und serverseitige Wertevalidierung.
- ✅ SQLite-Tourenspeicher: Liste/Detail/Anlegen/Ändern/Löschen; Versionsprüfung und Mutation unter SQLite-Schreibtransaktion; Anhänge bleiben bei Änderung erhalten.
- ✅ ASP.NET REST-Routen für Touren, Bearer-Schutz, HTTPS-Anforderung für Remotezugriffe, ProblemDetails und geschützte OpenAPI-Beschreibung.
- ✅ Typisierter asynchroner Touren-HTTP-Client.
- ✅ Sechs Tourenspeicher-Regressionstests wurden hinzugefügt, aber **nicht ausgeführt**.
- ✅ Testserver verwendet .NET SDK 10.0.402 unter `/opt/dotnet-sdk-10.0.402/dotnet`; die bisherigen relevanten Builds waren erfolgreich und warnungsfrei.
- ⏳ Die WPF-App verwendet die neuen API-Abläufe noch nicht; ihre direkten Daten-/Import-Abhängigkeiten und bisherigen Benutzerabläufe sind noch nicht umgestellt.
- ⏳ Alle weiteren Fachmodule und deren Clientabläufe in der Paritätstabelle bleiben offen. Diese Teilabdeckung ist keine vollständige Umstellung.

Keine EXE-Dateien wurden erstellt oder eingecheckt.
