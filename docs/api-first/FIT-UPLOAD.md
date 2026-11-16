# FIT-Upload – implementierter Stand

- API-Uploads streamen rohe Bytes mit einer Grenze von 128 MiB **pro Datei**. Es gibt keine 20-Dateien- oder 512-MiB-Satzgrenze. Opaque Upload-UUIDs, sichere Namensmetadaten und Hash-/Größenprüfung schützen unveränderliche Originaldateien; gleiche UUID/Inhalt sind wiederholbar, abweichender Inhalt kollidiert.
- Upload-Metadaten-CAS und semantische FIT-/Zeitzonenversion sind getrennt. FIT-Auswertung und Fallback-Zeitzone liegen im API-Importpfad. Ungültige Dateien erscheinen separat; gültige können weiter geprüft werden.
- Die WPF-Prüfung arbeitet in 10.000er-Chunks. Nicht verarbeitete Queue-Einträge bleiben für weitere Prüfung erhalten. Übernahme speichert Touren, Anhänge, Sensoränderungen, Uploadstatus und Receipt atomar. Wiederholungen benötigen eine ausdrückliche manuelle Aktion mit stabiler ID; es gibt kein automatisches Resenden.
- Gespeicherte Uploads lassen sich über paginierte Metadaten wiederherstellen. Das System nutzt einen einzelnen authentifizierten Datensatz, keinen transienten Token-Owner. Ablauf-/Retention-Cleanup ist nicht implementiert; Originale werden nicht automatisch entfernt.
- Datei-Menüs bieten FIT-Upload und Garmin-Geräteimport. USB/MTP liest und kopiert lokal ausschließlich in eigene temporäre Dateien; Aufräumen wartet laufende Uploads ab. FIT-Parsing bleibt in der API. Der App-Code enthält weiterhin Legacy-Data-/Import-Referenzen.

Tests wurden kompiliert, nicht ausgeführt. Laufzeit, Netzwerk/API, Windows-Visualisierung und echte Geräte bleiben unverifiziert.