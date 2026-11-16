# Tourdetail und Puls – API-Migrationsstatus

Tourdetails sind über typisierte API-Verträge verfügbar. Der Server wählt gespeicherte FIT-Anhänge aus sicheren Datenbankmetadaten, erstellt begrenzte, eigene Snapshots und liest tatsächliche UTC-Zeitstempel, aktive Timerzeit (Pausen ausgeschlossen) sowie vorhandene Messwerte. Fehlende Werte bleiben null; mehrstufige Antworten bewahren Reihenfolge und Lücken.

Die API-Übersicht unterstützt Mehrfachauswahl und öffnet modellose WPF-Tourdetails mit bestehenden Diagrammen, Karte und Trackbearbeitung. Der zuletzt gewählte GPX-Exportordner bleibt lokal gespeichert. Course-Export nutzt Backend-Routing und FIT-Erzeugung; fehlende Abbiegungen liefern eine Warnung, niemals partielle Abbiegungen. Die lokale Geräteübertragung erfolgt erst nach Nutzerauswahl und Bestätigung.

Im API-UI-Pfad liegen weder FIT-Parser, Zugangsdaten noch Datenbankzugriff; vorhandene Legacy-Verweise bleiben bestehen. Course-Anfragen sind auf 128 MiB, Routingantworten auf 8 MiB begrenzt; HTTP-Weiterleitungen sind deaktiviert.

Die API-Pulsauswertung verwendet dieselben Datums-, Aktivitäts- und Kategorienfilter wie die Übersicht. Sie zeigt die aktuelle, nicht historische Garmin-Referenz; CYCLING bzw. DEFAULT wird nur als Standard gewählt, ohne historische Zonenzuordnung. Pausen und Datenlücken über 60 Sekunden werden nicht interpoliert; Anteile beziehen sich auf gemessene Zeit je Periode, und Periodenschlüssel kommen unverändert vom Server. Garmin wird nur durch ausdrücklichen Abruf angefragt. Tests wurden vorbereitet und kompiliert, nicht ausgeführt. Windows-Laufzeitprüfung und API-Overlayverwaltung bleiben offen; Legacy-Funktionen bleiben erhalten.
