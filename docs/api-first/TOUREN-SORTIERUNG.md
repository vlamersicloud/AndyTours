# Touren: Sortierung und Paging

Die Sortierung ist für das API-Grid mit sechs Spalten angebunden: Datum, Startzeit, Name, Distanz, Höhenmeter und Fahrzeit. Das typisierte Query nutzt endliche Sortierschlüssel; Name wird explizit de-DE, case-sensitive verglichen. Sortiert wird global vor Paging, mit stabiler Id als Tie-Breaker. Das API-Grid startet wie die Legacy-Ansicht mit Datum DESC, Startzeit DESC, Id ASC. Für alte API-Aufrufer bleibt der Default Bestand (Datum ASC, Startzeit ASC, Id ASC) unverändert.

Sortierpfeile und Seite ändern sich erst nach bestätigter Antwort; veraltete Antworten werden verworfen, fehlgeschlagene Sortierung erhält die bisherige Seite. Die sechs API-Spalten erreichen keine Parität mit den 18 Legacy-Spalten. Bulk-Bearbeitung und übrige Spalten/Funktionen bleiben offen. Die neuen Tests sind kompiliert, nicht ausgeführt; Windows-Laufzeitverhalten ist nicht verifiziert.
