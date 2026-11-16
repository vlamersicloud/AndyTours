# Touren: Sortierung und Paging (Design)

TourenListenfilter behält seinen positionalen Vier-Argument-Konstruktor; Sortierschlüssel und Richtung sind optionale Init-Properties. Bestand erhält die bestehende Ladeordnung Datum ASC, Startzeit ASC, Id ASC. Die Legacy-WPF-Ansicht zeigt anfangs Datum DESC und Startzeit DESC. Das API-Grid soll dies ausdrücklich anfordern. Bei explizitem Datum folgt Startzeit in gleicher Richtung, danach stets Id ASC; andere Schlüssel erhalten Id ASC als stabilen Tie-Breaker.

Die API-Namenssortierung ist explizit de-DE, case-sensitive; daraus wird keine implizite Legacy-Kultursemantik abgeleitet. Das API-Grid hat sechs Spalten (Datum, Startzeit, Name, Distanz, Höhenmeter, Fahrzeit), das Legacy-Grid 18: Parität ist offen. Aktuell sortieren WPF-Klicks nur die geladene Seite. Die Speicherung muss später vor Paging sortieren; Vertrag und UI-/Clientintegration sind getrennte Arbeitsschritte.
