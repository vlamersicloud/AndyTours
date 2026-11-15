# 036 – Installation ohne Altdatenübernahme

## Was und warum
Das Installationsprogramm und das Handbuch richten sich an normale Anwender. Die Übernahme
von Daten aus einem anderen Programm wurde nur vom Entwickler einmalig gebraucht. Sie
verschwindet deshalb aus der Installation und aus dem Handbuch, ebenso die Übernahme eines
Ordners mit reinen Garmin-Aufzeichnungen bei der Installation; dafür gibt es in AndyTours den
Import von FIT-Dateien. Das Umwandlungswerkzeug bleibt im Projekt und funktioniert für den
Entwickler wie bisher.

## Kriterien
- Wenn der Benutzer AndyTours neu installiert, dann fragt das Installationsprogramm nur nach dem Datenordner und den Verknüpfungen; eine Seite zur Übernahme bisheriger Daten oder vorhandener Aufzeichnungen gibt es nicht.
- Wenn der Benutzer vor dem Installieren die Zusammenfassung ansieht, dann nennt sie Programmordner, Datenordner und Verknüpfungen, aber keine Übernahme.
- Wenn die Installation abgeschlossen ist, dann enthält der Programmordner kein Umwandlungsprogramm, und das Installationsprotokoll erwähnt keins.
- Wenn der gewählte Datenordner leer ist oder noch nicht existiert, dann startet AndyTours nach der Installation mit einem leeren Datenbestand.
- Wenn im gewählten Datenordner schon AndyTours-Daten liegen, dann weist das Installationsprogramm kurz darauf hin, dass sie weiter verwendet werden, und AndyTours verwendet sie unverändert.
- Wenn der gewählte Datenordner andere Dateien, aber keine AndyTours-Daten enthält, dann fragt das Installationsprogramm, ob der Ordner trotzdem verwendet werden soll; die Frage erwähnt keine Übernahme.
- Wenn auf dem Rechner eine ältere Version mit Umwandlungsprogramm installiert ist und der Benutzer die neue Version installiert, dann ist das Umwandlungsprogramm danach aus dem Programmordner entfernt, und Datenordner und Daten bleiben unverändert.
- Wenn ein Anwender das Handbuch liest, dann findet er weder eine Anleitung noch einen Hinweis zur Übernahme bisheriger Daten aus einem anderen Programm.
- Wenn ein Anwender schon Garmin-Aufzeichnungen (FIT-Dateien) auf dem Rechner hat, dann erklärt das Handbuch im Kapitel Installation oder Erste Schritte, dass er sie mit „Datei → FIT-Dateien importieren…“ in AndyTours übernehmen kann.
- Wenn der Entwickler das Umwandlungswerkzeug aus dem Projekt baut und aufruft, dann wandelt es Altdaten weiterhin wie bisher um.
