# Garmin Connect – Live-Import

## Projektanalyse und Umfang

AndyTours ist eine Einzelbenutzer-Windows-Anwendung mit WPF/MVVM, .NET 10 und SQLite.
Der bestehende USB-/Dateiimport verwendet bereits Garmin.FIT.Sdk (21.218.0), liest
Touren und Sensordaten und bietet eine Equipment-Auswahl. Diese Abläufe bleiben erhalten.
Die neue Anbindung ergänzt einen direkten, manuell ausgelösten Online-Abruf von
Radaktivitäten. „Live“ bedeutet hier Abruf beim Menüaufruf, nicht Push oder Hintergrunddienst.
Schlaf, HRV, Stress und Body Battery sind nicht Teil dieses Tourenimports.

## Einrichtung auf dem Windows-Rechner mit AndyTours

1. In Garmin Connect einen eigenen Personal API-Key erstellen:
   https://connect.garmin.com/app/settings/personalApiToken
2. In AndyTours **Extras → Einstellungen → Persönlich → Garmin Connect** öffnen.
3. Den API-Key im maskierten Feld eingeben, ohne Anführungszeichen oder Präfix
   `ApiKey`, und **Key speichern** wählen. Das Schließen des Fensters allein
   speichert einen Entwurf nicht.
4. Unter „Extras → Einstellungen“ muss wie bisher eine Hauptaktivität vorhanden sein.
5. „Datei → Von Garmin Connect importieren…“ aufrufen und die gewünschten Touren
   in der vorhandenen Importauswahl bestätigen. Equipment kann dort zugeordnet werden.

Der Key gilt sofort beim nächsten Import; kein Neustart und keine Umgebungsvariable
sind erforderlich. **Key entfernen** löscht den gespeicherten Wert. Auch das Speichern
einer leeren Eingabe entfernt ihn. Es gibt bewusst keinen Fallback auf eine
Umgebungsvariable, damit ein entfernter Key nicht unbemerkt weiterverwendet wird.

Wie der vorhandene Routing-Key liegt der Garmin-Key **unverschlüsselt in SQLite**
im aktuellen Datenordner (`Persoenlich.Garmin.PersonalApiKey`) und damit auch in
Datensicherungen. Ein Ordnerwechsel lädt den dort gespeicherten Key; ungespeicherte
Entwürfe werden verworfen. Alte Sicherungen behalten einen früheren Key auch nach
dem Entfernen; widerrufen wird er nur bei Garmin. Datenordner und Sicherungen
entsprechend schützen. Ein Key-Wechsel innerhalb desselben Datenordners soll nur
für dasselbe Garmin-Konto erfolgen (siehe Einzelkonto-Regel unten).

Der Bearer-Token lebt nur während des Importlaufs im Speicher. Es werden weder
Garmin-Passwörter noch SSO-Bibliotheken oder inoffizielle Login-Endpunkte verwendet.
Die Anwendung zeigt und protokolliert den Schlüssel nicht als Klartext.
Im Quellcode, in Downloads und im Windows-Paket ist kein echter Key hinterlegt;
den Safety-Store-Key übernimmt die Anwendung nicht automatisch.

## Verhalten

- Offizielle globale API: `https://personal.connectapi.garmin.com`.
- POST `/personalapitoken-service/personal-api/personal-api-key/token`
  mit `Authorization: ApiKey …`; Antwort als Texttoken.
- GET `/activitylist-service/activities/search/activities` mit Bearer-Token,
  `activityType=cycling`, `start` und `limit=100`. Alle Seiten werden gelesen;
  ältere und nachträglich hochgeladene Aktivitäten werden nicht durch einen
  Datumshochwasserstand ausgeschlossen.
- GET `/download-service/files/activity/{id}` liefert das Original-ZIP.
- Bereits erfolgreich importierte Garmin-IDs werden vor dem Download übersprungen.
  Die Zuordnung `GarminConnect.Aktivitaet.<ID>` → lokale Trainingseinheit liegt
  in der vorhandenen Einstellungstabelle. Einheit und ID werden atomar gespeichert.
- Abgewählte Touren gelten nicht als importiert und werden beim nächsten Aufruf
  erneut angeboten. Bei bereits per USB importierten Touren greift zusätzlich
  die bisherige Duplikatprüfung anhand Datum/Startzeit; keine unbewiesene ID-Zuordnung.
- Original-ZIPs bleiben unter `<Datenordner>/GarminConnect/activity-<ID>.zip`.
  Fertige Downloads werden nach Abbruch wiederverwendet. FIT-Dateien werden zuerst
  temporär gelesen und bei bestätigtem Import wie bisher im Jahresordner abgelegt.
- Archive mit keiner oder mehreren FIT-Dateien werden mit Hinweis übersprungen,
  nicht als importiert markiert und aus dem Downloadcache entfernt. ZIP-Pfade werden
  nicht übernommen. Grenze: 64 MiB Download, 128 MiB entpackte FIT-Datei.
- Bei 401 wird genau einmal neu authentifiziert; 403 und 429 brechen mit Hinweis ab.
  Es gibt keine automatische Retry-Schleife bei Ratenlimits. Teilweise geladene
  Dateien werden nicht als fertige Archive übernommen.
- HTTP-Redirects sind deaktiviert. Fehlerantworttexte werden nicht ausgegeben,
  damit Garmin nicht versehentlich Schlüssel oder Token in Meldungen spiegeln kann.
- Netzwerkfehler vor der Importauswahl ändern keine Trainingseinheiten.
  Bereits gespeicherte ZIPs bleiben lokal. Ein Importlauf hat ein Zeitlimit von 30 Minuten.
- Nach Löschen einer lokalen Tour ist deren ID wieder importierbar, das Archiv
  bleibt erhalten. Je Datenordner genau ein Garmin-Konto verwenden: API-IDs und
  Archivcache sind nicht als Mehrkontenverwaltung ausgelegt.
- Garmin liefert persönliche Daten. Beim Weitergeben eines Datenordners oder
  Erstellen einer Sicherung den Archivordner entsprechend berücksichtigen.

## Prüfung / Abnahme

Offline-Tests für den ursprünglichen Connect-Import wurden ausgeführt; die zusätzlichen Tests der persönlichen Einstellungen wurden nur kompiliert (Abnahme durch den Anwender):
`tests/AndyTours.Tests/Garmin/GarminPersonalClientTests.cs` und
`GarminImportZuordnungTests.cs`. Zusätzlich: `GarminEinstellungTests.cs`
für fehlenden Key, Ersetzen, Entfernen, getrennte Datenbestände und unveränderte
fremde Einstellungen.

Sie decken Authentifizierungsheader und Host, mehr als 100 Aktivitäten, Download,
begrenzte Token-Erneuerung, Ratenlimit ohne Geheimnisse in Fehlern, wiederholte
Seiten, Archivcache, Pfadtraversierung, mehrdeutige Archive sowie Commit/Rollback
der ID-Zuordnung ab. Sie benötigen keine echten Zugangsdaten.

Testserver-Befehl (mit dem in global.json geforderten SDK):
```sh
dotnet test tests/AndyTours.Tests/AndyTours.Tests.csproj --filter "FullyQualifiedName~Garmin"
```
Buildnachweis auf dem Linux-Testserver: Windows-Cross-Build mit
`dotnet build AndyTours.slnx -p:EnableWindowsTargeting=true`. Installiert war
SDK 10.0.401; ausschließlich für diese Buildprüfung wurde die SDK-Auswahl
vorübergehend darauf gesetzt und anschließend auf 10.0.402 zurückgesetzt.
Die versionierte global.json bleibt unverändert.
WPF-Start und interaktive Abnahme sind nur unter Windows möglich.
Empfohlene manuelle Fälle:
- Unter Persönlich: Key speichern, Fenster neu öffnen, maskierte Anzeige prüfen;
  nach Datenordnerwechsel gilt der Key des neuen Ordners.
- Entwurf ohne Speichern schließen: bisheriger gespeicherter Wert bleibt.
- Key entfernen: Import zeigt Einrichtungshinweis, auch bei gesetzter alter
  Umgebungsvariable. Falscher/abgelaufener Key: verständlicher Fehler.
- Echtes Konto: Radaktivitäten erscheinen, Equipment wählen, Import bestätigen.
- Erneuter Abruf: keine doppelten Touren, importierte IDs nicht erneut herunterladen.
- Auswahl abbrechen/teilweise bestätigen und erneut abrufen.
- Dieselbe Tour erst über USB/FIT, danach Connect: kein doppelter Eintrag.
- Netzwerk trennen und später erneut versuchen; fertige Archive bleiben nutzbar.

Kein echter Kontoabruf wurde vorgenommen: dafür ist ein eigener gültiger Personal
API-Key erforderlich. Offline-Vertragstests ersetzen diese Live-Abnahme nicht.

## Offizielle Referenzen

- https://developer.garmin.com/gc-developer-program/personal/personal-api/
- https://developer.garmin.com/gc-developer-program/personal/swagger-docs/
- https://github.com/garmin/connect-personal-api-python
- https://github.com/garmin/connect-personal-api-python/blob/main/src/garmin_personal_api/auth.py

Authentifizierung und Aktivitäts-/Download-Endpunkte wurden mit dem offiziellen
Referenzclient abgeglichen. Es wurde kein Python-Code oder zusätzlicher SDK-Wrapper übernommen.
