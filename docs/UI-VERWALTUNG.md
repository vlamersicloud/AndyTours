# Verwaltungsdialoge – UI-Überarbeitung

## Umgesetzt

Die zehn Verwaltungsfenster für Aktivitäten, Kategorien, Equipment, Wartungen/Alarme, Equipment-Einsatz, Textlisten, Routing, Garmin-Einstellungen, Touren-Mehrfachaktionen und Overlays verwenden nun die vorhandenen benannten API-Oberflächenressourcen. Inhalte und Erklärungen sind nach Bedarf begrenzt scrollbar; Tabellen behalten ihre Sortier-/Auswahlbindungen und Virtualisierung. Aktionsbereiche umbrechen oder sind thematisch im begrenzten Scrollbereich gruppiert; Schließen und vorhandene Status-/Ladeanzeigen bleiben fest erreichbar. Aktivitäten und Kategorien halten Editor und Aktionsgruppen zunächst eingeklappt, damit Tabellenfläche erhalten bleibt. Die bestehende Oberflächenskalierung ist in den betroffenen Fenstern nach `InitializeComponent` registriert.

Fachliche Befehle, Bestätigungen, Versions- und Speichergates, Schlüsselmaskierung, Overlay-Auswahl sowie Lebenszyklus-/Adapterlogik wurden nicht erweitert. Es gibt keine neue Freigabe-, Restore-, Lösch- oder Netzwerkaktion. Das Ressourcenwörterbuch und die Backend-/ViewModel-Logik wurden nicht geändert.

## Abnahme

- Bereits beobachteter vollständiger SDK-10.0.402-Build für API, AppWindows und Test mit `--no-restore --no-incremental`: 0 Warnungen, 0 Fehler. In diesem Korrekturschritt nicht erneut ausgeführt.
- Tests wurden nicht ausgeführt; eine visuelle Abnahme unter Windows ist weiterhin offen.
- Noch visuell abzunehmen: 100 %, 150 % und 200 % auf kleiner Windows-Arbeitsfläche; Größenbegrenzung, Tastatur/Fokus, Kontrast, Status-/Lade-/Fehler-/Leerzustände, Tabellen und Diagramme.
- Die vorbereiteten XML-/Quellvertragsfixtures wurden hier nicht ausgeführt. Testausführung und fachliche Abnahme liegen bei Volker.
- Weitere Ansichten und Dialoge außerhalb der zehn genannten Fenster bleiben UI-Restumfang.
