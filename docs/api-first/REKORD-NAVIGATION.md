# Rekordnavigation in der API-Übersicht

**Navigation implementiert; UI-Tests nur kompiliert, nicht ausgeführt.**

`BestleistungszeileAntwort` in `src/AndyTours.Contracts/V1/AktivitaetsauswertungVertrag.cs` enthält `TourId` als nullable numerische Identität neben Anzeigetexten. Die Projektionen stehen in `ApiUebersichtViewModel.cs`; Jahres- und Einzelrekorde werden aus der aktuell geladenen `AktivitaetsauswertungAntwort` übernommen. `ApiHauptfenster.xaml` bietet in beiden Listen eine explizite Tour-Aktion. Sie ist für fehlende IDs deaktiviert; der Handler prüft positive ID und aktuelle DTO-Referenz. Jahresaggregate besitzen normalerweise keine einzelne `TourId` und bleiben daher nicht navigierbar. Niemals Name/Datum aus Freitext als Identität ableiten.

Vor Navigation prüfen, dass `TourId` vorhanden und positiv ist. Bestehender Pfad: `ApiHauptfenster.TourdetailAngefordert` übergibt Tour-IDs an `ApiTourdetailAnzeige.Oeffnen`; `App.Api.cs` bindet das an die vorhandene authentifizierte `Tourdetail`-Leseschnittstelle, `CourseExport` und Overlay-Kontext der Sitzung. Der Klick verwendet diesen bestehenden Pfad, ohne neue HTTP-Sitzung anzulegen.

Rekorddaten bleiben an den ursprünglichen Auswertungszeitraum sowie Aktivitäts- und Kategorienfilter gebunden. Jahresrekorde bei Teilauswahl sind keine vollständigen Jahreswerte; Navigation ändert oder erweitert den Filter nicht. Identitäts- und XAML-Vertragstests sind vorbereitet und kompiliert, nicht ausgeführt; Windows-Rendering und Bedienung bleiben ungeprüft.
