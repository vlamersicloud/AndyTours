# Funde 6 – Umwandlungswerkzeug, Installations- und Bauskripte

Geprüft: `tools/AndyTours.Altdatenuebernahme` (11 Dateien), `tools/AndyTours.Installation`
(`AndyTours.iss`, `Erstelle-Installation.ps1`) und `tools/AndyTours.Programmicon`
(`Erstelle-Programmicon.ps1`). Das Werkzeug wurde in Spec 036 bereits auf eine einzige Aufrufart
verkleinert.

Freigabe: _offen_

## Ungenutzter Code
| Nr. | Fund | Datei | Vorschlag |
|---|---|---|---|
| 6.1 | `LegeLeerAn` und `StandardQuellordner` sind öffentlich, werden aber nur in der eigenen Klasse verwendet | `ErstelleZwischenordner.cs`, `LeseAufrufparameter.cs` | `private` |

Im Installationsskript wird jede Funktion und jede Konstante verwendet. `AltesWerkzeug` bleibt: Es
entfernt beim Update einer älteren Installation die frühere Datei `AndyTours.Altdatenuebernahme.exe`.

## Doppelte Logik
Kein Fund.

## Veraltete Kommentare
Kein Fund („später“ steht nur im Sinn von „danach im Ablauf“, z. B. „eine spätere Installation“).

## Projektregeln
Eingehalten: eine Klasse je Datei, Fachfunktionen mit `Ausfuehren`, deutsche Kommentare in Skripten.
