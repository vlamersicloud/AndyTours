# Vollarchiv-Erstellung: UI-Entwurfsplan

## Voraussetzungen
- Aktuelle Muster zeigen WPF/App-Composition und Sicherungseinstellungen-VM. Es gibt noch keine Vollarchiv-UI oder Pending-Operation-Journal.
- Der Client-POST hat ein ungewisses Ergebnis bei Transportabbruch: niemals beim erneuten Öffnen/Start eine neue GUID erzeugen.

## Dauerhafte Pending-Operation
- Vor dem ersten POST muss eine lokale, dauerhafte Operation-ID in einem Journal gespeichert sein. Scope ist die kanonische Backend-Basisadresse; keine Tokens, Secrets, Backup-/Serverpfade oder Nutzdaten persistieren.
- Journaldatei pro Backend unter `%LOCALAPPDATA%\\AndyTours`, Dateiname aus SHA-256 der kanonischen Basisadresse. Speicher-/Lesefehler oder beschädigtes/mehrdeutiges Journal sperren die Aktion mit sicherer Meldung, statt eine neue ID zu erzeugen.
- Clientprozesse teilen das Journal: exklusive Dateisperre (`FileShare.None`) über Lesen/Entscheiden/Schreiben und die gesamte Operation; Lockdatei persistent öffnen/erzeugen, nicht CreateNew-Stale-Lock. Pending atomar und crash-resilient schreiben. Persistenz schützt nicht absolut gegen Stromausfall/Dateisystemfehler.
- Bei Transportfehler, Cancellation, 409, 503 oder ungültiger Antwort Pending-ID behalten und „Mit derselben Vorgangs-ID erneut versuchen“ anbieten. Nur nach validierter Erfolgsantwort genau diese ID löschen; falls Löschen scheitert, neue Erstellung blockieren.

## UX/Lifecycle
- Dialog zeigt Erstellen/Erneut versuchen, laufenden Status und ausschließlich validierte Ergebnis-Metadaten; kein Restore/Download/Löschen.
- Währenddessen Aktionen und Schließen kontrollieren. Nach Schließen keine UI-Updates mehr; Pending bleibt für späteren Retry erhalten. Keine synchrone I/O auf UI-Thread.
- Backend-Schlüssel (`SicherungsjournalBackend`) und Journal-Port (`ISicherungsjournal`) sind implementiert; vorbereitete Schlüsselfakten sind kompiliert, nicht ausgeführt. Die dauerhafte Journalimplementierung und UI fehlen noch. Keine Katalogänderung; kein Laufzeit-/Persistenznachweis.
