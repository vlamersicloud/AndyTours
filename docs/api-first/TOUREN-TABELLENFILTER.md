# API-Tourentabellenfilter – Designnotiz

Legacy `FiltereEinheiten` filtert die aktuelle Jahres-/Aktivitätsauswahl unabhängig von der Werkzeugleisten-Summen: Kategorie (Name, bei Mehrfachnamen alle passenden IDs), Equipment (inkl. Ausgemustert-Zusatz), Trainingsbereich, inklusive km/hm-Bereiche und Freitext. 0 km gilt bei aktivem Distanzfilter als nicht erfasst; 0 hm ist gültig. Ungültige Zahlenbereiche werden nicht angewendet. Suche teilt an Leerzeichen und verlangt jedes Wort in Name oder Kommentar (OrdinalIgnoreCase); reine Leerzeichen filtern nicht.

`ErmittleFilterwerte` bildet Optionen aus der ungefilterten Jahres-/Aktivitätsbasis, ergänzt eine nicht mehr vorhandene Auswahl und hält ausgemusterte Equipments am Ende. Die API-Werkzeugleistenstatistik bleibt auf Datums-/Aktivitäts-/Kategorienumfang, nicht auf tabelleneigene Filter verengt.

Nächster Schritt: additive optionale Tabellenfilter und Lookup-Optionen prüfen, bestehende vier API-Filterparameter/Kategorie-ID-Semantik erhalten und dieselbe Core-Filterregel serverseitig anwenden, bevor Projektion, Bestwerte, Sortierung und Paging berechnet werden. UI muss Parserfehler anzeigen und Seitenantworten atomar validieren. Keine Anzeige-zu-ID-Ableitung; exakte Legacy-Parität für gleichnamige IDs und seitenübergreifende Auswahl bleibt offen.

Quellen: `src/AndyTours.Core/Berechnungen/FiltereEinheiten.cs`, `LeseFilterbereich.cs`, `PasstZumSuchbegriff.cs`, `ErmittleFilterwerte.cs`, `src/AndyTours.App/ViewModels/HauptfensterViewModel.cs`.