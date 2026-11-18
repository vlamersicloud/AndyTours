# Inline-Touren – Quellpaket für gemeinsames Review

Basis laut bereitgestelltem Auftrag: 36479ba. Keine lokale Git-Abfrage; keine Änderung am früheren SQLite-Paket. Alle Pfade unten relativ zum Repository; Inhalt liegt unter next/. Kein Transfer, Build, Testlauf oder Push in dieser Arbeit.

## Neue Quellen
- src/AndyTours.App/ViewModels/ApiTourInlineEntwurf.cs
- src/AndyTours.App/ViewModels/ApiTourInlineSpeicher.cs
- src/AndyTours.App/ViewModels/ApiUebersichtViewModel.Inline.cs
- src/AndyTours.App/Views/ApiHauptfenster.Inline.cs
- tests/AndyTours.Tests/Berechnungen/ApiTourInlineTests.cs
- tests/AndyTours.Tests/Berechnungen/ApiTourInlineStrukturTests.cs
- docs/api-first/INLINE-TOUREN.md
- docs/api-first/INLINE-PAKET-MANIFEST.md

## Geänderte bestehende Quellen
- src/AndyTours.App/ViewModels/ApiUebersichtViewModel.cs
- src/AndyTours.App/ViewModels/ApiUebersichtViewModel.Touren.cs
- src/AndyTours.App/ViewModels/ApiUebersichtViewModel.Auswahl.cs
- src/AndyTours.App/ViewModels/ApiUebersichtViewModel.Sortierung.cs
- src/AndyTours.App/Views/ApiHauptfenster.xaml
- src/AndyTours.App/Views/ApiHauptfenster.xaml.cs
- src/AndyTours.App/Views/ApiHauptfenster.Sortierung.cs
- src/AndyTours.App/Views/ApiHauptfenster.ManuelleAuswahl.cs
- src/AndyTours.App/Views/ApiHauptfenster.Mehrfach.cs
- tests/AndyTours.Tests/AndyTours.Tests.csproj
- tests/AndyTours.Tests/Darstellung/ApiHauptfensterSortierungVertragTests.cs
- tests/AndyTours.Tests/Darstellung/ApiHauptfensterVertragTests.cs
- tests/AndyTours.Tests/Darstellung/ApiOberflaechenVertragTests.cs
- docs/api-first/UMSTELLUNG.md

## Unveränderte Referenzen, nicht als Änderungen übertragen
Contracts/TourenVertrag und StammdatenVertrag, Application/ITourenSpeicher und TourAusnahmen, Core/EinheitEingaben/Eingabetext/UebernimmZellwert/EquipmentAnzeigename, Legacy-Zelltextspalte und Legacy-Tests dienen nur dem Signatur-/Paritätsabgleich. Keine neue Core-/Daten-Abhängigkeit in den Inlinequellen; kein zusätzlicher Endpoint.

## Gebündelte Reviewkorrekturen
Ersttext-Erhalt samt Lookup-Wiedereinstieg, Draft-ohne-Send-zu-Editdialog auf Row-Doppelklick mit Timestamp-Dedup, lokale Textlängen, separater Backend-Ungueltig-Status, malformed Saveantworten mit Reload statt Bestätigung, explizite Auswahlnotification bei Edit/Busy, deferred Filterinvalidierung und Lookup-Vollständigkeitsprüfung sind in den verbundenen Quellen korrigiert. Pure Writer-/Draft-Fixtures und Strukturassertions sind erweitert. Drei nachgelieferte bestehende Vertragstests behalten ihre Sort-/Style-/Header-/Virtualisierungsprüfungen und erwarten nun die tatsächlichen editierbaren Column-/Keyflow-Verträge. Keine Testausführung und kein Buildnachweis.

## Nachweisgrenzen / gemeinsames Review
Die nachgelieferte Originalquelle src/AndyTours.App/Views/ApiHauptfenster.Mehrfach.cs ist abgeglichen und geändert: FuehreMehrfachAus lehnt Inline-Edit/Send und beendete Fensterlebenszeit vor Snapshot-/Dialogstart ab; TourenDataGrid_PreviewKeyDownMehrfach überlässt Delete dem aktiven Editor; Lebt verwirft Ergebnisse bereits nach finalem Inline-Cleanup. Die vorhandenen Snapshot-Kopien, ID-/Versionsvalidierung, expliziten Dialogcallbacks, Bestaetigt-only-Auswahlentfernung und Mehrfach-Retry-/Refreshrückmeldungen bleiben unverändert. Der konkrete fehlende Handlerquellen-Anschluss ist damit geschlossen.

Das Testprojekt behält seine bestehenden Ressourcenverweise auf weitere nicht lokal mitgelieferte Baselinequellen; diese werden im vollständigen Repository benötigt. Neue Compilelinks betreffen nur Draft/Speicher, neue EmbeddedResources nur Inlinequellen.

Das verbundene 22-Dateien-Quellpaket wurde gemeinsam geprüft und korrigiert. API, Windows-Ziel und Testprojekt wurden auf 192.168.178.32 mit SDK 10.0.402 vollständig gebaut, jeweils 0 Warnungen / 0 Fehler. Logs: `/tmp/andytours-inline-parity-{api,app,tests}-final-20261118.log`. Dies ist Compile-Evidenz, keine Laufzeitfreigabe. Tests ausschließlich durch Auftraggeber; Windows-/Mehrclient-Bedienung ist nicht verifiziert.
