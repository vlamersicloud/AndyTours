# Funde 3 – Daten

Geprüft: alle 55 Dateien unter `src/AndyTours.Daten` (Tabellen, Einstellungen, Datenordner, Datensicherung).

Freigabe: _offen_

## Ungenutzter Code
Kein Fund. Alles Öffentliche wird außerhalb seiner Datei verwendet, alles Private in seiner Datei, und
jede öffentliche Stelle hat einen XML-Kommentar.

## Doppelte Logik
| Nr. | Fund | Dateien | Vorschlag | Wirkung |
|---|---|---|---|---|
| 3.1 | Zwölf Einstellungsklassen schreiben ihre SQL-Befehle für die Tabelle `Einstellung` jeweils selbst aus (`SELECT Wert … WHERE Schluessel = …`, `INSERT OR REPLACE …`, `DELETE …`, `… LIKE 'Bereich.%'`) | `AktivitaetenReihenfolgeEinstellung`, `AusgeblendeteSpaltenEinstellung`, `AuswahllisteEinstellung`, `FensterpositionEinstellung`, `GpxOrdnerEinstellung`, `KarteikarteEinstellung`, `OberflaechengroesseEinstellung`, `OverlayfarbeEinstellung`, `PlanungseinstellungenEinstellung`, `RoutingEinstellung`, `SicherungEinstellungen`, `SpaltenanordnungEinstellung` | neue Klasse `EinstellungTabelle` mit `Lesen(schluessel)`, `LesenMitVorsilbe(vorsilbe)` und `Schreiben(schluessel, wert)` (`null` löscht); die Einstellungsklassen behalten nur noch das Umwandeln ihrer Werte | geschätzt −150 Zeilen; SQL der Tabelle an einer Stelle |
| 3.2 | `ErstelleSicherung` und `SpieleSicherungZurueck` enthalten dieselben zwei Hilfsmethoden (`Verbindungstext`, `LoescheZwischendatei`) | beide | in die vorhandene Klasse `Sicherungsdatei` verschieben und von beiden aufrufen | −20 Zeilen |
| 3.3 | „Anzahl der Trainingseinheiten“ wird zweimal per SQL ermittelt | `DatenbestandTabellen`, `FuehreStartsicherungDurch` | `TrainingseinheitenTabelle.Zaehlen(verbindung)` | eine Abfrage |
| 3.4 | `WartungenTabelle.ErsetzeTaetigkeit` schreibt den Namensvergleich aus | `WartungenTabelle` | `VergleicheNamen` aus Abschnitt 2 verwenden | einheitlich |

## Veraltete Kommentare
| Nr. | Datei | Heute | Vorschlag |
|---|---|---|---|
| 3.5 | `OeffneDatenbank.cs` | Pooling aus, sonst „könnte die Datei für eine erneute Migration nicht gelöscht werden“ – die Migration gibt es in der App nicht mehr | „… sonst bliebe die Datei gesperrt und ließe sich z. B. beim Wiederherstellen einer Sicherung nicht ersetzen“ |

## Projektregeln
| Nr. | Fund | Vorschlag |
|---|---|---|
| 3.6 | Namen der Einstellungsklassen folgen sonst dem Muster „Thema + `Einstellung`“, zwei weichen ab: `SicherungEinstellungen` (Mehrzahl) und `PlanungseinstellungenEinstellung` (doppelt) | umbenennen in `SicherungEinstellung` und `PlanungEinstellung` (wie `RoutingEinstellung`) |

Sonst eingehalten: eine Klasse je Datei, Dateiname = Klassenname, Fachfunktionen mit `Ausfuehren`.
