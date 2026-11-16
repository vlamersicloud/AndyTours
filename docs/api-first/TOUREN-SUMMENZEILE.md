# Touren-Summenzeile

Der Server berechnet die optionale Tabellensumme aus allen durch Kategorie- und Tabellenfilter getroffenen Touren vor Sortierung und Paging. Dazu verwendet er innerhalb desselben Speicher-Snapshots Core `BerechneAuswahlsummen`: Anzahl, auf zwei Stellen gerundete Distanz, Höhenmeter und Summe der ursprünglichen Fahrzeit-Ticks; erst danach wird die Gesamtdauer in Sekunden übertragen. Werkzeugleisten-Kennzahlen bleiben getrennt.

Die WPF-Seite zeigt die geprüfte Summe als „Alle Filtertreffer“ und übernimmt sie gemeinsam mit Seite und Optionen. Bei älteren Antworten ohne Summenmetadatum erscheint ein Versionshinweis statt einer vorgetäuschten Nullsumme. Tour-IDs, Versionen und Hashes werden nicht verändert.

Die Presenter-/Übernahme-Fakten wurden vorbereitet und kompiliert, Tests nicht erneut ausgeführt; der frühere Testvorfall ist in VERIFIKATION.md dokumentiert. Legacy-Spaltenausrichtung und synchrones horizontales Scrollen sind nicht umgesetzt. Windows-Laufzeit und Rendering sind ungeprüft; die Gesamtmigration bleibt offen. Getrennte Statistik-Requests werden nicht als transaktionsübergreifend konsistent behauptet.

Quellen: Core BerechneAuswahlsummen; API_FIRST Tourenspeicher, Contracts und WPF-ViewModel.
