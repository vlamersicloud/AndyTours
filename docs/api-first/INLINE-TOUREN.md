# Inline-Bearbeitung der API-Tourentabelle

## Quellumfang, keine Laufzeitabnahme

Dieses Paket ergänzt den API-WPF-Pfad und wurde gemeinsam quellengeprüft und korrigiert. Remote auf 192.168.178.32 mit SDK 10.0.402 wurden API, Windows-Ziel und Testprojekt vollständig gebaut (`-c Release --no-restore --no-incremental -p:EnableWindowsTargeting=true`), jeweils 0 Warnungen / 0 Fehler. Logs: `/tmp/andytours-inline-parity-{api,app,tests}-final-20261118.log`. Tests und Anwendungen wurden nicht ausgeführt. Windows-/Mehrclient-Prüfung bleibt ein separates Gate; die Gesamtmigration bleibt offen.

## Bedienung und Parität

Genau eine global manuell ausgewählte, auf der sichtbaren Seite vorhandene Tour ist erforderlich. F2 bzw. der native Zell-Editstart öffnet einen Draft. Enter/Tab bestätigt einen Wert; während des asynchronen Saves bleibt die Navigation gesperrt. Danach wird die Liste aktualisiert, nicht blind die nächste editierbare Zelle angesprungen. Escape vor Send verwirft ohne API-Aufruf. Während Send wird Escape nicht als Rücknahme einer möglicherweise bereits gespeicherten Änderung dargestellt.

15 Spalten sind editierbar: Datum, Startzeit, Name, Aktivität, Equipment, Distanz, Höhenmeter, Fahrzeit, Maximalgeschwindigkeit, Durchschnitts-/Maximalpuls, Trittfrequenz, Temperatur, maximale Höhe, Trainingsbereich. Kategorien, berechneter Durchschnitt und Kommentar bleiben explizit readonly. Alle 18 Anzeige.*-SortMemberPaths und bestehende Breiten, Bestwertfarben, Virtualisierung und Layoutpfade bleiben erhalten.

Ein per TextComposition gestarteter Texteditor behält den getippten Ersttext; Rohtext/SelectAll wird nur bei anderem Editstart eingesetzt. Bei vorher notwendigem Lookupabruf wird der auslösende Text nach erfolgreichem Wiedereinstieg eingesetzt. Ein Row-Doppelklick derselben Tour beendet einen noch nicht sendenden Draft ohne Save und öffnet anschließend einmal den bestehenden Editdialog; ComboBoxen, Header und fremde Tour-IDs sind ausgeschlossen. Preview- und Hauptfensterhandler teilen eine Timestamp-Sperre gegen doppelte Dialogauslösung.

Die zwölf Texteditoren verwenden die vorhandene WPF-Zelltextspalte: OneWay-Anzeigebindings bleiben bestehen, ohne die automatische OneWay-Schreibsperre der normalen DataGridTextColumn. Drei echte EditingTemplates enthalten ungebundene ComboBoxen. Aktivitäten und Equipment werden über Snapshot-ID vorgewählt; Trainingsbereiche über ihren exakten Originaltext, nicht die gemeinsame Dummy-ID. Equipment enthält (kein) mit null-Zuordnung und den belegten Zusatz (ausgemustert). Vor Namensauflösung wird die ausgewählte ID im aktuellen Snapshot geprüft; mehrdeutige Namen werden abgelehnt.

## Datensicherheit

Der Draft friert Tour-ID und opaque Version beim Editstart ein. Private Transportkopien und defensive Kategoriearray-Kopien an jeder öffentlichen Ausgabe verhindern Fremdmutation. Genau ein Feld wird ersetzt; alle übrigen Transportwerte einschließlich Fahrer, nicht angezeigter Messwerte, Kommentar und Kategorie-Reihenfolge/Duplikate bleiben erhalten. No-op vergleicht skalare Werte plus Kategorie-SequenceEquality und sendet nichts.

Der WPF-Zellcommit wird synchron abgebrochen. Nach Rückkehr aus dem nativen Event wird höchstens eine CAS-Anfrage gestartet. Der reine Inline-Speicher lädt nichts nach und hat keine Retries. Vor Refresh werden Cell und Row unter Apply-Guard per CancelEdit beendet; nur nach erfolgreichem Ende darf die VM Zeilen austauschen. Wenn das Ende fehlschlägt, bleibt die Bearbeitung gesperrt und es erfolgt kein Refresh.

Query-/Page-/Sort-/Filter-/Auswahl- und CRUD-Einstiege prüfen den gemeinsamen Edit-/Sendzustand. Auch die ursprünglichen Mehrfach-/Dialogeinstiege in ApiHauptfenster.Mehrfach.cs prüfen Edit/Send vor Snapshot und Callback; der dortige Delete-Handler kehrt bei aktivem Editor ohne Touraktion zurück. Die eingefrorenen Mehrfachsnapshots und ausschließlich bestätigte Auswahlentfernung bleiben unverändert. Preview-Eingaben verhindern Auswahl-/Aktionswechsel während Edit und Doppelaktionen bei Enter/Delete/Doppelklick. Delete wirkt im Texteditor, nicht als Tourlöschung. Konflikt, verschwundene Tour oder unklarer Transportstatus führen zu Reload-Hinweis und einer Inline-Wiedereinstiegssperre bis zu einem erfolgreichen Listencommit; kein Nachladen einer neuen Version für einen automatischen zweiten Send.

Programmatische Filteränderungen während Edit/Send werden nicht ignoriert: Eine deferred Invalidierung hält Tabellen-/Gesamtfilter getrennt fest und invalidiert Query-/Inlinegenerationen sofort. Vor noch nicht begonnenem Send wird der Commit abgebrochen. Erst nach beendetem nativen Edit und freigegebenem Sendguard werden die betroffenen Zeilen/Ansichten invalidiert und ein expliziter Reload verlangt. Ein bereits gesendeter CAS wird niemals wegen neuer Filter wiederholt. Erfolgreiche Listencommits aller drei Ladewege heben die Reloadsperre zentral auf. Edit-/Busy-Zustandswechsel benachrichtigen auch HatTourenauswahl. Lookupantworten mit fehlenden Listen, ungültigen/duplizierten IDs oder fehlenden Aktivitäts-/Equipmentnamen werden nicht veröffentlicht.

Der reine Writer unterscheidet UngueltigeTourException als eindeutig abgelehnte Eingabe von unklarem Transportstatus. Eine Saveantwort mit falscher ID, leerer/fehlender Versionskennung oder fehlenden Werten ist unklar, nicht gespeichert bestätigt: Reload erforderlich, kein Port-Retry. Die Versionskennung bleibt opaque, ohne Hash-/Längenformatregel.

Closing nach nicht abgebrochenem Schließevent, Closed und Dispose verwenden den idempotenten Inline-Cleanup. Cleanup cancelt eine laufende Save-CTS, entsorgt sie aber nicht. Ihre lokale Operation besitzt die CTS bis finally und räumt die Feldreferenz nur bei ReferenceEquals ab. Lebenszeit-/Query-/Inlinegenerationen verwerfen späte Ergebnisse; Lookup- und Savefehler ändern nach Cleanup keinen Inline-Status mehr.

## Parserreferenz

Abgeglichen wurden Core/Berechnungen/EinheitEingaben.cs, Eingabetext.cs, UebernimmZellwert.cs und EquipmentAnzeigename.cs sowie die vorliegenden Eingabetext-/Zellwerttests. Der neue Parser importiert diese Core-Typen nicht.

- Leerzahlen/Whitespace ergeben 0; leere Startzeit ergibt null.
- Dezimalkomma oder Punkt, keine Tausendertrennzeichen; ausschließlich endliche Zahlen.
- Integerwerte müssen in Int32 passen und nichtnegativ sein, außer Temperatur.
- Name und Trainingsbereich werden wie im Legacy-Formular getrimmt. Für das geänderte Feld gelten zusätzlich die belegten Backendgrenzen Name maximal 500 und Trainingsbereich maximal 200 Zeichen; Überschreitung bleibt lokal ungültig.
- Datum d.M.yyyy oder d.M.yy mit de-DE; Uhrzeit H:mm oder HH:mm.
- Dauer h:mm oder h:mm:ss mit zweistelligen Minuten/Sekunden 00–59, 1–6 Stundenziffern; Rohtext über 24 Stunden ohne Stunden-Wrap, 0 leer.
- Lookupidentität ist die ID des gewählten Snapshotobjekts; Mehrdeutigkeit ist ein Fehler, keine FirstOrDefault-Zufallsauswahl.

## Vorbereitete Nachweise

ApiTourInlineTests enthält reine Draft-/Fake-Speicherfälle für 15 Felder, Schutz unberührter Werte, Snapshotmutation, No-op, Formatfehler, Zweiziffernjahr, negative Temperatur, Langdauer, Lookupmehrdeutigkeit, nullable Equipment, Editstart-CAS, Konflikt/404/Transportfehler ohne Retry und parallelen Doppelcommit. Zusätzliche reine Writer-Fixtures behandeln eindeutige Backendablehnung, sechs malformed Saveantworten ohne Retry, opaque Versionen und Sendguard-Freigabe nach Transportabbruch; die bestehenden PreCancel-/DoppelSend-/Snapshotfälle bleiben erhalten. Grenztests prüfen Name 500/501 und Trainingsbereich 200/201. ApiTourInlineStrukturTests prüft eingebettete XAML-/Quellressourcen einschließlich Erstzeichen-Gate, Doppelklickidentität, Timestamp-Sperre, Auswahlnotification und deferred Filterguards; das sind strukturelle Quellenprüfungen, keine ausgeführten VM-/WPF-Verhaltenstests. Die bestehenden drei Hauptfenster-/Sortier-/Oberflächen-Vertragstests sind auf 12 Zelltextspalten, drei Combo-Templates und drei readonly-Spalten sowie den neuen Keyflow umgestellt; Sortpfade, OneWay-Anzeigen, Bestwertstyles, Virtualisierung und Headerreihenfolge bleiben assertiert. Testprojektlinks kompilieren nur Draft und den reinen Speicher, nicht WPF.

Diese Fixtures sind vorbereitet, nicht ausgeführt. Sie ersetzen keine Windows-Dispatcher-/Fokus-/Dropdown-/Virtualisierungs-/Closeprüfung. Insbesondere Enter/Tab, invalid-rot/Editbehalt, Doppelklick/Entf, seitenübergreifende Auswahl, Filter/Sortversuche beim Send, Close während Lookup/Send, 409 zwischen zwei Clients und Reloadfehler sind vom Auftraggeber zur Laufzeit zu prüfen.
