# Testportabilität

Die Logs von Commit 1ceeb49 (1051 bestanden, 10 fehlgeschlagen) und
Baseline ec3376c (1044 bestanden, 10 fehlgeschlagen) enthalten dieselben
zehn fehlgeschlagenen Fälle. Diese Änderung passt ausschließlich Tests an.

## Einordnung der ursprünglichen zehn Fehler

| Test | Fälle | Behandlung |
|---|---:|---|
| TauscheZielordnerAus: geöffnete Datei | 1 | Windows-only: fehlendes FileShare.Delete verhindert dort den Ordneraustausch. |
| PruefeDatenordner: fehlendes Laufwerk | 1 | Windows-only: Laufwerksbuchstaben sind unter Unix keine Laufwerke. |
| IstNetzwerkpfad: UNC und verbundenes Netzlaufwerk | 3 | Windows-only: native Pfadauflösung und Windows-Laufwerkssemantik. |
| LeseAufrufparameter: vollständige Pfade und Standardziel | 2 | Portable native absolute Pfade; ursprüngliche Windows-Pfade zusätzlich separat erhalten. |
| BildeCoursename: gespeicherte Planung | 1 | Nativer Dateipfad; ursprünglicher Windows-Pfad zusätzlich separat erhalten. |
| FuehreStartsicherungDurch: unerreichbarer Sicherungsordner | 1 | Windows-only: Fehlersimulation über nicht vorhandenes Laufwerk bleibt erhalten. |
| SpieleSicherungZurueck: Austausch scheitert | 1 | Windows-only: FileShare.ReadWrite ohne Delete sperrt unter Windows das Ersetzen. |

WindowsFactAttribute und WindowsTheoryAttribute melden außerhalb von
Windows einen ausdrücklichen xUnit-Skip statt eines Fehlschlags. In den
betroffenen Dateien verwenden auch die vorhandenen Windows-ACL-Tests diese
Attribute statt eines stillen Returns. Fehlende freie Laufwerksbuchstaben
oder ein nicht verbundenes Netzlaufwerk führen auf Windows weiterhin zum
bisherigen frühen Return; diese Tests benötigen entsprechende Umgebung.

Die drei portablen Pfadprüfungen bleiben auf allen Plattformen aktiv. Ihre
zusätzlichen Windows-Varianten bewahren die ursprünglichen Regressionen,
insbesondere die UNC-Zielangabe. Produktionscode und Windows-Fachverhalten
bleiben unverändert.

## Validierung

Builds und Tests dürfen ausschließlich auf dem Testserver und nach
gesonderter Freigabe ausgeführt werden. global.json verlangt SDK 10.0.402
und wird für die Tests nicht verändert.

Nach gesonderter Freigabe am 15.11.2026 auf root@192.168.178.32 geprüft:

- SDK 10.0.402 separat unter /opt/dotnet-sdk-10.0.402 installiert; bestehendes
  System-SDK 10.0.401 und global.json unverändert.
- Gezielter Lauf der sieben Testklassen: 34 bestanden, 0 Fehler, 13 Skips.
- Vollsuite des Testprojekts: 1050 bestanden, 0 Fehler, 13 Skips (1063 gesamt).
- Drei zuvor still als bestanden gezählte ACL-Tests und ein Test eines fehlenden
  Laufwerks werden jetzt ehrlich als Windows-Skips ausgewiesen. Drei portable
  Pfadfälle kommen zur aktiven Linux-Abdeckung hinzu; die ursprünglichen
  Windows-Varianten bleiben zusätzlich bestehen.
- xUnit zählt eine vollständig übersprungene UNC-Theorie als einen Skip statt
  als zwei Datenzeilen; daher 1063 statt rechnerisch 1064 Testfälle.
- Logs: /tmp/andytours-portability-targeted.log und
  /tmp/andytours-portability-full.log. TRX-Dateien liegen unter
  /tmp/andytours-portability-results/.
- Die Windows-spezifischen Prüfungen wurden auf Linux nicht ausgeführt; ihre
  tatsächliche Ausführung erfordert weiterhin eine geeignete Windows-Umgebung.
