# Plan: 036 – Installation ohne Altdatenübernahme

## Vorgehen
- **Installationsprogramm (`AndyTours.iss`):** Die Seite „Bisherige Daten übernehmen“ entfällt mit allem, was dazugehört: Haken, Ordnerwahl, Prüfen des Ordners mit `--erkennen`, Entpacken und Aufrufen des Umwandlungsprogramms, Rückgabecodes. Die Seitenfolge ist dann Datenordner → Verknüpfungen → Zusammenfassung.
  - Das Umwandlungsprogramm wird nicht mehr installiert (beide Einträge unter `[Files]` und `#define Werkzeug`/`WerkzeugQuelle` fallen weg).
  - Die Zusammenfassung nennt nur noch Programmordner, Datenordner (bzw. bei einer Aktualisierung den Hinweis zur Aktualisierung) und Verknüpfungen.
  - Prüfung des Datenordners: Liegen dort schon AndyTours-Daten, erscheint der Hinweis „Im Datenordner liegen bereits AndyTours-Daten. Sie werden weiter verwendet.“ Liegen dort andere Dateien, lautet die Frage „Der Ordner enthält bereits andere Dateien. Diesen Ordner trotzdem verwenden?“ ohne Hinweis auf eine Übernahme. Die Variable `DatenordnerHatInhalt` wird damit überflüssig.
  - Aktualisierung: Ein neuer Abschnitt `[InstallDelete]` entfernt `{app}\AndyTours.Altdatenuebernahme.exe`. Das Installationsprogramm hält das im Protokoll als Zeile mit der Aktion `entfernt` fest, wenn die Datei vorher da war. Die Deinstallation liest nur Art und Ziel; ein erneutes Löschen einer fehlenden Datei ist ohne Wirkung.
  - Bei einer Neuinstallation wird der Datenordner wie bisher angelegt, falls er fehlt; die App beginnt dann mit einem leeren Datenbestand (an der App ändert sich nichts).
  - Kopfkommentar und Kommentare an die neue Seitenfolge anpassen.
- **Bauskript (`Erstelle-Installation.ps1`):** Schritt 3 (Umwandlungsprogramm veröffentlichen), `$WerkzeugAusgabe` und `/DWerkzeugQuelle` entfallen; Beschreibung im Kopf anpassen.
- **Umwandlungsprogramm:** Die nur für das Installationsprogramm gebauten Aufrufarten `--erkennen`, `--aufzeichnungen` und `--ohne-rueckfrage` entfallen samt Code und Tests. Übrig bleibt der Aufruf `AndyTours.Altdatenuebernahme [BikeXperience-Ordner] [Zielordner]` mit der Rückfrage (j/N) bei vorhandenen Daten, unverändert im Verhalten. `Rueckgabecode` behält nur `Erfolg` und `Fehler`.
- **Handbuch:** Alle Stellen zur Übernahme bei der Installation entfallen (siehe Dateien). Abschnitt 2.3 entfällt, die folgenden Abschnitte rücken auf (2.3 Neue Version, 2.4 Installationsprotokoll, 2.5 Deinstallieren). In 3.5 „Die erste Tour vom Garmin Edge importieren“ kommt ein Hinweis, dass vorhandene FIT-Dateien mit **Datei → FIT-Dateien importieren…** übernommen werden (Verweis auf 4.2). Weitere Nennungen von BikeXperience, die nichts mit der Installation zu tun haben, bleiben für 037.

## Dateien
- `tools/AndyTours.Installation/AndyTours.iss` (ändern) — Übernahme-Seite, Werkzeug und Rückgabecodes entfernen; Meldungen zum Datenordner und Zusammenfassung anpassen; `[InstallDelete]` für das alte Umwandlungsprogramm samt Protokollzeile
- `tools/AndyTours.Installation/Erstelle-Installation.ps1` (ändern) — Umwandlungsprogramm nicht mehr veröffentlichen
- `tools/AndyTours.Altdatenuebernahme/Program.cs` (ändern) — nur noch Umwandeln; Kopfkommentar und Aufrufhinweis anpassen
- `tools/AndyTours.Altdatenuebernahme/Aufrufparameter.cs` (ändern) — nur Quell- und Zielordner
- `tools/AndyTours.Altdatenuebernahme/LeseAufrufparameter.cs` (ändern) — Schalter entfernen
- `tools/AndyTours.Altdatenuebernahme/Rueckgabecode.cs` (ändern) — nur `Erfolg` und `Fehler`
- `tools/AndyTours.Altdatenuebernahme/ErstelleZwischenordner.cs` (ändern) — Hinweis auf `ErstelleAufzeichnungsordner` im Kommentar entfernen
- `tools/AndyTours.Altdatenuebernahme/Programmmodus.cs` (löschen)
- `tools/AndyTours.Altdatenuebernahme/ErstelleAufzeichnungsordner.cs` (löschen)
- `tools/AndyTours.Altdatenuebernahme/Aufzeichnungsergebnis.cs` (löschen)
- `tools/AndyTours.Altdatenuebernahme/ErstelleAufzeichnungsbericht.cs` (löschen)
- `src/AndyTours.Import/Altdaten/ErkenneAltdaten.cs` (löschen)
- `src/AndyTours.Import/Altdaten/Altdatenfund.cs` (löschen)
- `src/AndyTours.Import/Altdaten/Altdatenart.cs` (löschen)
- `tests/AndyTours.Tests/Altdatenuebernahme/LeseAufrufparameterTests.cs` (ändern) — Tests der entfernten Schalter streichen; bisheriger Aufruf bleibt geprüft
- `tests/AndyTours.Tests/Altdatenuebernahme/AufzeichnungsuebernahmeTests.cs` (löschen)
- `tests/AndyTours.Tests/Altdatenuebernahme/ErstelleAufzeichnungsberichtTests.cs` (löschen)
- `tests/AndyTours.Tests/Altdaten/ErkenneAltdatenTests.cs` (löschen)
- `HANDBUCH.md` (ändern) — Kapitel 1: Satz zur Übernahme bei der Installation; 2.2: Einleitung, Schritte 4, 5 und 9, Zusammenfassung in Schritt 7, Hinweis zum vorhandenen Datenordner; 2.3 entfällt, Nummern rücken auf; Aktualisierung: Satz zur Übernahme-Seite; 3.5: Hinweis auf den FIT-Import; Glossar: Eintrag mit Verweis auf 2.3

## Abdeckung der Kriterien
Die manuellen Prüfungen laufen mit dem frisch gebauten Installationsprogramm (`powershell tools/AndyTours.Installation/Erstelle-Installation.ps1`) und einem Test-Datenordner, nicht mit `AppDaten`. Für die Aktualisierung wird vorher die bisherige Version 1.0.1 installiert.

- Wenn der Benutzer AndyTours neu installiert, dann fragt das Installationsprogramm nur nach dem Datenordner und den Verknüpfungen; eine Seite zur Übernahme bisheriger Daten oder vorhandener Aufzeichnungen gibt es nicht. → `AndyTours.iss` (Seiten); manuelle Prüfung: Installationsprogramm durchgehen, es erscheinen nur Datenordner, Verknüpfungen und Zusammenfassung.
- Wenn der Benutzer vor dem Installieren die Zusammenfassung ansieht, dann nennt sie Programmordner, Datenordner und Verknüpfungen, aber keine Übernahme. → `AndyTours.iss` (`UpdateReadyMemo`); manuelle Prüfung der Seite „Bereit zur Installation“.
- Wenn die Installation abgeschlossen ist, dann enthält der Programmordner kein Umwandlungsprogramm, und das Installationsprotokoll erwähnt keins. → `AndyTours.iss` (`[Files]`), `Erstelle-Installation.ps1`; manuelle Prüfung: `%LOCALAPPDATA%\Programs\AndyTours` enthält keine `AndyTours.Altdatenuebernahme.exe`, `installationsprotokoll.txt` nennt sie nicht.
- Wenn der gewählte Datenordner leer ist oder noch nicht existiert, dann startet AndyTours nach der Installation mit einem leeren Datenbestand. → `AndyTours.iss` (Datenordner anlegen, Merkdatei); manuelle Prüfung: mit einem nicht vorhandenen Ordner installieren, AndyTours starten, die Tabelle ist leer und die Statusleiste zeigt den Ordner.
- Wenn im gewählten Datenordner schon AndyTours-Daten liegen, dann weist das Installationsprogramm kurz darauf hin, dass sie weiter verwendet werden, und AndyTours verwendet sie unverändert. → `AndyTours.iss` (`PruefeDatenordner`); manuelle Prüfung: Kopie eines Datenordners wählen, Hinweis erscheint, nach dem Start sind dieselben Touren da.
- Wenn der gewählte Datenordner andere Dateien, aber keine AndyTours-Daten enthält, dann fragt das Installationsprogramm, ob der Ordner trotzdem verwendet werden soll; die Frage erwähnt keine Übernahme. → `AndyTours.iss` (`PruefeDatenordner`); manuelle Prüfung: Ordner mit einer Textdatei wählen, Frage lesen; „Nein“ bleibt auf der Seite, „Ja“ geht weiter.
- Wenn auf dem Rechner eine ältere Version mit Umwandlungsprogramm installiert ist und der Benutzer die neue Version installiert, dann ist das Umwandlungsprogramm danach aus dem Programmordner entfernt, und Datenordner und Daten bleiben unverändert. → `AndyTours.iss` (`[InstallDelete]`, Protokollzeile `entfernt`); manuelle Prüfung: über 1.0.1 installieren, die exe ist weg, das Protokoll hat die Zeile `entfernt`, AndyTours zeigt dieselben Touren.
- Wenn ein Anwender das Handbuch liest, dann findet er weder eine Anleitung noch einen Hinweis zur Übernahme bisheriger Daten aus einem anderen Programm. → `HANDBUCH.md`; manuelle Prüfung: Suche nach „Übernahme“, „bisherige Daten“, „migrationsbericht“ und „aufzeichnungsbericht“ findet nichts zur Installation.
- Wenn ein Anwender schon Garmin-Aufzeichnungen (FIT-Dateien) auf dem Rechner hat, dann erklärt das Handbuch im Kapitel Installation oder Erste Schritte, dass er sie mit „Datei → FIT-Dateien importieren…“ in AndyTours übernehmen kann. → `HANDBUCH.md` (3.5); manuelle Prüfung: Hinweis lesen.
- Wenn der Entwickler das Umwandlungswerkzeug aus dem Projekt baut und aufruft, dann wandelt es Altdaten weiterhin wie bisher um. → `tools/AndyTours.Altdatenuebernahme/`; geprüft durch die vorhandenen `UmwandlungTests`, `PruefeZielordnerTests`, `TauscheZielordnerAusTests` und die angepassten `LeseAufrufparameterTests` mit `dotnet test AndyTours.slnx`.

## Entscheidungen
- Die nur für das Installationsprogramm gebauten Aufrufarten des Umwandlungsprogramms (`--erkennen`, `--aufzeichnungen`, `--ohne-rueckfrage`) werden samt Code und Tests entfernt (Entscheidung des Benutzers).
- Das alte Umwandlungsprogramm wird bei einer Aktualisierung über `[InstallDelete]` entfernt und im Protokoll mit der neuen Aktion `entfernt` vermerkt.
- Spec 033, Plan 033 und ADR 0005 bleiben als Verlauf unverändert; sie beschreiben den damaligen Stand.

## Risiken
- Das Installationsprogramm hat keine automatischen Tests; alle Kriterien zur Installation werden von Hand geprüft. Die Aktualisierung lässt sich nur mit einer vorher installierten Version 1.0.1 prüfen.
