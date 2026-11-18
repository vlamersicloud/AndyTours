# Wiederherstellungsvorschau: Implementierungsstand

Die API bietet `POST /api/v1/sicherungen/wiederherstellungsvorschau/` und der API-Modus eine Vorschau im Extras-Menü. Die Anfrage enthält ausschließlich opake Sicherungs-ID und erwarteten SHA-256 der vollständigen Datei. Der Dienst löst beides gegen den Katalog auf; fehlende IDs ergeben 404, doppelte oder veraltete Inhalte 409, Katalogausfall 503. Die Route nutzt die normale In-Process-Datasetkoordination, aber keinen exklusiven Lease.

Vollarchive liefern derzeit unbekannte Manifestversion, Dateianzahl und Nutzdatenbytes als `null`; Legacy-Einträge zeigen bekannte Dateigröße/Anzahl 1 und keinen erfundenen Zeitzonenzeitpunkt. `DatenbankGeprueft` und `AnwendungFreigegeben` bleiben immer `false`. UI lädt beim Öffnen nur den Katalog, fordert Vorschau erst nach expliziter Betätigung der Vorschau-Schaltfläche an, ignoriert verspätete Ergebnisse nach Auswahlwechsel und verlangt nach 404/409 erneutes Laden. Schließen bricht laufende Leseanforderungen ab.

Ein separater, ausdrücklich gestarteter Sicherungsdownload ist als Quellpaket vorbereitet: Er schreibt eine kataloggelistete Legacy- oder Vollarchivdatei lokal in eine neue temporäre Datei und prüft Hash/Länge, gewährt aber keine Prüf- oder Restorefreigabe. Details und offene Prüfungen: [Sicherungsdownload-Manifest](SICHERUNGSDOWNLOAD-MANIFEST.md).

Das ist keine SQLite-Prüfung, Extraktion, aktive Datenbankänderung, Vorab-Sicherung oder Restore-Freigabe. Für tatsächliche Wiederherstellung braucht es zunächst isoliertes Staging samt ZIP-/Manifest-/Hash- und SQLite-Prüfung sowie einen nachweisbaren Offline-/Prozessguard oder ein getestetes Generation-/Reconnect-Konzept für Handles, Worker und Clientcaches. Danach erst: Vorab-Sicherung, ausdrückliche Bestätigung, atomarer Austausch mit Rollback/Crash-Recovery. Keine Produktionslimits wurden gesetzt; neun serverseitige Archiv-/Kataloggrenzen bleiben deploymentseitig zu konfigurieren. Retention/Löschung ist ungeklärt und bleibt getrennt.

## Interne Bereitstellung
Die interne, serverseitige Staging-Grundlage für gelistete Vollarchive ist dokumentiert: [Isolierte Vollarchiv-Prüfdatenbank](WIEDERHERSTELLUNGS-PRUEFBEREITSTELLUNG.md). Sie verändert weder Metadatenendpoint noch Vorschau-Flags und führt keine SQLite-Prüfung aus.

API-, App- und Testprojekte wurden für den vorherigen Vorschau-/Katalogstand vollständig und ohne inkrementelle Schritte kompiliert (Remote-SDK 10.0.402, 0 Warnungen/Fehler); auch das Downloadpaket ist mit denselben vollständigen Builds jeweils ohne Warnungen oder Fehler kompiliert. Die vorbereiteten Fixtures wurden nicht ausgeführt. Kein HTTP-, Laufzeit-, Backup- oder Restore-Nachweis; Windows-Darstellung ungeprüft.
