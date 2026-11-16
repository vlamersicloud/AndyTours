# Tourdetail und Puls – API-Migrationsstatus

Tourdetails sind über typisierte API-Verträge verfügbar. Der Server wählt gespeicherte FIT-Anhänge aus sicheren Datenbankmetadaten, erstellt begrenzte, eigene Snapshots und liest tatsächliche UTC-Zeitstempel, aktive Timerzeit (Pausen ausgeschlossen) sowie vorhandene Messwerte. Fehlende Werte bleiben null; mehrstufige Antworten bewahren Reihenfolge und Lücken.

Die API-Übersicht unterstützt Mehrfachauswahl und öffnet modellose WPF-Tourdetails mit bestehenden Diagrammen, Karte und Trackbearbeitung. Der zuletzt gewählte GPX-Exportordner bleibt lokal gespeichert. Course-Export nutzt Backend-Routing und FIT-Erzeugung; fehlende Abbiegungen liefern eine Warnung, niemals partielle Abbiegungen. Die lokale Geräteübertragung erfolgt erst nach Nutzerauswahl und Bestätigung.

Im API-UI-Pfad liegen weder FIT-Parser, Zugangsdaten noch Datenbankzugriff; vorhandene Legacy-Verweise bleiben bestehen. Course-Anfragen sind auf 128 MiB, Routingantworten auf 8 MiB begrenzt; HTTP-Weiterleitungen sind deaktiviert.

Vertragstests wurden vorbereitet und kompiliert, aber nicht ausgeführt. Windows-UI, Karten, Geräte und Netzwerkverhalten sind nicht zur Laufzeit verifiziert. Pulsanalyse und API-Overlayverwaltung bleiben offen; Legacy-Funktionen bleiben erhalten.
