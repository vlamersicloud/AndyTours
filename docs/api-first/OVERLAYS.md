# API-Overlays

Die API-Overlayabläufe sind im Client angeschlossen; vollständige Migration und Laufzeitparität sind noch offen. Der typisierte Client liest Metadaten und versionsgebundene Geometrie und bietet gezielte Ordner-/Overlayänderungen mit CAS-Versionen. Serverseitig gelten Zyklus- und Kaskadenregeln.

Der GPX-Endpunkt nimmt begrenzte Rohdaten an (128 MiB je Datei); Parsing/Validierung erfolgen serverseitig. Stabile Anfrage-IDs, Quittungen und Body-Hashes unterstützen idempotente Wiederholung. Clientpfade bleiben lokal und erscheinen weder im Vertrag noch in der Oberfläche.

Pro API-Root gibt es einen Overlaykontext mit UI-Dispatcher-Anwendung, versionsgebundenem Geometriecache und eigenem modeless Manager. Die Tourdetailkarte erhält denselben Kontext. Baumzustände, Auswahl, dreistufige Sichtbarkeit, Farben, Verschieben und Zoom sind angebunden. Mehrere Dateien lassen sich einreihen; unsichere Anfragen behalten ID und ursprüngliche Version für manuelle Wiederholung. Bestätigte Importe bleiben bis zum erfolgreichen Metadaten-Reload gesperrt.

Builds wurden geprüft; vorbereitete Tests wurden kompiliert, aber nicht ausgeführt. Windows-/Kartenlaufzeit, Geräte und vollständige Migration sind nicht verifiziert.
