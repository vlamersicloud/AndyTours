# Touren: Sortierung und Paging

Das API-Grid sortiert alle 18 Anzeige-Spalten über endliche typisierte Schlüssel serverseitig vor dem Paging. Lookup-Namen, Nullwerte und Bestwertmarker folgen der aktuellen gefilterten Projektion; Namen werden de-DE, case-sensitive sortiert, stabile Gleichstände folgen der ID. Der erste Klick auf eine neue Spalte ist aufsteigend. Initial zeigt das Grid Datum DESC; Startzeit DESC und ID ASC bleiben Tie-Breaker. Der API-Default `Bestand` bleibt unverändert.

Seitenantworten werden vor Übernahme validiert; fehlende oder inkonsistente Anzeige lässt die bisherige Seite bestehen. Sortierpfeile und Seite ändern sich erst nach bestätigter Antwort. Optionale fehlende `Anzeige` bleibt für ältere HTTP-Aufrufer lesbar, aber die UI weist solche Seiten zurück.

Tests vorbereitet/kompiliert, nicht ausgeführt; Windows-Rendering ungeprüft. Inline-Bearbeitung, Filterparität/weitere Legacy-Filter, Spaltenpräferenzen und Auswahl über Seiten bzw. alle Filtertreffer bleiben offen.