# Explizite SQLite-Basisprüfung

## Umfang und Grenzen

Das Quellpaket verbindet den isolierten Prüfer mit einem expliziten authentifizierten POST auf `/api/v1/sicherungen/sqlite-basispruefung`, typisiertem Client und einer eigenen Aktion in der Windows-Metadatenvorschau. Die Route trägt die exklusive Datenbestandskoordination. Anfrage: nur `SicherungsId` und `ErwarteterInhaltHash`, keine Dateipfade oder Clientbudgets. Die vorhandene Bereitstellung akzeptiert ausschließlich den gelisteten Vollarchivstand mit passendem vollständigen Hash.

Der Prüfer untersucht ausschließlich Read-only-Zugriff, Schema-Version 1–5, acht Kerntabellen und `quick_check`. Das ist kein vollständiger Schema-/Foreign-Key-/Anhangsreferenz-, Restore- oder Freigabenachweis. Das Ergebnis wird separat angezeigt; die vorhandenen Metadatenflags und Restore-/Apply-Sperren bleiben unverändert. Keine automatische Prüfung beim Start oder Listing. Kein Restore-Endpunkt und keine Retention-/Recovery-Löschung werden eingeführt.

## Explizite Serverkonfiguration

Ohne vollständige gültige Konfiguration bleibt nur die Prüffunktion nicht verfügbar; übrige API-Funktionen bleiben nutzbar. Keine Produktivlimits, Dateipfade oder Aufbewahrungsregeln werden vorbelegt.

Unter `Backend:Sicherung:SqlitePruefung:`:

- `CheckerExecutable`: absoluter vertrauenswürdiger Executable-Pfad.
- `Pruefordner`: vorhandener privater Prüfroot, ohne Überlappung mit dem aktiven Datenordner.
- `LaufzeitMillisekunden`: explizit positiv, im implementierten Integerbereich.
- `MaxOutputBytes`: explizit positives Byte-Limit je Prozesspipe.
- `MaxDatenbankBytes`: explizit positives Größenlimit der bereitgestellten Datenbank.

Zusätzlich gelten die vorhandenen Archiv-/Manifestgrenzen unter `Backend:Sicherung:`: `MaxEintraege`, `MaxDateiBytes`, `MaxGesamtBytes`, `MaxPfadzeichen`, `MaxArchivBytes`, `MaxManifestBytes`, `MaxZentralverzeichnisBytes`. Der Vollarchiv-Katalog benötigt außerdem `MaxKatalogVollarchive` und `MaxKatalogNutzdatenBytes`. Alle Werte müssen die vorhandenen Konsistenzregeln erfüllen. Aktiver Datenroot: vorhandenes `Backend:Datenordner`.

Verzeichnisse und Executable müssen vertrauenswürdig bereitgestellt sein; Reparse-Prüfung ersetzt keine OS-Sandbox oder Absicherung gegen unabhängige Writer. Fehlende/ungültige Ressourcen und technische Runnerfehler liefern sichere Nichtverfügbarkeit. Fachliche Anfrage-, Fund- und Standkonflikte werden als 400/404/409 behandelt; erfolgreiche HTTP-Verarbeitung bedeutet nicht zwangsläufig eine bestandene SQLite-Prüfung. Fachstatus und Prüfumfang müssen berücksichtigt werden.

## Paketierung

Die Builddefinition `tools/AndyTours.Installation/Erstelle-Installation.ps1` veröffentlicht das vorhandene Projekt `tools/AndyTours.SqlitePruefer/AndyTours.SqlitePruefer.csproj` für win-x64 self-contained nach `backend/sqlite-checker`. Die bestehende rekursive Dateigruppe in `AndyTours.iss` übernimmt es; ein zusätzlicher Prüferstart wird nicht definiert. Der konfigurierte Executable-Pfad muss anschließend zur tatsächlichen Installation passen. Dieses Paket setzt keine Betriebskonfiguration und startet oder installiert nichts.

## Verifikation und offene Abnahme

Gemeinsames Quellreview und Korrekturdurchgang abgeschlossen. Remote auf 192.168.178.32 mit SDK 10.0.402 wurden Checker, API, Windows-Ziel und Testprojekt vollständig gebaut (`-c Release --no-restore --no-incremental -p:EnableWindowsTargeting=true`), jeweils 0 Warnungen / 0 Fehler. Logs: `/tmp/andytours-rest-integration-{checker,api,app,tests}-final-20261118.log`. Vorbereitete Fixtures sind keine ausgeführten Tests und keine vollständige Race-/Fehlerpfadabdeckung. Weder API, Windows-App, Installer, Prüfer noch Restore wurden ausgeführt. Offen: freigegebene Betriebsbudgets/Deploymentprüfung, Benutzertestlauf, Windows-/Skalierungs-/Providerabnahme, echte Read-only-Prüferläufe und Betriebs-/Recoverykonzept. Gesamtmigration und tatsächliches Anwenden bleiben offen.
