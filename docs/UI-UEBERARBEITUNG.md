# UI-Überarbeitung: Umfang und Abnahme

## Umgesetzt in diesem Paket
- Benannte, API-spezifische gemeinsame WPF-Ressourcen; App-Merge im bestehenden Fluent-Light-Thema. Graue Flächen, weiße Karten/Felder/Tabellen, ruhige Kanten und Akzentaktionen ohne globale neue Steuerelementvorlagen.
- Hauptfenster mit getrennter Titel-/Backendzeile und schlichter Menüleiste, kompaktem Filterbereich, vier Kennzahlenkarten, Übersicht/Diagramm und Tourentabelle. Bindings, Menüereignisse, Sortierung, Auswahl, Spalten, Virtualisierung und Bestwertfarben bleiben erhalten; Löschen ist optisch unterscheidbar.
- Sechs API-Dialoge mit rollbarem Inhalt und festem, rechtsbündigem Umbruch-Footer; Validierungs- und Sicherheitshinweise sowie bestehende Aktionen bleiben bestehen. Der FIT-Dialog bietet zusätzlich den vorhandenen Geräte-Ladebefehl. Die Wiederherstellungsvorschau bleibt eine reine Metadatenvorschau.
- Zwei vorhandene Diagramme mit besser lesbaren Achsen, zurückhaltendem Raster und klaren Datenfarben; fachliche Zuordnungen und Berechnungen wurden nicht geändert.
- Vorbereitete Quell-/XAML-Vertragsfixtures einschließlich App-merged-Resource-Prüfung, FIT-Geräteaktion, Garmin-Footerstruktur, Archivdialog-Layoutinvariante und Skalierungsanmeldung; Sicherheitsassertionen bleiben bestehen. Resource-Links sind im Testprojekt ergänzt.

## Abnahmekriterien (noch auszuführen)
- Auf kleiner Windows-Arbeitsfläche bei 100 %, 150 % und 200 %: keine abgeschnittenen Beschriftungen oder verdeckten Footer; Inhalt bleibt erreichbar.
- Tastatur: Alt-Zugriffstasten, Tab-Reihenfolge, Enter/Esc, Fokusrahmen und Tabellen-Auswahl.
- Kontrast: Menü, Auswahl/Fokus, Fehler- und Alarmfarben sowie rote Bestwerte auch bei markierten Zeilen.
- Lade-, Fehler- und Leerzustände: verständliche Statusmeldungen und erreichbare Aktionen.
- Diagramme: Achsen, Einheiten, Raster und Datenfarben bei typischen und leeren Datensätzen lesbar.
- API-, App-/Windows- und Testprojekt wurden auf dem Testserver vollständig mit .NET SDK 10.0.402, `--no-incremental --no-restore`, gebaut: jeweils **0 Warnungen und 0 Fehler**. Die im ersten Testprojekt-Build gemeldete xUnit2013-Warnung wurde korrigiert; auch die vollständige Neukompilierung ist warnungsfrei. Es wurden keine Tests oder Anwendungen ausgeführt; die Testausführung liegt bei Volker. Windows-Sichtabnahme steht ebenfalls aus.

## Nicht Teil dieses Pakets
Andere Ansichten und Dialoge außerhalb der oben genannten API-Hauptansicht und sechs Dialoge – darunter Tourenbearbeitung, Planung, Stammdaten, Routing, Karten und weitere Einstellungen – bleiben ausdrücklich Restumfang. Verhalten, Backend, Restore und Datenmigration sind nicht Teil dieser Oberflächenänderung.
