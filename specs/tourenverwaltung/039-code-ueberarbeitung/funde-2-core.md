# Funde 2 – Core

Geprüft: alle 189 Dateien unter `src/AndyTours.Core` (Modell und Berechnungen).

Freigabe: alle Punkte freigegeben am 15.11.2026, Frage 1: A1

## Ungenutzter Code
Kein Fund. Jede öffentliche Klasse, Methode, Eigenschaft und Konstante wird außerhalb ihrer Datei
verwendet (der Aufzählungswert `Einzelrekordart.HoechsterPunkt` über `Enum.GetValues`), jede
private wird in ihrer Datei verwendet, und jede öffentliche hat einen XML-Kommentar.

## Doppelte Logik
| Nr. | Fund | Dateien | Vorschlag | Wirkung |
|---|---|---|---|---|
| 2.1 | Der Vergleich „gleicher Name ohne Rücksicht auf Groß-/Kleinschreibung und Leerzeichen am Rand“ steht 13-mal ausgeschrieben im Core (`string.Equals(x.Trim(), y, StringComparison.OrdinalIgnoreCase)`) | `AlarmEingaben`, `WartungEingaben`, `BerechneAlarmstand`, `BerechneHaltbarkeit`, `BenenneKategorieUm`, `BenenneTaetigkeitUm`, `BenenneTrainingsbereichUm`, `FuehreKategorienZusammen`, `FindeStandardkategorie`, `PruefeOrdnername`, `ZaehleTaetigkeit`, `ZaehleTrainingsbereich`, `ZaehleVerwendung` | neue Fachfunktion `VergleicheNamen.Ausfuehren(a, b)` mit Test; alle Stellen rufen sie auf (die Stellen in App und Daten folgen in deren Abschnitten) | kürzere, einheitliche Zeilen; eine Regel an einer Stelle |
| 2.2 | `ErmittleTaetigkeiten` und `ErmittleTrainingsbereiche` sind bis auf das gelesene Feld gleich (Namen trimmen, leere weglassen, Doppelte entfernen, sortieren) | beide Dateien und Tests, 2 Aufrufer in der App | zusammenfassen zu `ErmittleNamensliste.Ausfuehren(IEnumerable<string> namen)`; Aufrufer übergeben die Tätigkeiten bzw. Trainingsbereiche | −1 Datei, −1 Testdatei |
| 2.3 | `ZaehleTaetigkeit` und `ZaehleTrainingsbereich` sind ebenso gleich (Tätigkeit zusätzlich nach Typ gefiltert) | beide Dateien und Tests, Aufrufer in der App | zusammenfassen zu `ZaehleNamen.Ausfuehren(IEnumerable<string> namen, string gesucht)`; der Aufrufer filtert vorher nach Typ | −1 Datei, −1 Testdatei |
| 2.4 | Das Ersetzen der Standardkategorie an den Equipments steht zweimal | `BenenneKategorieUm`, `FuehreKategorienZusammen` | beide rufen eine gemeinsame Fachfunktion `ErsetzeStandardkategorie.Ausfuehren(equipments, alt, neu)` auf | eine Schleife statt zwei |
| 2.5 | Das Ersetzen unzulässiger Zeichen in Dateinamen (`\/:*?"<>|` und Steuerzeichen → „-“) steht zweimal | `BildeGpxDateiname`, `BildeCoursename.Dateiname` | gemeinsame Fachfunktion `BereinigeDateiname.Ausfuehren(name)` mit Test | eine Regel an einer Stelle |
| 2.6 | Kleine Ungleichheit: `BenenneTaetigkeitUm` trimmt den neuen Namen, `BenenneTrainingsbereichUm` nicht | beide | beide trimmen (die App übergibt ohnehin getrimmte Namen, das Verhalten bleibt gleich) | einheitlich |

Bewusst nicht zusammengefasst: `ErstelleGpx` (Spuren gefahrener Touren) und `PlanungsGpx`
(Planungsdatei mit eigenen Erweiterungen) schreiben beide GPX-Punkte. Die Überschneidung sind drei
Zeilen; eine gemeinsame Hilfsklasse würde zwei sonst unabhängige Formate koppeln.

## Veraltete oder falsche Kommentare
| Nr. | Datei | Heute | Vorschlag |
|---|---|---|---|
| 2.7 | `Modell/Einheitszeile.cs` | Nummer „für spätere Funktionen wie Bearbeiten“ | „Nummer der Einheit in der Datenbank (zum Bearbeiten, Löschen und Markieren)“ |
| 2.8 | `Modell/Jahressumme.cs` | „wird verwendet, um nach der Übernahme der Altdaten zu prüfen …“ | heutige Verwendung nennen: Grundlage der Jahresrekorde (`ErmittleBestleistungen`) |
| 2.9 | `Modell/Fahrer.cs` | Geburtstag „für altersabhängige Pulsformeln“, Gewicht „Vorschlag für neue Einheiten“, Heimatort „Startpunkt der Kartenanzeige“ – nichts davon gibt es | sagen, was stimmt: Die Werte werden im Fenster „Persönliche Daten“ gepflegt und sonst nicht ausgewertet |

## Projektregeln
| Nr. | Fund | Vorschlag |
|---|---|---|
| 2.10 | Neun Fachfunktionen mit Verb-Namen haben mehrere öffentliche Methoden statt genau einer `Ausfuehren`: `AenderePlanung` (7: Anhaengen, Einfuegen, Entfernen …), `BildeCourse` (AusPlanung, AusVerlauf), `BildeCoursename` (FuerPlanung, FuerEtappe, Dateiname), `ErmittleLoeschsperre` (Aktivitaet, Equipment, Kategorie), `UebernimmEquipment` (Ausfuehren, Einsetzen, Kopie), `BeschreibeAbbiegung` (+Kurz), `BeschreibeJahresfortschritt` (+Ueberschrift), `ErmittleStandardEquipment` (+IstWirksam), `UebernimmZellwert` (+Rohtext) | siehe Frage 1 |

Sonst eingehalten: eine Klasse je Datei, Dateiname = Klassenname, Namen ohne Umlaute, XML-Kommentare vollständig.

## Fragen
1. Wie mit 2.10 umgehen?
   - A1 (empfohlen): Regel in `docs/tech.md` präzisieren – eine Verb-Klasse darf **Varianten derselben Aufgabe** als benannte Methoden haben (z. B. `ErmittleLoeschsperre.Equipment`). Nur `AenderePlanung` (sieben verschiedene Änderungen) wird zum Substantiv `Planungsaenderung` umbenannt. Begründung: Die Varianten lesen sich gut, Aufteilen würde neun zusätzliche Dateien ohne Gewinn erzeugen.
   - A2: Alle neun streng nach Regel: Substantiv-Namen oder Aufteilen in einzelne Fachfunktionen (ca. 12 neue Dateien).
   - A3: so lassen.

## Hinweis (nicht Teil dieser Überarbeitung)
Die Werte im Fenster „Persönliche Daten“ (Geburtstag, Größe, Gewicht, Puls, Heimatort) werden
nirgends ausgewertet. Ob das Fenster bleiben soll, ist eine Funktionsänderung und gehört, wenn
gewünscht, in eine eigene Spec.
