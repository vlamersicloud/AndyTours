# Touren-Editoren: UI-Überarbeitung

## Umfang

Acht Ansichten: Einheit, Equipment, Wartung, Alarm, Saisonziele, Persönliche Daten, Tourdetail und Tourenplanung. Die Änderungen betreffen ausschließlich XAML-Darstellung; ViewModels, Storage, Adapter und Fachlogik bleiben außerhalb des Umfangs. Gemeinsame API-Oberflächenressourcen strukturieren Titel, Kartenflächen und Aktionen.

## Abnahme / offen

- Windows-Sichtabnahme bei 100 %, 150 % und 200 % Skalierung auf kleiner Arbeitsfläche ist offen.
- Die vorbereiteten Vertragsfixtures sind Quellen, wurden aber nicht ausgeführt; Testausführung und fachliche Abnahme durch Volker stehen aus.
- API, Windows-Ziel und Testprojekt wurden auf dem Testserver mit SDK 10.0.402 vollständig (`--no-restore --no-incremental`) gebaut: jeweils 0 Warnungen und 0 Fehler. Das ist kein Laufzeit- oder Windows-Renderingnachweis; keine Tests oder Anwendungen wurden ausgeführt.
- Tastatur/Fokus, Kontrast, Status-/Fehlerzustände sowie Karten- und Diagrammverhalten sind visuell abzunehmen.
