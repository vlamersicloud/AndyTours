# Plan: 039 – Code-Überarbeitung

## Vorgehen
Die Überarbeitung läuft in sieben Abschnitten. Jeder Abschnitt hat denselben Ablauf: prüfen →
Fundliste schreiben → Freigabe durch den Benutzer → umsetzen → bauen und testen → committen.

**Ausgangsstand (zum Vergleich am Ende):** 45.477 Zeilen in `.cs`-, `.xaml`-, `.iss`- und `.ps1`-Dateien unter `src/`, `tools/` und `tests/` (Core 8.499, Daten 4.301, Import 2.331, App 16.564, tools 1.207, tests 12.575), 1054 Tests, 0 Warnungen.

**Abschnitt 1 – ungenutzte Werte aus dem Datenbestand entfernen** (vorab, weil er quer durch alle Projekte geht)
- Kandidaten: am Equipment `GewichtKg`, `RadumfangMm`, `CrCoeff`, `CmCoeff`, `CwCoeff`, `CwaCoeff`, `SinCoeff`, `Kettenblaetter`, `Ritzel`, `Bildpfad`; am Fahrer `TrainingspulsVon`, `TrainingspulsBis`, `HeimatBreitengrad`, `HeimatLaengengrad`; die ganze Tabelle `Schwellwert` mit `Schwellwert` und `SchwellwertArt`. Sie werden nur gelesen, gespeichert und durchgereicht. Die endgültige Auswahl steht in `funde-1-datenbestand.md` und braucht die Freigabe des Benutzers.
- Schemaversion 5. Ein neuer Datenbestand entsteht gleich ohne diese Spalten und ohne die Tabelle.
- Ein älterer Datenbestand (Version 1–4) wird beim Öffnen umgebaut (`BaueSchemaUm`):
  - Zuerst legt `ErstelleSchema` fehlende Tabellen an, ohne die Version zu setzen.
  - Dann entfernt `ALTER TABLE … DROP COLUMN` die Spalten, und `DROP TABLE Schwellwert` entfernt die Tabelle. Das unterstützt die mit `Microsoft.Data.Sqlite` gelieferte SQLite-Version.
  - Danach steht die Version auf 5.
  - Alles läuft in einer Transaktion: Entweder ist der Umbau vollständig, oder nichts ist geändert.
- **Sicherung vor dem Umbau:** `OeffneDatenbank` erkennt die ältere Version, bevor es etwas ändert. Dann legt es zuerst mit `ErstelleSicherung` eine Sicherung im eingestellten Sicherungsordner an, unabhängig vom Intervall.
  - Schlägt das fehl, baut es nichts um und meldet einen eigenen Fehler. Der Programmstart zeigt dann eine verständliche Meldung und endet (Entscheidung des Benutzers). Ein Wechsel des Datenordners in den Einstellungen meldet den Fehler und behält den bisherigen Datenordner.
  - `SpieleSicherungZurueck` öffnet die Zwischendatei ohne diese zusätzliche Sicherung, weil es den aktuellen Stand ohnehin vorher sichert.
- `PruefeSicherung` verlangt die Tabelle `Schwellwert` nicht mehr. Ältere Sicherungen bleiben gültig und werden beim Wiederherstellen umgebaut.
- Das Umwandlungswerkzeug übernimmt diese Werte nicht mehr: Lesecode, Bericht und Zusammenfassung werden angepasst.

**Abschnitte 2–7 – Prüfung je Bereich:** 2 Core, 3 Daten, 4 Import, 5 App (C# und XAML), 6 Umwandlungswerkzeug sowie Installations- und Bauskripte, 7 Tests. Geprüft wird jeweils auf:
- **Ungenutzter Code:** Für jede öffentliche Klasse, Methode und Eigenschaft wird nach Verwendungen außerhalb ihrer eigenen Datei gesucht. Verwendung nur in Tests zählt als ungenutzt. Dazu kommen die Analyzer-Meldungen des Builds.
- **Doppelte Logik:** gleiche oder fast gleiche Berechnungen, Abfragen und Prüfungen.
- **Umständliches:** zu lange Methoden und Umwege, die sich einfacher schreiben lassen.
- **Veraltete Kommentare:** „später“, „Phase“, entfallene Funktionen, Widersprüche zum Code.
- **Projektregeln:** eine Datei je Aufgabe, Namen, Kommentare, Fluent-Stile.
- **Tests (Abschnitt 7):** doppelte oder überflüssige Tests, Fachfunktionen ohne Test.

Jeder Fund kommt mit Datei, Begründung und geschätzter Wirkung in die Fundliste des Abschnitts. Größere Umbauten stehen dort als eigener Punkt. Betreffen sie Projektstruktur oder eine ADR, wird vor der Umsetzung `/sdd-adr` empfohlen. Umgesetzt wird nur, was der Benutzer freigibt.

**Am Ende:**
- Die Zeilen werden neu gezählt und mit dem Ausgangsstand verglichen.
- Das Installationsprogramm wird gebaut.
- Das Umwandlungswerkzeug läuft gegen eine Kopie von `Daten/` in einen leeren Testordner.
- `docs/tech.md`, `docs/structure.md` und die Projektbeschreibung werden nachgezogen, falls sich etwas Beschriebenes geändert hat.

## Dateien
Abschnitt 1 (bekannt):
- `src/AndyTours.Core/Modell/Equipment.cs` (ändern) — ungenutzte Eigenschaften entfernen
- `src/AndyTours.Core/Modell/Fahrer.cs` (ändern) — ungenutzte Eigenschaften entfernen
- `src/AndyTours.Core/Modell/Schwellwert.cs` (löschen)
- `src/AndyTours.Core/Modell/SchwellwertArt.cs` (löschen)
- `src/AndyTours.Core/Modell/Datenbestand.cs` (ändern) — Liste der Schwellwerte entfällt
- `src/AndyTours.Core/Modell/Equipmenteingaben.cs` (ändern) — Verweise auf entfernte Werte
- `src/AndyTours.Core/Berechnungen/EquipmentEingaben.cs` (ändern) — Durchreichen entfällt
- `src/AndyTours.Core/Berechnungen/FahrerEingaben.cs` (ändern) — Durchreichen entfällt
- `src/AndyTours.Core/Berechnungen/UebernimmEquipment.cs` (ändern) — Durchreichen entfällt
- `src/AndyTours.Daten/ErstelleSchema.cs` (ändern) — Schemaversion 5, neue Tabellen ohne die Spalten, Version nicht mehr pauschal setzen
- `src/AndyTours.Daten/BaueSchemaUm.cs` (neu) — Umbau von Version 1–4 auf 5 in einer Transaktion
- `src/AndyTours.Daten/OeffneDatenbank.cs` (ändern) — Version erkennen, Sicherung vor dem Umbau, Umbau aufrufen; Schalter für den Fall „Zwischendatei beim Wiederherstellen“
- `src/AndyTours.Daten/SicherungVorUmbauFehlgeschlagenException.cs` (neu) — Fehler, wenn die Sicherung vor dem Umbau scheitert
- `src/AndyTours.Daten/EquipmentsTabelle.cs` (ändern) — Spalten in Lesen und Schreiben
- `src/AndyTours.Daten/FahrerTabelle.cs` (ändern) — Spalten in Lesen und Schreiben
- `src/AndyTours.Daten/SchwellwerteTabelle.cs` (löschen)
- `src/AndyTours.Daten/DatenbestandTabellen.cs` (ändern) — Schwellwerte laden/speichern entfällt
- `src/AndyTours.Daten/Datensicherung/PruefeSicherung.cs` (ändern) — Pflichttabellen ohne `Schwellwert`
- `src/AndyTours.Daten/Datensicherung/SpieleSicherungZurueck.cs` (ändern) — Zwischendatei ohne zusätzliche Sicherung öffnen
- `src/AndyTours.App/App.xaml.cs` (ändern) — Meldung und Programmende, wenn die Sicherung vor dem Umbau scheitert
- `src/AndyTours.App/ViewModels/EinstellungenViewModel.cs` (ändern) — Meldung beim Wechsel des Datenordners
- `src/AndyTours.Import/Altdaten/WandleEquipments.cs` (ändern)
- `src/AndyTours.Import/Altdaten/WandleFahrer.cs` (ändern)
- `src/AndyTours.Import/Altdaten/WandleSchwellwerte.cs` (löschen)
- `src/AndyTours.Import/Altdaten/AltEinstellungen.cs` (ändern)
- `src/AndyTours.Import/Altdaten/LeseBikexperienceIni.cs` (ändern)
- `src/AndyTours.Import/Altdaten/IniDatei.cs` (ändern)
- `src/AndyTours.Import/Altdaten/UebernehmeAltdaten.cs` (ändern)
- `src/AndyTours.Import/Altdaten/ErstelleBerichtstext.cs` (ändern)
- `tools/AndyTours.Altdatenuebernahme/ErstelleZusammenfassung.cs` (ändern)
- `tests/AndyTours.Tests/Daten/SchemaumbauTests.cs` (neu) — Umbau von Version 4 auf 5, Sicherung vorher, Fehlschlag ohne Umbau, Transaktion, Wiederherstellen einer alten Sicherung
- `tests/AndyTours.Tests/Daten/DatenbankTests.cs` (ändern)
- `tests/AndyTours.Tests/Daten/EquipmentsTests.cs` (ändern)
- `tests/AndyTours.Tests/Daten/FahrerTests.cs` (ändern)
- `tests/AndyTours.Tests/Berechnungen/EquipmentEingabenTests.cs` (ändern)
- `tests/AndyTours.Tests/Berechnungen/FahrerEingabenTests.cs` (ändern)
- `tests/AndyTours.Tests/Altdaten/IniUndStammdatenTests.cs` (ändern)
- `tests/AndyTours.Tests/Altdatenuebernahme/UmwandlungTests.cs` (ändern)

Fundlisten:
- `specs/tourenverwaltung/039-code-ueberarbeitung/funde-1-datenbestand.md` (neu)
- `specs/tourenverwaltung/039-code-ueberarbeitung/funde-2-core.md` (neu)
- `specs/tourenverwaltung/039-code-ueberarbeitung/funde-3-daten.md` (neu)
- `specs/tourenverwaltung/039-code-ueberarbeitung/funde-4-import.md` (neu)
- `specs/tourenverwaltung/039-code-ueberarbeitung/funde-5-app.md` (neu)
- `specs/tourenverwaltung/039-code-ueberarbeitung/funde-6-werkzeuge.md` (neu)
- `specs/tourenverwaltung/039-code-ueberarbeitung/funde-7-tests.md` (neu)

Abschnitte 2–7: Welche weiteren Dateien unter `src/`, `tools/` und `tests/` sich ändern oder entfallen, steht erst in der freigegebenen Fundliste des Abschnitts. Die Fundliste nennt jede Datei mit der Aktion `ändern` oder `löschen`.

Dokumentation, soweit nötig: `docs/tech.md`, `docs/structure.md`, `Beschreibung der fundamentals.md`.

## Abdeckung der Kriterien
- Wenn ein Bereich geprüft ist, dann bekommt der Benutzer eine Liste der vorgeschlagenen Änderungen mit kurzer Begründung, bevor etwas geändert wird, und umgesetzt wird nur, was er freigibt. → Fundlisten `funde-1` bis `funde-7`; geprüft: jede Fundliste trägt den Vermerk der Freigabe, und jeder Commit eines Abschnitts setzt nur freigegebene Punkte um (Abgleich Fundliste ↔ Diff).
- Wenn eine vorgeschlagene Änderung die Projektstruktur oder eine festgehaltene Architekturentscheidung betrifft, dann wird sie vor der Umsetzung als eigene Architekturentscheidung festgehalten. → Kennzeichnung in der Fundliste, `/sdd-adr` vor der Umsetzung; geprüft: zu jedem so gekennzeichneten Punkt gibt es eine ADR in `docs/adr/`.
- Wenn jemand den Code liest, dann findet er keine Klassen, Methoden, Eigenschaften oder Einstellungen mehr, die nirgends verwendet werden. → Abschnitte 1–6; geprüft: die Suche nach Verwendungen über alle öffentlichen Bezeichner findet am Ende nichts Ungenutztes außer bewusst begründeten Ausnahmen in den Fundlisten, und der Build zeigt keine Analyzer-Meldung zu ungenutztem Code.
- Wenn zwei Stellen dasselbe leisten, dann gibt es diese Logik nur noch einmal. → Funde „doppelte Logik“; geprüft: jeder freigegebene Fund ist umgesetzt (Abgleich Fundliste ↔ Code).
- Wenn jemand die Kommentare liest, dann beschreiben sie den heutigen Stand, ohne Hinweise auf „später“, „Phase …“ oder entfallene Funktionen. → Funde „veraltete Kommentare“; geprüft: Suche nach „später“, „Phase“, „Ritzelrechner“, „Leistungsberechnung“ und ähnlichen Begriffen findet keine veralteten Stellen mehr.
- Wenn jemand den Code liest, dann folgt er durchgehend den Projektregeln: eine Datei je Aufgabe, Namensregeln, ausführliche deutsche Kommentare. → Funde „Projektregeln“; geprüft: Abgleich mit `docs/Konventionen.md`, Build ohne Warnungen (`dotnet build AndyTours.slnx`).
- Wenn die Überarbeitung fertig ist, dann ist der Code insgesamt nicht umfangreicher als vorher. → geprüft: erneute Zeilenzählung wie im Ausgangsstand ergibt höchstens 45.477 Zeilen.
- Wenn jemand die Tests liest, dann gibt es keine doppelten oder überflüssigen Tests mehr, und jede Fachfunktion hat weiterhin Tests. → Abschnitt 7; geprüft: zu jeder Klasse unter `src/AndyTours.Core/Berechnungen` und jeder Fachfunktion in Daten und Import gibt es einen Test, der sie aufruft.
- Wenn das Programm nach der Überarbeitung benutzt wird, dann verhalten sich alle Funktionen wie vorher, alle Tests bestehen und der Build zeigt keine Warnungen. → nach jedem Abschnitt `dotnet build AndyTours.slnx` und `dotnet test AndyTours.slnx`; am Ende manuelle Prüfung: Programm starten, Karteikarten, Bearbeiten, Import aus FIT-Datei, Tourdetail, Wartung, Einstellungen durchgehen.
- Wenn der Benutzer einen Datenbestand einer früheren Version zum ersten Mal öffnet, dann entfernt AndyTours die nicht mehr verwendeten Werte, und alle übrigen Daten bleiben unverändert und nutzbar. → `BaueSchemaUm`, `OeffneDatenbank`; geprüft durch `SchemaumbauTests` (Datenbank der Version 4 mit Werten in allen Tabellen, nach dem Umbau sind die Spalten weg und alle übrigen Werte gleich); manuell: App mit einer Kopie von `AppDaten` starten.
- Wenn AndyTours einen Datenbestand einer früheren Version so umbaut, dann legt es vorher eine Sicherung des bisherigen Stands an. → `OeffneDatenbank`; geprüft durch `SchemaumbauTests` (Sicherung mit Version 4 vorhanden; bei nicht beschreibbarem Sicherungsordner kein Umbau und Fehler); manuell: nach dem ersten Start liegt im Sicherungsordner eine neue Sicherung.
- Wenn der Benutzer eine Sicherung einer früheren Version wiederherstellt, dann wird sie ebenso umgebaut und ist danach vollständig nutzbar. → `SpieleSicherungZurueck`, `PruefeSicherung`; geprüft durch `SchemaumbauTests` (Sicherung der Version 4 zurückspielen, danach Version 5 und alle übrigen Daten vorhanden).
- Wenn das Installationsprogramm gebaut und das Umwandlungswerkzeug aufgerufen werden, dann funktionieren beide wie vorher; das Umwandlungswerkzeug übernimmt die nicht mehr verwendeten Werte nicht mehr. → `tools/`; geprüft durch `UmwandlungTests` und `IniUndStammdatenTests`; manuell: `powershell tools/AndyTours.Installation/Erstelle-Installation.ps1` und Umwandlung einer Kopie von `Daten/` in einen leeren Testordner.

## Entscheidungen
- Scheitert die Sicherung vor dem Umbau, wird nicht umgebaut; der Programmstart zeigt eine Meldung und endet (Entscheidung des Benutzers). Beim nächsten Start wird es erneut versucht.
- Die Sicherung vor dem Umbau liegt in `OeffneDatenbank`, damit jeder Weg zu einem älteren Datenbestand (Programmstart, Wechsel des Datenordners) sie auslöst. Nur die Zwischendatei beim Wiederherstellen ist davon ausgenommen, weil dort der aktuelle Stand schon gesichert ist und die Quelle selbst eine Sicherung ist.
- Der Umbau läuft in einer Transaktion, damit ein Abbruch keinen halb umgebauten Datenbestand hinterlässt.
- Abschnitt 1 kommt vor den übrigen, weil er quer durch alle Projekte geht und die späteren Fundlisten sonst Code bewerten würden, der ohnehin entfällt.

## Offene Entscheidungen
- Wie werden ältere Datenbestände an ein geändertes Datenbankschema angepasst (Umbau beim Öffnen anhand der Schemaversion, vorher Sicherung, alles in einer Transaktion)? → decided in docs/adr/0010-anpassung-des-datenbankschemas.md

## Risiken
- `ALTER TABLE … DROP COLUMN` braucht SQLite 3.35 oder neuer; das wird mit einem Test der mitgelieferten Version abgesichert.
- Abschnitt 5 (App, 16.500 Zeilen) ist groß; die Fundliste wird dort nach Fenstern gegliedert, damit die Freigabe überschaubar bleibt.
- Der Umbau ist für den Benutzer nicht umkehrbar, außer über die Sicherung; deshalb wird er zuerst mit einer Kopie von `AppDaten` geprüft, nie direkt mit dem echten Datenordner.
