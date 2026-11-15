# 0010 · Anpassung des Datenbankschemas

## Entscheidung
Ältere Datenbestände werden beim Öffnen stufenweise an das aktuelle Schema angepasst. AndyTours liest die Schemaversion (`PRAGMA user_version`) und führt die nötigen Umbaustufen nacheinander aus (z. B. „4 → 5“), alle in einer Transaktion; die neue Versionsnummer wird erst am Ende gesetzt. Vor einem Umbau legt AndyTours eine Sicherung des bisherigen Stands an; scheitert sie, wird nichts umgebaut und der Start bricht mit einer Meldung ab. Wiederhergestellte Sicherungen älterer Versionen durchlaufen denselben Umbau. Neue Tabellen kommen weiterhin einfach über `CREATE TABLE IF NOT EXISTS` hinzu.

## Warum
Die einfachste Lösung, die auch Spalten und Tabellen entfernen kann: wenig Code, jede Stufe klein und einzeln testbar, und sie setzt den bisherigen Weg über die Schemaversion fort. Die Transaktion verhindert halb umgebaute Bestände, die Sicherung schützt vor Fehlern im Umbau selbst.

## Alternativen
- Neue Datenbank aufbauen und alle Daten umkopieren: jede Art von Änderung möglich, aber deutlich mehr Code, der für jede Tabelle gepflegt werden muss.
- Nur hinzufügen, nie entfernen: kein Risiko, aber nicht mehr verwendete Werte bleiben dauerhaft im Datenbestand.
