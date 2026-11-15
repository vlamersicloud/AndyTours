# Funde 1 – ungenutzte Werte im Datenbestand

Prüfung: Suche nach jeder Verwendung in `src/`, `tools/` und `tests/`. Keiner der Werte kommt in
der Oberfläche (`src/AndyTours.App`) vor, in keiner Berechnung und in keiner Anzeige. Sie werden
nur aus den Altdaten gelesen, in der Datenbank gespeichert und beim Bearbeiten unverändert
durchgereicht.

Freigabe: _offen_

## Equipment (Tabelle `Equipment`)
| Nr. | Wert | Bedeutung | Heute verwendet in | Vorschlag |
|---|---|---|---|---|
| 1.1 | `GewichtKg` | Gewicht des Rads | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.2 | `RadumfangMm` | Radumfang | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.3 | `CrCoeff` | Rollwiderstandsbeiwert | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.4 | `CmCoeff` | Beiwert, laut Kommentar „wird nicht verwendet“ | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.5 | `CwCoeff` | Luftwiderstandsbeiwert | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.6 | `CwaCoeff` | Luftwiderstandsfläche | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.7 | `SinCoeff` | Beiwert, laut Kommentar „wird nicht verwendet“ | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.8 | `Kettenblaetter` | Zähnezahlen („für den Ritzelrechner“, den es nicht gibt) | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.9 | `Ritzel` | Zähnezahlen („für den Ritzelrechner“) | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.10 | `Bildpfad` | Bild des Rads | nur Lesen/Speichern/Durchreichen | entfernen |

## Fahrer (Tabelle `Fahrer`)
| Nr. | Wert | Bedeutung | Heute verwendet in | Vorschlag |
|---|---|---|---|---|
| 1.11 | `TrainingspulsVon` | untere Grenze Trainingspuls | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.12 | `TrainingspulsBis` | obere Grenze Trainingspuls | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.13 | `HeimatBreitengrad` | Koordinate des Heimatorts | nur Lesen/Speichern/Durchreichen | entfernen |
| 1.14 | `HeimatLaengengrad` | Koordinate des Heimatorts | nur Lesen/Speichern/Durchreichen | entfernen |

Bleibt: `Fahrer.GewichtKg` – wird im Fahrer-Dialog bearbeitet und ist damit in Gebrauch. Der
Ortsname `Heimatort` bleibt ebenfalls.

## Schwellwerte
| Nr. | Wert | Bedeutung | Heute verwendet in | Vorschlag |
|---|---|---|---|---|
| 1.15 | Tabelle `Schwellwert` mit `Schwellwert`, `SchwellwertArt`, `SchwellwerteTabelle`, `WandleSchwellwerte` | Grenzwerte aus den Altdaten | nur aus der Ini gelesen, gespeichert, im Umwandlungsbericht gezählt | ganz entfernen |

## Folgen
- Schemaversion 5; ältere Bestände werden beim Öffnen umgebaut, vorher gesichert (ADR 0010).
- Dateien wie im Plan unter „Abschnitt 1“, zusätzlich `src/AndyTours.Core/Modell/Fahrereingaben.cs`
  (nur Kommentar, der die nicht gepflegten Werte aufzählt).
- Die Werte gehen auch in Ihrem Datenbestand verloren; nur die Sicherung vor dem Umbau enthält
  sie dann noch.
