# Isolierter SQLite-Prüfer

## Umfang

Der interne Prüfdienst kombiniert die vorhandene Vollarchiv-Prüfbereitstellung mit einem separaten .NET-10-Consoleprüfer. Er prüft ausschließlich SQLite `user_version` (1–5), die acht belegten Kerntabellen und `PRAGMA quick_check`. Ein Erfolg ist kein vollständiger Schema-, Foreign-Key-, Anhangsreferenz-, Restore- oder Freigabenachweis. Der Prüfer öffnet ReadOnly mit Pooling=false, setzt `query_only` und liest dessen Wert zurück. Er startet keine Migration und keinen Restore.

Der Server muss einen vertrauenswürdigen absoluten Executable-Pfad sowie positive Timeout-, Pipe-Ausgabe- und Datenbankgrößenlimits explizit bereitstellen. Keine Produktivdefaults, Konfigurations-/DI-Bindung oder Deploymentintegration wurden ergänzt. Insbesondere wird der Prüfer nicht automatisch durch das Hinzufügen des Projekts ausgeführt. Argument ist ausschließlich der interne Pfad der eigenen versiegelten Staging-Datei; es gibt keine HTTP-Pfadübergabe. Bestehende öffentliche Vorschau-Flags und Apply-Sperren bleiben unverändert.

## Prozess- und Ressourcenbesitz

Der Parent liest beide Pipes gleichzeitig und begrenzt ihre Bytezahl. Er überwacht Child-Exit, Readerfehler, Timeout und Caller-Abbruch. Normaler Exit allein beendet das Lesen nicht: Beide Reader müssen vollständig abgeschlossen sein. Fehler und Abbruch führen zum Killversuch ausschließlich am eigenen Child-Prozessbaum.

Sind Exit oder Readerabschluss nicht bestätigt, hält eine interne, prozesslokale Quarantäneregistry Child, Reader-Tasks, Reader-Cancellation-Source und Stage stark fest. Der Prüfdienst löscht diese Stage nicht. Eine interne Bereinigung ist erst nach bestätigtem Exit und abgeschlossenen Readern möglich. Diese Registry ist kein fertiger betrieblicher Recovery-/Administrationsablauf und überlebt keinen Serverneustart. Es gibt keine automatische Löschung alter Quarantänedateien und keine daraus abgeleitete Retention-Regel.

SHA-256 vor und nach der Prüfung erkennt beobachtbare Dateiänderungen. Pfadprüfungen verweigern Reparse-Points und deren Vorfahren. Das ersetzt keine OS-Sandbox und keinen Schutz gegen einen unabhängig schreibenden Angreifer oder nicht beendbare Kernel-I/O; vertrauenswürdige private Verzeichnisse und Ausschluss externer Writer bleiben Voraussetzung. Nach dem Child-Start gibt es keine unbeschränkte Wartepflicht auf nicht kooperierende Reader: Solche Ressourcen bleiben quarantiniert.

Protokollversion 1 akzeptiert genau fünf benannte Felder, feste Statuswerte und begrenzte Prüfmetadaten. Doppelte, unbekannte oder fehlende Felder, falsche Typen/Versionen und widersprüchliche Erfolgsdaten werden verworfen. Erfolg setzt Exitcode 0 voraus; unterstützte fachliche Fehler benötigen Exitcode 1 oder 2. Pfade, SQL, Zeilenwerte und Rohfehler werden nicht als Ergebnis ausgegeben.

## Verifikation und offene Abnahme

Vorbereitet sind eine reine Protokollfixture und sieben Fake-Runnerfälle: Erfolg, widersprüchlicher Exitcode, Output-Überlauf, verzögerte Ausgabe nach Exit, Caller-Abbruch, Readerfehler und Killfehler mit Stage-Aufbewahrung. Die Fixture erzeugt eigene temporäre Archiv-/Stage-Testdaten; die Datenbankbytes sind keine gültige SQLite-Datei und belegen keine echte SQLite-Prüfung. Es werden keine echten Child-Prozesse in diesen Fakes verwendet. Dies ist keine vollständige Fehlerpfad- oder Race-Abdeckung.

Finaler gemeinsamer Remote-Fullbuild auf 192.168.178.32 mit SDK 10.0.402: Checker, API, Windows-Ziel und Testprojekt jeweils 0 Warnungen / 0 Fehler (`-c Release --no-restore --no-incremental -p:EnableWindowsTargeting=true`). Neue Checker-Projektassets wurden zuvor gezielt wiederhergestellt. Logs: `/tmp/andytours-sqlite-checker-{checker,api,app,tests}-final-20261117.log`. Tests, Checker, Anwendungen und SQLite-Prüfungen wurden nicht ausgeführt; die Builds kompilieren die vorbereiteten Prüffälle, führen sie aber nicht aus.

Offen bleiben Benutzer-Testlauf, tatsächliche Checker-/Publish-/Deploymentprüfung, Windows-Abnahme, Betriebs-/Recoverykonzept und jede Restore-/Apply-Freigabe.
