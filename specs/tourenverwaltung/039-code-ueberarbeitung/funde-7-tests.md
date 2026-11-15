# Funde 7 – Tests

Geprüft: alle 156 Dateien unter `tests/AndyTours.Tests` (1055 Testfälle). Gesucht wurde nach Tests mit
gleichem Inhalt, nach gleich benannten Tests für dieselbe Sache, nach Tests für Code, den nur die Tests
verwenden, und – mit einer Abdeckungsmessung – nach Fachfunktionen ohne Test.

Freigabe: freigegeben am 15.11.2026

## Fachfunktionen ohne Test
Kein Fund. Jede Klasse in Core, Daten, Import und im Umwandlungswerkzeug wird von Tests durchlaufen.
17 davon haben keine eigene Testdatei, werden aber über die aufrufende Funktion geprüft (z. B.
`DatumSpalte` über die Tabellentests, `LeseWorkoutsCsv` über die Umwandlungstests,
`EinstellungTabelle` über die Tests der Einstellungsklassen). Eigene Tests dafür würden den Umfang nur
vergrößern.

## Doppelte oder überflüssige Tests
| Nr. | Fund | Datei | Vorschlag | Wirkung |
|---|---|---|---|---|
| 7.1 | Zwei Theorien mit gleichem Rumpf: `Ein_Wort_in_Name_oder_Kommentar_passt` und `Mehrere_Woerter_in_beliebiger_Reihenfolge` | `Berechnungen/PasstZumSuchbegriffTests.cs` | zu einer Theorie `Passende_Suchbegriffe` mit allen Beispielen zusammenfassen | −1 Testmethode |
| 7.2 | Die Prüfung „beschädigte Datei“ und „keine FIT-Datei“ steht in den Tests beider FIT-Leser; seit Abschnitt 4 prüft das die gemeinsame `PruefeFitDatei` | `Garmin/LeseFitAufzeichnungTests.cs`, `Garmin/LeseFitVerlaufTests.cs` | beide Fälle nur noch in `LeseFitVerlaufTests` prüfen; in `LeseFitAufzeichnungTests` bleibt nur „keine FIT-Datei“ als Nachweis, dass auch dieser Leser prüft | −1 Test |

Gleich benannte Tests in verschiedenen Dateien (z. B. `Punkte_mit_Position_und_Hoehe` für BRouter
und openrouteservice) prüfen verschiedene Funktionen und bleiben.

Code, den nur Tests verwenden: keiner (geprüft in den Abschnitten 2–6).

## Projektregeln
Eingehalten. Einige Testdateien sind nach Themen statt nach einer Klasse benannt (z. B.
`DatenbankTests`, `NamenTests`); das ist gewollt, weil sie zusammengehörige Funktionen gemeinsam
prüfen.
