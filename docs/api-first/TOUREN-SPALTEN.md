# Tourenspalten im API-Modus

Die API-Übersicht zeigt jetzt 18 schreibgeschützte `TourAnzeigeAntwort`-Spalten aus der serverseitigen Projektion. Lookup-Namen und Bestwertmarker stammen aus der gefilterten Gesamtauswertung; Sortierung und Bestwerte werden vor Paging berechnet. Anzeige bleibt an derselben Tour-ID/Version gekoppelt, während `Werte` und Versionshash unverändert bleiben. Die Anzeige-Fahrzeit nutzt `h:mm:ss`.

`TourAntwort`s optionale Anzeige bleibt wire-kompatibel; die UI-Seitenvalidierung lehnt fehlende/inkonsistente Anzeige ab, bevor eine Seite übernommen wird, und erhält bei Fehlern die bestehende Anzeige. Die typisierte Tourenauswahl und Aktionen verwenden weiterhin `TourAntwort`.

Quellen: `src/AndyTours.Contracts/V1/TourAnzeigeVertrag.cs`, `src/AndyTours.Daten/SqliteTourenSpeicher.cs`, `src/AndyTours.App/ViewModels/ApiTourSeitenRegeln.cs`, `src/AndyTours.App/Views/ApiHauptfenster.xaml`. Tests vorbereitet/kompiliert, nicht ausgeführt; Windows-Rendering ungeprüft. Inline-Bearbeitung, Spaltenausblendung/-reihenfolge/-breite, weitere Legacy-Filter und seitenübergreifende Auswahl bleiben offen.