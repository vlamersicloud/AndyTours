# Vollarchiv-Aufbewahrung – Quellpaket

## Umfang und Semantik
Der Planner unter `src/AndyTours.Application/Sicherung/VollarchivAufbewahrungsPlaner.cs` erzeugt aus einem Katalogsnapshot, einer konfigurierten Anzahl (1–99) und dem gerade erstellten Vollarchiv einen deterministischen, unveränderlichen Vorschlag. Er verwendet `WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig`; Legacy- und ungültige Metadaten werden niemals Kandidaten. Das frische Vollarchiv muss im Snapshot identisch enthalten sein und bleibt auch dann erhalten, wenn sein Zeitstempel außerhalb der Retention-Reihenfolge läge. Gleichstände werden nach opaker ID deterministisch aufgelöst.

**Der Plan ist keine Löschberechtigung.** Es wurde kein Executor und keine automatische Retention geliefert. Insbesondere darf Legacy-SQLite nie automatisch gelöscht werden; unbekannte, ungültige und temporäre Dateien bleiben unangetastet.

## Sicherheitsgrenze für automatische Ausführung
Der vorhandene Katalog-/Prüfquellpfad hält read-only Stream-Leases für Lesen und Validieren bereit. Aus den geprüften Verträgen ist keine identitätsgebundene, atomare Löschprimitive belegt. Ein nachträgliches `Delete(path)` löst TOCTOU nicht: zwischen Katalogprüfung und Löschen kann die Datei ausgetauscht werden. Opaque ID, Hash oder ein offener Lesestream allein beweisen nicht, dass ein separat gelöschter Pfad weiterhin dasselbe Objekt bezeichnet.

Daher ist automatische Löschung absichtlich blockiert. Für einen Executor wäre eine serverlokale, unter derselben exklusiven Datenbestandskoordination ausgeführte Storage-Primitive nötig, die vertrauenswürdige Root- und Reparse-Prüfung, Katalogidentität (ID/Hash/Größe), Objektidentität und unlink/delete als eine abgesicherte Operation verbindet. Externe Writer müssen ausgeschlossen oder als verbleibende TOCTOU-Grenze ausdrücklich akzeptiert werden. Der Executor dürfte erst nach erfolgreichem vollständigem Archiv-Commit anlaufen; ein späterer Cleanupfehler darf den bestätigten Sicherungserfolg nicht als fehlgeschlagene/zu wiederholende Erstellung darstellen.

## Verifikation und Grenzen
Pure Fixtures sind vorbereitet und kompiliert, wurden jedoch nicht ausgeführt. Am 18.11.2026 wurden API, Windows-Ziel und Testprojekt auf dem Testserver mit SDK 10.0.402 jeweils vollständig (`--no-incremental`) mit 0 Warnungen / 0 Fehlern gebaut. Kein API-/Windows-Lauf und kein tatsächlicher Sicherungs- oder Löschvorgang. Restore-Apply bleibt gesperrt, bis ein separater sicherer Betriebs-, Staging-, Recovery- und Bestätigungsentwurf vorliegt. Die Aufbewahrungskonfiguration verwendet die bestehende Sicherungsrichtlinie; es werden keine Produktionswerte erfunden.
