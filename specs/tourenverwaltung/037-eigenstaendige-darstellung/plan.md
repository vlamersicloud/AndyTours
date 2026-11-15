# Plan: 037 – Eigenständige Darstellung

## Vorgehen
- **Programm:** Zwei sichtbare Stellen ändern sich.
  - `UeberViewModel.Beschreibung` wird „Tourenverwaltung für Radltouren.“
  - Das Grundziel einer Aktivität heißt in der Spalte „Herkunft“ jetzt „Grundziel“. Dafür heißt die Eigenschaft `WirksamesSaisonziel.AusBikeXperience` künftig `IstGrundziel`.
  - An der Datenbank ändert sich nichts. Das Grundziel steht wie bisher in der Tabelle `Aktivitaet`, vorhandene Datenbestände bleiben unverändert nutzbar.
- **Code-Kommentare und Bezeichner:** Jeder Verweis auf das Vorbildprogramm außerhalb der Ausnahmen entfällt oder wird neutral formuliert. Dazu gehören auch indirekte Verweise wie „entspricht `datamodel.…`“, „Java-Klasse …“ und „wie im Original“. Wo der Verweis etwas erklärt, bleibt die Erklärung ohne den Namen stehen, z. B. „Grundziel der Aktivität“ statt „Grundziel aus BikeXperience“.
- **Projektregeln:** Die Regel, nachgebildete Klassen des Vorbilds zu nennen, entfällt in `docs/tech.md` und `docs/Konventionen.md`.
- **Dokumentation:** Handbuch, README, Projektgrundlagen, Map, Funktionsauswahl, die Specs, Pläne und Aufgaben 001–036 sowie die Projektbeschreibung werden neutral formuliert, mit unverändertem fachlichem Inhalt.
  - Wo ein Satz nur den Bezug zum Vorbild herstellt, z. B. „wie in BikeXperience“, entfällt der Bezug.
  - Wo das Vorbild als Datenquelle gemeint ist, heißt es „Altdaten“.
  - Ihre noch nicht committete Änderung an `funktionsauswahl.md` wird übernommen.
- **Analysedokument:** `docs/Analyse_und_Plan_Neuprogrammierung.md` wird gelöscht, samt Eintrag in der Projektmappe (Entscheidung des Benutzers).
- **Ausnahmen:** Diese Stellen behalten den Namen, weil sie nur das Werkzeug bzw. seine Quelldaten beschreiben:
  - `tools/AndyTours.Altdatenuebernahme/`
  - `src/AndyTours.Import/Altdaten/`
  - `tests/AndyTours.Tests/Altdaten/` und `tests/AndyTours.Tests/Altdatenuebernahme/`
  - `specs/tourenverwaltung/002-altdaten-umwandeln/`
  - in `docs/structure.md` die Zeilen zu `src/AndyTours.Import/`, `tools/AndyTours.Altdatenuebernahme/` und `Daten/`
  - die beiden Einträge in `.gitignore`
  - Abschnitt 9 „Altdaten“ der Projektbeschreibung
  - in Plan 036 die Aufrufzeile des Werkzeugs
  - Im README und in `docs/product.md` wird das Werkzeug dagegen ohne den Namen beschrieben, als „Altdaten“.
- **Prüfung:** Eine Suche nach „BikeXperience“, „datamodel.“, „workouts.csv“ und „Vorbild“ über alle versionierten Dateien außerhalb der Ausnahmen findet nichts mehr. Ausgenommen sind Spec und Plan 037 selbst, die das Thema beschreiben.

## Dateien
- `src/AndyTours.App/ViewModels/UeberViewModel.cs` (ändern) — neuer Beschreibungstext
- `src/AndyTours.App/Views/Hauptfenster.xaml` (ändern) — Kommentar zur Reihenfolge der Karteikarten
- `src/AndyTours.App/Views/SaisonzieleFenster.xaml` (ändern) — Kopfkommentar zur Spalte „Herkunft“
- `src/AndyTours.Core/Modell/WirksamesSaisonziel.cs` (ändern) — `IstGrundziel`, Herkunft „Grundziel“
- `src/AndyTours.Core/Berechnungen/ErmittleSaisonziel.cs` (ändern) — Kommentare; neuer Eigenschaftsname
- `src/AndyTours.Core/Berechnungen/BerechneDurchschnittsgeschwindigkeit.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Berechnungen/ErmittleBestleistungen.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Berechnungen/ErmittleJahresfortschritt.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Berechnungen/ErstelleImportierteEinheit.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Berechnungen/GeraetezuordnungText.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Berechnungen/IstTourVorhanden.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Modell/Aktivitaet.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Modell/Radkennungsart.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Modell/Saisonziel.cs` (ändern) — Kommentar
- `src/AndyTours.Core/Modell/Equipment.cs` (ändern) — Verweis auf `datamodel.PowerCalc`
- `src/AndyTours.Core/Modell/Wartung.cs` (ändern) — Verweis auf `datamodel.Maintenance`
- `src/AndyTours.Core/Modell/Trainingseinheit.cs` (ändern) — Verweis auf `workouts.csv`/`TourEntry`
- `src/AndyTours.Daten/ErstelleSchema.cs` (ändern) — SQL-Kommentar zum Grundziel
- `src/AndyTours.Import/Garmin/LeseFitAufzeichnung.cs` (ändern) — Kommentar
- `tests/AndyTours.Tests/Berechnungen/ErmittleSaisonzielTests.cs` (ändern) — Testname, Kommentar, erwartete Herkunft „Grundziel“
- `docs/Analyse_und_Plan_Neuprogrammierung.md` (löschen)
- `AndyTours.slnx` (ändern) — Eintrag des gelöschten Dokuments
- `docs/product.md` (ändern) — Vorbild, Parallelbetrieb, Ziele; Werkzeug als „Altdaten“
- `docs/tech.md` (ändern) — Regel „nachgebildete Klassen nennen“ entfällt
- `docs/Konventionen.md` (ändern) — dieselbe Regel und weitere Nennungen
- `README.md` (ändern) — Einleitung, Funktionsliste, Hinweis
- `HANDBUCH.md` (ändern) — Kapitel 1 (Nachfolger, „Was das Programm nicht kann“), Saisonziele (Herkunft „Grundziel“), Equipment („Altwert:“)
- `Beschreibung der fundamentals.md` (ändern) — alle Nennungen außer Abschnitt 9; Zitat der Regel aus den Konventionen
- `specs/tourenverwaltung/map.md` (ändern) — Umfang und Funktionseinträge
- `specs/tourenverwaltung/funktionsauswahl.md` (ändern)
- `specs/tourenverwaltung/001-datenordner-waehlen/plan.md` (ändern)
- `specs/tourenverwaltung/004-datensicherung/spec.md` (ändern)
- `specs/tourenverwaltung/006-einheiten-tabelle/spec.md` (ändern)
- `specs/tourenverwaltung/007-einheiten-filter/spec.md` (ändern)
- `specs/tourenverwaltung/008-einheit-bearbeiten/spec.md` (ändern)
- `specs/tourenverwaltung/010-aktivitaeten-kategorien-verwalten/spec.md` (ändern)
- `specs/tourenverwaltung/011-equipments-verwalten/spec.md` (ändern)
- `specs/tourenverwaltung/013-persoenliche-daten/spec.md` (ändern)
- `specs/tourenverwaltung/013-persoenliche-daten/plan.md` (ändern)
- `specs/tourenverwaltung/014-saisonziele/spec.md` (ändern)
- `specs/tourenverwaltung/014-saisonziele/plan.md` (ändern)
- `specs/tourenverwaltung/016-bestleistungen/spec.md` (ändern)
- `specs/tourenverwaltung/017-garmin-import/spec.md` (ändern)
- `specs/tourenverwaltung/017-garmin-import/plan.md` (ändern)
- `specs/tourenverwaltung/018-hoehenprofil/spec.md` (ändern)
- `specs/tourenverwaltung/019-streckenkarte/spec.md` (ändern)
- `specs/tourenverwaltung/020-mehrtagestour/spec.md` (ändern)
- `specs/tourenverwaltung/021-wartung-erfassen/spec.md` (ändern)
- `specs/tourenverwaltung/021-wartung-erfassen/plan.md` (ändern)
- `specs/tourenverwaltung/022-wartungsalarme/spec.md` (ändern)
- `specs/tourenverwaltung/022-wartungsalarme/plan.md` (ändern)
- `specs/tourenverwaltung/023-teile-haltbarkeit/spec.md` (ändern)
- `specs/tourenverwaltung/027-equipment-einsatz/spec.md` (ändern)
- `specs/tourenverwaltung/028-gpx-export/spec.md` (ändern)
- `specs/tourenverwaltung/029-track-bearbeiten/spec.md` (ändern)
- `specs/tourenverwaltung/030-tourenplanung-karte/spec.md` (ändern)
- `specs/tourenverwaltung/033-installationsprogramm/spec.md` (ändern)
- `specs/tourenverwaltung/033-installationsprogramm/plan.md` (ändern)
- `specs/tourenverwaltung/034-erscheinungsbild-und-icon/spec.md` (ändern)
- `specs/tourenverwaltung/034-erscheinungsbild-und-icon/plan.md` (ändern)

## Abdeckung der Kriterien
- Wenn der Benutzer „Hilfe → Über AndyTours“ öffnet, dann beschreibt das Fenster AndyTours mit „Tourenverwaltung für Radltouren.“, ohne ein anderes Programm zu nennen. → `UeberViewModel.cs`; manuelle Prüfung: App starten, **Hilfe → Über AndyTours** öffnen und den Text lesen.
- Wenn für eine Aktivität das Grundziel gilt, weil es noch kein eigenes Saisonziel gibt, dann zeigt die Spalte „Herkunft“ dafür „Grundziel“. → `WirksamesSaisonziel.cs`, `ErmittleSaisonziel.cs`; geprüft durch `ErmittleSaisonzielTests` mit `dotnet test AndyTours.slnx`.
- Wenn der Benutzer Fenster, Menüs, Meldungen oder Hinweise des Programms ansieht, dann nennt keine davon ein Vorbild- oder Vorgängerprogramm. → alle Dateien unter `src/` außer `src/AndyTours.Import/Altdaten/`; geprüft durch die Suche (siehe Vorgehen) über `src/`, die nichts findet.
- Wenn der Benutzer einen vorhandenen Datenbestand aus einer früheren Version öffnet, dann ist er unverändert nutzbar, und die Grundziele erscheinen mit der Bezeichnung „Grundziel“. → keine Schemaänderung; manuelle Prüfung: App mit `AppDaten` starten, **Extras → Saisonziele…** für ein Jahr vor dem ersten eigenen Ziel öffnen, die Herkunft zeigt „Grundziel“.
- Wenn ein Anwender das Handbuch liest, dann beschreibt es AndyTours als eigenständiges Programm und nennt kein anderes Programm als Vorbild oder Vorgänger. → `HANDBUCH.md`; geprüft durch die Suche über `HANDBUCH.md`.
- Wenn ein Anwender den Abschnitt „Was das Programm nicht kann“ liest, dann sind dort die nicht enthaltenen Funktionen weiter aufgezählt, ohne Bezug auf ein anderes Programm. → `HANDBUCH.md`, Kapitel 1; manuelle Prüfung: Abschnitt lesen, alle bisherigen Punkte sind da.
- Wenn jemand README, Projektgrundlagen, Konventionen, Architekturentscheidungen, Feature-Map, Funktionsauswahl, Spezifikationen samt Plänen und Aufgaben oder die vollständige Projektbeschreibung liest, dann nennen sie kein Vorbildprogramm; ausgenommen sind Stellen, die das Umwandlungswerkzeug oder seine Quelldaten beschreiben (die Spezifikation des Umwandlungswerkzeugs, die Beschreibung des Ordners mit den Altdaten und der Ausschluss der Originalprogrammdatei aus der Versionsverwaltung). → die Dokumente unter Dateien; geprüft durch die Suche über alle versionierten Dateien außerhalb der Ausnahmen.
- Wenn jemand den Programmcode samt Kommentaren, Bezeichnern und Tests liest, dann nennt er kein Vorbildprogramm; ausgenommen sind das Umwandlungswerkzeug, sein Lesecode und dessen Tests. → Code- und Testdateien unter Dateien; geprüft durch die Suche über `src/` und `tests/` außerhalb der Ausnahmen.
- Wenn jemand die Projektregeln liest, dann verlangen sie nicht mehr, bei nachgebildeten Teilen das Vorbild zu nennen. → `docs/tech.md`, `docs/Konventionen.md`; manuelle Prüfung: Abschnitte „Coding-Standards“ bzw. „Kommentare“ lesen.
- Wenn ältere Spezifikationen, Pläne und Aufgaben bereinigt werden, dann bleibt ihr fachlicher Inhalt erhalten. → Specs und Pläne unter Dateien; manuelle Prüfung: Diff der Specs durchsehen, nur Bezüge zum Vorbild sind entfernt oder neutral formuliert, kein Kriterium ist weggefallen.
- Wenn der Entwickler das Umwandlungswerkzeug aufruft, dann wandelt es weiterhin wie bisher um. → keine Änderung an `tools/AndyTours.Altdatenuebernahme/` und `src/AndyTours.Import/Altdaten/`; geprüft durch `UmwandlungTests` mit `dotnet test AndyTours.slnx`.
- Wenn das Programm nach der Änderung benutzt wird, dann verhalten sich alle Funktionen wie vorher. → nur Kommentare, ein Anzeigetext und ein Bezeichner ändern sich; geprüft durch alle Tests mit `dotnet test AndyTours.slnx` und den Build ohne Warnungen.

## Entscheidungen
- `docs/Analyse_und_Plan_Neuprogrammierung.md` wird gelöscht (Entscheidung des Benutzers); es bleibt in der Git-Historie.
- Im README und in `docs/product.md` wird das Umwandlungswerkzeug neutral als Werkzeug für „Altdaten“ beschrieben, obwohl die Ausnahme den Namen dort erlauben würde. Diese Dateien sind die öffentliche Beschreibung des Projekts.
- `WirksamesSaisonziel.AusBikeXperience` heißt künftig `IstGrundziel`.

## Risiken
- Die Specs, Pläne und die Projektbeschreibung sind viel Text. Beim Umformulieren kann versehentlich fachlicher Inhalt verloren gehen. Gegenmittel: nur die betroffenen Sätze ändern und den Diff vor dem Commit durchsehen.
