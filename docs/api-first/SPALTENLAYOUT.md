# API-Spaltenlayout der Tourentabelle

## Implementierung

18 stabile fachliche Schlüssel transportieren Reihenfolge, Sichtbarkeit und Breiten der Tourentabelle. Die Breite ist ein WPF-DIP-Wert bei 100 %; `null` bedeutet automatische Breite. Die vorhandene Oberflächenskalierung wird nicht zusätzlich auf gespeicherte Breiten angewendet. Mindestens eine Spalte bleibt sichtbar.

SQLite verwendet die bisherigen Schlüssel `Einheitentabelle.Spalten` und `Einheitentabelle.AusgeblendeteSpalten` sowie deren Textformat. GET öffnet readonly ohne Schreibmigration. Die opaque Version berücksichtigt beide unveränderten Rohwerte einschließlich ihrer Existenz. PUT prüft die Version innerhalb einer Schreibtransaktion und schreibt beide Werte atomar. Der Client greift nicht auf die Datenbank zu.

Die WPF-Komposition lädt das Layout über den typisierten Sitzungsclient. Ein Auswahlmenü steuert Sichtbarkeit; Reihenfolge und manuelle Breiten werden verzögert und serialisiert gespeichert. Nach einem Konflikt oder unklarem Speicherergebnis wird nicht automatisch mit der alten Version weitergeschrieben. „Spaltenlayout neu laden“ verwirft den lokalen Layoutstand zugunsten eines erneuten Serverabrufs.

## Verifikation und offene Abnahme

Quellen und vorbereitete Regeln-, SQLite-, HTTP- und UI-Vertragsfixtures wurden gemeinsam geprüft und korrigiert. API, Windows-App und Testprojekt wurden auf dem autorisierten Testserver mit SDK 10.0.402 vollständig neu gebaut (`--no-incremental --no-restore`): jeweils 0 Warnungen und 0 Fehler. Tests und Anwendungen wurden für die Paketprüfung nicht ausgeführt. Die vorbereiteten Fixtures sind kein vollständiger Beweis aller Cancellation- und UI-Laufzeitfälle.

Windows-Abnahme für Drag-Reorder, Breiten/Auto-Breite, Auswahlmenü, Skalierung, Tastaturbedienung, Lebenszyklus und Mehrclient-Konflikte bleibt offen. Quellassertions und spätere Kompilierung sind kein Laufzeitnachweis. Auswahl über Seiten und Inline-Bearbeitung sind nicht Teil dieses Pakets.
