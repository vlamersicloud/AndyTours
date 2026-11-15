# 0009 · Hauptmenüleiste

## Entscheidung
Die Hauptmenüleiste des Hauptfensters ist eine schlichte, durchgehende Leiste in der Farbe des Fensterhintergrunds. Die Hauptmenüpunkte („Datei“, „Einheit“ …) stehen als einfacher Text darauf, ohne eigene Flächen oder Rahmen, wie in der Fluent-Vorlage. Beim Darüberfahren und bei offenem Menü sind sie leicht grau hinterlegt (`#CDD4DC`, dieselbe Farbe wie hervorgehobene Einträge in aufklappenden Menüs). Eine feine Linie (`#C2C9D1`) trennt die Leiste von der Werkzeugleiste darunter. Aufklappende Menüs und Kontextmenüs bleiben wie in [0008](0008-kontrastfarben-der-oberflaeche.md), bekommen aber einen dunkelgrauen Rahmen (`#8F98A3`, wie der Rahmen der Karteikarten), damit sie sich von weißen Tabellen abheben. Die Entscheidung ersetzt 0008 für die Hauptmenüpunkte.

## Warum
Die Hauptmenüpunkte als graue Flächen mit Rahmen (0008) wirken wie große Schaltflächen und unelegant. Eine schlichte Textleiste ist der heutige Windows-Standard (z. B. der Editor in Windows 11) und passt zum Fluent-Design. Die etwas dunklere Hervorhebung beim Darüberfahren bleibt, weil das Fluent-Grau kaum sichtbar ist; die Trennlinie grenzt die Leiste deutlich ab.

## Alternativen
- Graue Flächen mit Rahmen beibehalten (0008): gut erkennbar, aber unelegant.
- Weiße Leiste, die sich vom grauen Fensterinhalt abhebt: deutlich abgegrenzt, aber ungewohnt für ein Windows-Menü.
- Fluent ganz unverändert, ohne eigene Hervorhebung: kein eigener Wert, aber die Hervorhebung ist kaum sichtbar.
