# API-Saisonfortschritt

Die API-Übersicht kann eine optionale, serverseitig berechnete YTD-Projektion für den exakten Zeitraum 1. Januar bis Stichtag anzeigen: Kilometer, Höhenmeter, Anzahl und Fahrzeitstunden (ohne Fahrzeitziel). Backend-Datum und SQLite-Lesesnapshot werden gemeinsam verwendet; zukünftige Touren sind ausgeschlossen. Aktivität gilt für Ist-Werte und Ziele, Kategorien nur für Ist-Werte. Tabellenfilter ändern die Saisonwerte nicht. Andere Zeiträume oder ältere Backends ohne optionales Feld zeigen leer, nicht nullsummig. Ein Kennzahlwechsel nutzt vorbereitete Texte; bei ungültiger Antwort bleibt die bisherige Anzeige bestehen.

Bestehende Periodenbalken bleiben Zeitraumsauswertungen; eine historische Zielkurve ist weder belegt noch Teil dieser Änderung. Tests wurden kompiliert, aber nicht ausgeführt. Windows-Rendering und Laufzeit sind ungeprüft; die Gesamtmigration bleibt offen.
