# Funde 4 – Import

Geprüft: alle 41 Dateien unter `src/AndyTours.Import` (`Garmin/` für die App, `Altdaten/` für das
Umwandlungswerkzeug).

Freigabe: alle Punkte freigegeben am 15.11.2026, Frage 1: A1 (4.6 bleibt)

## Ungenutzter Code
Kein Fund. Alles Öffentliche wird außerhalb seiner Datei verwendet (der Altdaten-Teil vom
Umwandlungswerkzeug), alles hat einen XML-Kommentar.

| Nr. | Fund | Vorschlag |
|---|---|---|
| 4.1 | `LeseFitAufzeichnung.RadwahlNachricht` ist öffentlich, wird aber nur in der eigenen Datei verwendet | `private` |

## Doppelte Logik
| Nr. | Fund | Dateien | Vorschlag | Wirkung |
|---|---|---|---|---|
| 4.2 | Öffnen und Prüfen einer FIT-Datei (`IsFIT`, `CheckIntegrity`, Zurückspulen) steht zweimal | `LeseFitAufzeichnung`, `LeseFitVerlauf` | gemeinsame Fachfunktion `PruefeFitDatei.Ausfuehren(datei, decoder)` | eine Prüfung an einer Stelle |
| 4.3 | Die Umrechnung zwischen Grad und „Semicircles“ (Koordinaten im FIT-Format) steht in zwei Dateien, je eine Richtung | `LeseFitVerlauf` (`InGrad`), `SchreibeFitCourse` (`Semicircles`) | eine Klasse `Semicircles` mit `InGrad` und `AusGrad` (Hin- und Rückumwandlung eines Formats, wie `DatumSpalte`) | Formel an einer Stelle |
| 4.4 | Die Suche nach Wechsellaufwerken und der Ersatzname „Garmin (E:\)“ stehen zweimal | `FindeLaufwerksgeraete`, `FindeLaufwerksziele` | beides als Hilfen in `FindeLaufwerksgeraete`, von `FindeLaufwerksziele` aufgerufen (wie schon `LeseGeraetename`) | je eine Stelle |
| 4.5 | Namensvergleich ausgeschrieben | `FindeOderErgaenzeAktivitaet`, `FindeOderErgaenzeKategorie`, `FindeOderErgaenzeEquipment` | `VergleicheNamen` verwenden | einheitlich |
| 4.6 | `FindeOderErgaenzeAktivitaet` und `FindeOderErgaenzeKategorie` sind bis auf den Typ gleich | beide | siehe Frage 1 | – |

## Veraltete Kommentare
Kein Fund.

## Projektregeln
Eingehalten: eine Klasse je Datei, Dateiname = Klassenname, Fachfunktionen mit `Ausfuehren`
(`FindeLaufwerksgeraete` und `FindeLaufwerksziele` haben je zwei Varianten von `Ausfuehren`: mit
den echten Laufwerken und mit vorgegebenen Ordnern für die Tests).

## Fragen
1. 4.6 zusammenfassen?
   - A1 (empfohlen): nicht zusammenfassen. Aktivität und Kategorie sind verschiedene Typen ohne
     gemeinsame Grundlage; eine gemeinsame Funktion bräuchte Typparameter und übergebene Funktionen
     und wäre schwerer zu lesen als die zwei kurzen, gleich gebauten Dateien. Der Code gehört nur zum
     einmaligen Umwandlungswerkzeug.
   - A2: zu einer generischen Funktion `FindeOderErgaenze<T>` zusammenfassen (−1 Datei).
