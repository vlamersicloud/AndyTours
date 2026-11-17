# API-Einstellungen und Sicherung

**Umgesetzter erster Schnitt:** Versionierter Vertrag und SQLite-Speicher für Intervall/Aufbewahrung (jeweils 1–99), CAS-Änderung ausschließlich dieser beiden Richtlinienwerte, API-Endpunkte und Client sowie UI-ViewModel/Dialog unter Extras. Der Zähler „Starts seit letzter Sicherung“ wird nur gelesen und bleibt unabhängig von der Richtlinienversion. Der konfigurierte Serverordner wird nicht über die Richtlinien-API geändert; beliebige Pfade werden nicht übertragen. Speichern und explizites Neuladen sind getrennt. Bei Konflikt, unklarem Speicherergebnis oder fehlgeschlagenem Neuladen bleiben Entwürfe erhalten und ein erneutes Speichern erfordert erfolgreiches Neuladen.

Der Schnitt ändert weder Zähler noch Ordner oder andere Einstellungswerte. Er startet keine Sicherung und bietet keine Wiederherstellung.

**Noch offen:** Sicherungsauflistung und sicherer Download über opake IDs; vollständiges versioniertes Archiv aus Datenbank, FIT-Dateien und Anhängen; Legacy-SQLite-only-Sicherungen klar als eingeschränkt kennzeichnen. Wiederherstellung braucht Vorschau/Bestätigung, vorherige Sicherung, datasetweite Schreibsperre und Client-/Cacheinvalidierung. Keine beliebigen Pfade oder unkoordinierte Ersetzung einer aktiven Datenbank.

Kompilierung ist kein Testnachweis: neue Tests wurden nicht ausgeführt; Windows-Darstellung und Laufzeit sind nicht verifiziert. Frühere Testincident-Details stehen in VERIFIKATION.md. Die Gesamtrestmigration bleibt offen.
