# Rekordnavigation in der API-Übersicht

**Design-only; nicht implementiert.**

`BestleistungszeileAntwort` in `src/AndyTours.Contracts/V1/AktivitaetsauswertungVertrag.cs` enthält `TourId` als nullable numerische Identität neben Anzeigetexten. Die Projektionen stehen in `ApiUebersichtViewModel.cs`; Jahres- und Einzelrekorde werden aus der aktuell geladenen `AktivitaetsauswertungAntwort` übernommen. `ApiHauptfenster.xaml` zeigt derzeit Bezeichnung, Wert und Herkunft, nicht `TourId`; kein Name/Datum aus Freitext als Identität ableiten.

Vor Navigation prüfen, dass `TourId` vorhanden und positiv ist. Bestehender Pfad: `ApiHauptfenster.TourdetailAngefordert` übergibt Tour-IDs an `ApiTourdetailAnzeige.Oeffnen`; `App.Api.cs` bindet das an die vorhandene authentifizierte `Tourdetail`-Leseschnittstelle, `CourseExport` und Overlay-Kontext der Sitzung. Navigation muss diesen Pfad wiederverwenden, keine neue HTTP-Sitzung anlegen.

Rekorddaten bleiben an den ursprünglichen Auswertungszeitraum sowie Aktivitäts- und Kategorienfilter gebunden. Jahresrekorde bei Teilauswahl sind keine vollständigen Jahreswerte; Navigation ändert oder erweitert den Filter nicht. UI und Tests für den Rekordklick sowie Windows-Rendering sind offen.
