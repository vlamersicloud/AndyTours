# Touren-Summenzeile – Designnotiz

Die Legacy-Summenzeile bildet alle Tabellenfilter-Treffer ab – nicht nur markierte Zeilen oder die aktuelle Seite. Core `BerechneAuswahlsummen` liefert Anzahl, auf zwei Nachkommastellen gerundete Distanz, Höhenmetersumme und `TimeSpan` aus der Summe der ursprünglichen Ticks. Die Werkzeugleistenstatistik bleibt davon getrennt.

Vorschlag: optionaler, typisierter `TourListeAntwort.Tabellensumme`-Vertrag. Der Speicher berechnet ihn mit derselben gefilterten Snapshot-Liste vor Sortierung/Paging durch den bestehenden Core-Aufruf. Nur die Gesamtticks werden danach für den Transport in Sekunden umgerechnet; nicht je Tour vorab runden. Die UI beschriftet die Werte ausdrücklich als Summe aller Filtertreffer. Seite und Summe werden gemeinsam validiert, bevor UI-Zustand übernommen wird; bestehende Tour-IDs, Versionen und Hashes bleiben unberührt.

Dies ist Design, keine Implementierung. Keine Aussage über transaktionsübergreifende Einheit mit getrennten Statistik-Requests. Spaltenausrichtung, Auswahl über Seiten und Windows-Rendering bleiben zu prüfen; die Gesamtmigration ist offen.
