# Plan: 038 – Windows-Menüleiste

## Vorgehen
- **Hauptmenüpunkte ohne eigene Fläche:** In `App.xaml` entfällt der Stil, der den Hauptmenüpunkten (Rolle `TopLevelHeader`/`TopLevelItem`) die Farben der nicht gewählten Reiter und einen Rahmen gibt. Damit gilt wieder die Fluent-Vorlage: Die Punkte stehen als Text auf der Leiste, die Leiste hat die Farbe des Fensterhintergrunds.
- **Hervorhebung:** Die Farbressource `MenuBarItemBackgroundSelected` (`#CDD4DC`) bleibt. Die Fluent-Vorlagen nutzen sie für den hervorgehobenen Hauptmenüpunkt und für hervorgehobene Einträge in aufklappenden Menüs und Kontextmenüs. Der Hauptmenüpunkt ist beim Darüberfahren und bei offenem Menü also leicht grau hinterlegt, und die aufklappenden Menüs sehen aus wie bisher.
- **Trennlinie:** Im Hauptfenster steht direkt unter dem `Menu` ein 1 px hoher `Border` im Grau des bisherigen Menürahmens (`#C2C9D1`), oben angedockt. Ein `Border` braucht keinen Fluent-Stil (`docs/tech.md`), und die Linie hängt so nicht davon ab, ob die Fluent-Vorlage des Menüs einen Rahmen zeichnet.
- **Unverändert:** Menüinhalt, Zugriffstasten (Alt+Buchstabe), Strg+N, Entf und das Ausgrauen nicht möglicher Einträge (Befehlsbindung) bleiben, wie sie sind. Die Skalierung wirkt über die vorhandene Vergrößerung des Fensterinhalts (ADR 0007) auch auf die Menüleiste.
- **Dokumentation:** Handbuch 3.3, `docs/tech.md`, `CLAUDE.md` und die Projektbeschreibung beschreiben das Hauptmenü nicht mehr als graue Flächen. Die Entscheidung selbst kommt als ADR 0009 (siehe Offene Entscheidungen); ADR 0008 bekommt den Vermerk „teilweise ersetzt durch 0009“.

## Dateien
- `src/AndyTours.App/App.xaml` (ändern) — Stil für die Hauptmenüpunkte entfernen; Kommentar zu `MenuBarItemBackgroundSelected` anpassen
- `src/AndyTours.App/Views/Hauptfenster.xaml` (ändern) — Trennlinie unter der Menüleiste; Kopfkommentar
- `HANDBUCH.md` (ändern) — Abschnitt 3.3: Aussehen der Menüleiste
- `docs/tech.md` (ändern) — Kontrastfarben ohne Hauptmenü, Hinweis auf ADR 0009
- `CLAUDE.md` (ändern) — dieselbe Zeile zu den Kontrastfarben
- `Beschreibung der fundamentals.md` (ändern) — Abschnitte zu Technik, Regeln und Erscheinungsbild (die wörtliche Kopie von ADR 0008 bleibt als Verlauf)
- `docs/adr/0009-…md` (neu, hängt ab von: Wie sieht die Hauptmenüleiste aus?) — wird mit `/sdd-adr` angelegt
- `docs/adr/0008-kontrastfarben-der-oberflaeche.md` (ändern, hängt ab von: Wie sieht die Hauptmenüleiste aus?) — Vermerk „teilweise ersetzt durch 0009“

## Abdeckung der Kriterien
Alle Kriterien betreffen das Aussehen bzw. unveränderte Bedienung und werden manuell mit `dotnet run --project src/AndyTours.App` geprüft. Dass sich das Verhalten nicht ändert, sichern zusätzlich Build ohne Warnungen und alle Tests mit `dotnet test AndyTours.slnx`.

- Wenn der Benutzer das Hauptfenster öffnet, dann stehen „Datei“, „Einheit“, „Tourenplanung“, „Extras“ und „Hilfe“ als einfacher Text in einer durchgehenden Leiste in der Farbe des Fensterhintergrunds, ohne einzelne Flächen oder Rahmen. → `App.xaml` (Stil entfernt); manuelle Prüfung: Hauptfenster ansehen.
- Wenn der Benutzer die Menüleiste ansieht, dann trennt eine feine Linie sie von der Werkzeugleiste darunter. → `Hauptfenster.xaml` (Trennlinie); manuelle Prüfung.
- Wenn der Benutzer mit der Maus über einen Hauptmenüpunkt fährt oder dessen Menü geöffnet ist, dann ist der Punkt leicht grau hinterlegt, dezent, aber deutlich erkennbar. → `MenuBarItemBackgroundSelected` in `App.xaml`; manuelle Prüfung: über „Datei“ fahren, „Datei“ öffnen.
- Wenn der Benutzer einen Hauptmenüpunkt anklickt, dann klappt dasselbe Menü mit denselben Einträgen und Trennlinien auf wie bisher, und hervorgehobene Einträge darin sind wie bisher etwas dunkler grau. → Menüinhalt in `Hauptfenster.xaml` unverändert; manuelle Prüfung: alle fünf Menüs öffnen und mit Handbuch 4.2–4.10 vergleichen.
- Wenn der Benutzer Alt drückt oder Alt zusammen mit dem unterstrichenen Buchstaben eines Hauptmenüpunkts, dann öffnet bzw. erreicht er das Menü wie bisher; Strg+N und Entf wirken weiter. → unverändert; manuelle Prüfung: Alt, Alt+D, Alt+X, Strg+N, Entf in der Tabelle.
- Wenn ein Menüeintrag gerade nicht möglich ist, dann ist er wie bisher ausgegraut. → unverändert (Befehlsbindung); manuelle Prüfung: ohne markierte Einheit „Einheit“ öffnen, „Bearbeiten…“ ist ausgegraut.
- Wenn der Benutzer die Größe der Oberfläche in den Einstellungen ändert, dann wächst die Menüleiste wie der übrige Fensterinhalt mit. → ADR 0007, unverändert; manuelle Prüfung: Größe der Oberfläche auf 150 % stellen, Menüleiste samt Trennlinie wächst mit, danach zurückstellen.
- Wenn der Benutzer das Kontextmenü einer Tabelle oder Liste öffnet, dann sieht es aus wie die aufklappenden Menüs der Menüleiste. → unverändert; manuelle Prüfung: Rechtsklick in der Tabelle der Einheiten und der Wartung.
- Wenn ein Anwender das Handbuch liest, dann beschreibt es die Menüleiste so, wie sie jetzt aussieht. → `HANDBUCH.md` 3.3; manuelle Prüfung: Abschnitt lesen.

## Entscheidungen
- Die Trennlinie ist ein eigener `Border` unter dem Menü statt eines Rahmens am Menü selbst, damit sie nicht von der Fluent-Vorlage des Menüs abhängt.
- Die Farbe der Hervorhebung bleibt `#CDD4DC` für Hauptmenü und aufklappende Menüs; eine zweite, hellere Farbe nur für das Hauptmenü wäre ein weiterer Wert ohne erkennbaren Nutzen.

## Offene Entscheidungen
- Wie sieht die Hauptmenüleiste aus (schlichte Leiste ohne eigene Flächen; löst ADR 0008 für die Hauptmenüpunkte ab)? → /sdd-adr

## Risiken
- Wie die Fluent-Vorlage den Hauptmenüpunkt ohne eigene Farben genau zeichnet (Abstände, Rundung der Hervorhebung), zeigt sich erst im laufenden Programm. Weicht es von der Spec ab, werden nur Eigenschaften im Stil ergänzt, keine eigene Vorlage.
