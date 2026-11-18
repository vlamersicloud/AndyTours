# Textlisten-Nutzungsvorschau – Quellpaket

## Umfang
Trainingsbereiche und Tätigkeiten erhalten eine read-only Nutzungsabfrage. Die Antwort enthält Textlistenart, Textlisten-Version, kanonischen Namen, Touren/Wartungen/Alarme sowie einen opaken Nutzungsstempel. Für Tätigkeiten stammen Wartung und Alarm aus derselben Tabelle und werden anhand des gespeicherten Typs getrennt gezählt.

Die SQLite-Vorschau öffnet die vorhandene Datenbank direkt mit ReadOnly und Pooling=false. Sie ruft nicht `OeffneDatenbank` auf und führt keine Migration, Schemaanlage oder Schreiboperation aus. Eine fehlende Datenbank wird nicht angelegt. Nutzungsstempel binden die IDs und ursprünglichen Nutzungswerte der betroffenen Zeilen. Vor Umbenennung werden Textlisten-CAS und Stempel innerhalb derselben schreibenden Transaktion geprüft; geänderte Nutzung endet als Konflikt. Es gibt keinen automatischen Retry.

Die WPF-VM fragt vor jedem Rename die Verwendung ab. Bei Verwendung nutzt sie den vorhandenen Bestätigungsdialog mit Touren- bzw. getrennten Wartungs-/Alarmzahlen. Ablehnung oder Schließen führt zu keinem Schreibaufruf. Delete zeigt vorab konkrete Verwendungsgründe; das Backend prüft weiterhin aktuell innerhalb der Mutationstransaktion. Jede Umbenennung muss den opaken `VerwendungsVersion`-Stempel aus genau dieser Vorschau mitsenden; fehlender oder veralteter Stempel wird serverseitig fail-closed abgelehnt. Ältere Clients, die den Stempel nicht mitsenden oder die erforderlichen Zählerfelder nicht strikt lesen, können den Rename-Flow nicht sicher ausführen und müssen aktualisiert werden.

## Konfiguration und Betrieb
Keine neue Produktionskonfiguration. Die Abfrage ist read-only, erzeugt keine Datenbank und benötigt die bestehende Datenbankdatei. Backend-Zugriffsschutz wird nicht geändert.

## Verifikation
Quelltests für ReadOnly/fehlende Datenbank, getrennte Zählungen, veränderte betroffene Zeilen, HTTP-Echo/Pre-Cancel sowie VM-Bestätigung/Close sind vorbereitet. API, Windows-App und Testprojekt wurden auf 192.168.178.32 mit SDK 10.0.402 in Release vollständig (--no-restore --no-incremental, EnableWindowsTargeting=true) kompiliert: jeweils 0 Warnungen und 0 Fehler. Tests und Laufzeitaktionen wurden nicht ausgeführt. API-Routen-/Markup-Struktur und Windows-Laufzeitprüfung bleiben vor einer Abnahme offen.

Buildlogs: `/tmp/andytours-textlist-usage-api-final-20261118.log`, `/tmp/andytours-textlist-usage-app-final-20261118.log`, `/tmp/andytours-textlist-usage-tests-final-20261118.log`. Kompilierung ist kein Test- oder Windows-Abnahmenachweis.
