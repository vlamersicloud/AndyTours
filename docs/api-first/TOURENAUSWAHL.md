# Manuelle und seitenübergreifende Tourenauswahl

Die Tourenübersicht hält manuell ausgewählte Touren über Seitenwechsel und reine Sortier-/Listenaktualisierungen hinweg. Die Auswahl speichert einen unveränderlichen Snapshot aus Tour-ID, beim Auswählen gelesener Version und kopierten Tourdaten; spätere Listenantworten ersetzen eine eingefrorene Version nicht. „Auswahl aufheben“ leert den gesamten Snapshot. Ctrl+A bleibt auf die sichtbare Seite begrenzt.

## Alle Treffer der angewandten Filter

„Alle Filtertreffer auswählen“ ist eine ausdrückliche Benutzeraktion; es gibt keine automatische Massenauswahl und keine Massenschreibaktion. Der Client sendet den zuletzt erfolgreich angewandten Snapshot aus Von/Bis, Aktivitäts-ID, Kategorie-ID und den sieben Tabellenfiltern an `POST /api/v1/touren/auswahl/filtertreffer`. Nicht angewandte Texteingaben werden nicht in diesen Request übernommen. Die Antwort muss denselben Filter-Snapshot zurückgeben und wird nur nach passender Query-Generation, vollständiger Validierung und erfolgreichem Klonen atomar zur Auswahl.

Der Server wertet alle Filter mit denselben Core-Regeln wie die Tourenliste in einer einzelnen schreibgeschützten SQLite-Transaktion aus. IDs, Versionen und Tourdaten stammen aus diesem konsistenten Snapshot. Das Antwort-DTO enthält die vorhandenen Tourantworten, da die bestehende Mehrfachdialog-/CAS-Strecke diese vollständigen, versionierten Auswahl-Snapshots benötigt. Es werden keine Schreiboperationen oder automatischen Mehrfachaktionen ausgelöst.

### Pflichtkonfiguration und Fehlerverhalten

Das Feature ist nur verfügbar, wenn beide positiven, invariant dezimalen Integerwerte konfiguriert und gültig sind:

- `Backend:Tourenauswahl:MaxTouren`
- `Backend:Tourenauswahl:MaxAntwortBytes`

Es gibt keine Standardwerte. Eine fehlende, partielle, nichtpositive, überlaufende oder anderweitig ungültige Konfiguration deaktiviert ausschließlich diesen Endpunkt; er antwortet mit einem festen HTTP 503, während die übrige API gestartet bleibt. Beide Grenzen werden mit positivem, invariantem `Int32`-Parsing ohne arithmetische Addition oder Überschreitung geprüft.

Überschreitet die Trefferzahl `MaxTouren` oder die exakt serialisierte JSON-Antwort `MaxAntwortBytes`, antwortet der Endpunkt mit HTTP 413. Die Abfrage liefert dann keinerlei Auswahl zurück: keine Trunkierung, keine teilweise ersetzte Auswahl und kein Writer-Aufruf. Die Antwortserialisierung wird beim konfigurierten Bytebudget begrenzt. Die aktuelle serverseitige Core-Filterung materialisiert die Tourdaten vor der Treffer-Cap-Prüfung; diese beiden Ergebnisgrenzen begrenzen daher nicht den vollständigen Prozessspeicherbedarf während der Filterauswertung. Es wird keine zusätzliche Scan-Cap angenommen. Das Leeren der Auswahl bleibt jederzeit eine separate explizite Aktion.

Änderungen eines angewandten Datums-, Aktivitäts-, Kategorien- oder Tabellenfilters heben die Auswahl nach erfolgreichem Listencommit auf. Fehlgeschlagene oder abgebrochene Listen- und Auswahlrequests bewahren den vorherigen Auswahl-Snapshot. Sortierung und Seitenwechsel ändern die Filteridentität nicht. Mehrfachaktionen verwenden den eingefrorenen Snapshot und behalten ihre Bestätigung, CAS- und Idempotenzregeln; nur ausdrücklich bestätigte Outcomes entfernen ausgewählte IDs.

Quellfixtures decken SQL-Filtertreffer, Cap-Ablehnung ohne Teilresultat, Snapshotversionen, Auswahlimmutability sowie wesentliche HTTP-/VM-/Markup-Verträge ab. Die Tests wurden nicht ausgeführt. Das Paket wurde gemeinsam quellengeprüft und auf 192.168.178.32 mit SDK 10.0.402 vollständig gebaut: API, Windows-Ziel und Testprojekt jeweils 0 Warnungen / 0 Fehler. Logs: `/tmp/andytours-allfilter-selection-{api,app,tests}-final-20261118.log`. UI-/Integrationslaufzeit und Benutzer-Testlauf bleiben offen; ein Build ersetzt diese Abnahme nicht.
