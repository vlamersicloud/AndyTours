# Vollarchiv-Erstellung: UI- und Journalstand

- UI-/Modal- und App-Composition-Anbindung fehlt weiterhin.
- Vorhanden sind Backend-Schlüssel, Journal-Port und `DateisystemSicherungsjournal`: Pending-ID als 32 ASCII-Hexzeichen, Speicherung vor Rückgabe, asynchrone Dateisystemarbeit, exklusive persistente Lockdatei, private Rechte bei neu angelegten Unix-Pfaden und Löschen nur nach passender expliziter Bestätigung. Unbekannte/corrupt Pending-Dateien bleiben erhalten.
- Journal-Store-Fixtures sind kompiliert, aber nicht ausgeführt. Keine UI-Laufzeit- oder Wiederanlaufprüfung.
- Der Pending-Eintrag muss vor dem POST geöffnet bleiben; bei unklarem Ergebnis bleibt dieselbe ID erhalten. Bestätigung erst nach validiertem Erfolg. Stromausfall-/Filesystem-Durabilität ist keine absolute Garantie; auch externe Manipulation/TOCTOU werden nicht ausgeschlossen.
