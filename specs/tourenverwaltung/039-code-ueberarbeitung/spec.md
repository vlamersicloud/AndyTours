# 039 – Code-Überarbeitung

## Was und warum
Der gesamte Code samt Oberflächendateien, Tests, Umwandlungswerkzeug sowie Installations- und
Bauskripten wird geprüft und verschlankt, damit das Projekt schlank und gut verständlich ist:
ungenutzter oder doppelter Code entfällt, umständliche Stellen werden einfacher, veraltete
Kommentare werden berichtigt. Dazu gehören auch Werte, die im Datenbestand aufbewahrt, aber
nirgends verwendet werden; sie entfallen samt ihrem Platz im Datenbestand. Ansonsten ändert
sich für den Anwender nichts. Der Benutzer entscheidet je Bereich anhand einer Liste, welche
Änderungen umgesetzt werden.

## Kriterien
- Wenn ein Bereich geprüft ist, dann bekommt der Benutzer eine Liste der vorgeschlagenen Änderungen mit kurzer Begründung, bevor etwas geändert wird, und umgesetzt wird nur, was er freigibt.
- Wenn eine vorgeschlagene Änderung die Projektstruktur oder eine festgehaltene Architekturentscheidung betrifft, dann wird sie vor der Umsetzung als eigene Architekturentscheidung festgehalten.
- Wenn jemand den Code liest, dann findet er keine Klassen, Methoden, Eigenschaften oder Einstellungen mehr, die nirgends verwendet werden.
- Wenn zwei Stellen dasselbe leisten, dann gibt es diese Logik nur noch einmal.
- Wenn jemand die Kommentare liest, dann beschreiben sie den heutigen Stand, ohne Hinweise auf „später“, „Phase …“ oder entfallene Funktionen.
- Wenn jemand den Code liest, dann folgt er durchgehend den Projektregeln: eine Datei je Aufgabe, Namensregeln, ausführliche deutsche Kommentare.
- Wenn die Überarbeitung fertig ist, dann ist der Code insgesamt nicht umfangreicher als vorher.
- Wenn jemand die Tests liest, dann gibt es keine doppelten oder überflüssigen Tests mehr, und jede Fachfunktion hat weiterhin Tests.
- Wenn das Programm nach der Überarbeitung benutzt wird, dann verhalten sich alle Funktionen wie vorher, alle Tests bestehen und der Build zeigt keine Warnungen.
- Wenn der Benutzer einen Datenbestand einer früheren Version zum ersten Mal öffnet, dann entfernt AndyTours die nicht mehr verwendeten Werte, und alle übrigen Daten bleiben unverändert und nutzbar.
- Wenn AndyTours einen Datenbestand einer früheren Version so umbaut, dann legt es vorher eine Sicherung des bisherigen Stands an.
- Wenn der Benutzer eine Sicherung einer früheren Version wiederherstellt, dann wird sie ebenso umgebaut und ist danach vollständig nutzbar.
- Wenn das Installationsprogramm gebaut und das Umwandlungswerkzeug aufgerufen werden, dann funktionieren beide wie vorher; das Umwandlungswerkzeug übernimmt die nicht mehr verwendeten Werte nicht mehr.
