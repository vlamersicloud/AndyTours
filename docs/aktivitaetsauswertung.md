# Aktivitäts- und Pulsbereichsauswertung

## Bedienung

Die Karteikarte **Aktuell** hat eigene Von-/Bis-Felder. Standard ist **1. Januar des
aktuellen Jahres bis heute**. **Aktuelles Jahr bis heute** stellt diesen Bereich
erneut her. Eigene Datumsgrenzen bleiben für die laufende Sitzung erhalten.
Leere oder umgekehrte Grenzen werden mit Hinweis abgewiesen, nicht still geändert.

**Gruppierung:** Jahr, Monat, ISO-Kalenderwoche (Montag–Sonntag, ISO-Wochenjahr) oder Tag.
**Kennzahl:** Gesamtkilometer, Höhenmeter, Fahrzeit in Stunden oder Anzahl Aktivitäten.
Balken zeigen die jeweilige Periodensumme, keine kumulative Kurve. Leere Perioden
werden mit 0 berücksichtigt, Anfang und Ende sind einschließlich. Über 10.000
Perioden werden abgewiesen; dann Zeitraum verkürzen oder gröberes Raster wählen.

Diagramm, Bestleistungen und Pulsanalyse verwenden dieselben Datumsgrenzen.
Aktivität aus der Werkzeugleiste und Kategorie aus dem Tabellenfilter wirken mit.
Die oben als **Tabellenjahr** bezeichnete Auswahl bleibt für die Trainingstabelle/
zugehörige bestehende Werkzeugleisten-Auswahl zuständig; sie überschreibt die
eigenen Von-/Bis-Felder nicht. Andere Tabellenfilter wirken hier wie bisher nicht.
Jahresrekorde beziehen sich auf die innerhalb des Zeitraums vorhandenen Teiljahre,
nicht auf ungefilterte volle Jahre. Saisonkennzahlen bleiben in der Standardauswahl
1. Januar bis heute als Text erhalten; die früheren Jahres-Zielmarkierungen werden
im Kalenderbalkendiagramm nicht als scheinbar passende Monats-/Wochenziele dargestellt.

## Warum stand die Achse auf 2046?

Die alte Ansicht wertete alle gespeicherten Jahre aus und skalierte die Achse nach
diesen Daten, unabhängig vom Tabellen-Jahresfilter. Der bereitgestellte Screenshot
zeigte außerdem einen Einzelrekord mit Datum **31.03.2046**. Solche gespeicherten
Datumswerte reichen aus, die alte Jahresachse bis 2046 aufzuspannen.

Die Herkunft dieses Datums (Originaldatei, Altbestand, Bearbeitung oder Import) ist
ohne den konkreten privaten Datenbestand nicht nachgewiesen. Es wurden **keine
Tourdaten umdatiert oder gelöscht**. Die neue Übersicht zeigt nur den eingestellten
Zeitraum und meldet die Zahl der Aktivitäten mit Datum nach heute. Wer bewusst
ein zukünftiges Enddatum einstellt, kann entsprechend datierte Touren sehen und
über die Tabelle prüfen bzw. ausdrücklich korrigieren.

## Pulsbereiche

1. Optional **Garmin-Zonen aktualisieren** wählen. Der bereits in den persönlichen
   Einstellungen hinterlegte Personal API-Key wird verwendet.
2. Sportprofil ausdrücklich prüfen: z. B. Radfahren (CYCLING), Laufen (RUNNING) oder
   Allgemein (DEFAULT). CYCLING bzw. DEFAULT wird, falls vorhanden, vorausgewählt;
   die Anwendung behauptet nicht, das Profil automatisch aus jeder Tour ableiten zu können.
3. **Pulsanalyse laden** liest die FIT-Anhänge der gewählten Aktivitäten im Hintergrund.
4. Das gestapelte Diagramm zeigt Minuten in jedem Pulsbereich pro gewählter Kalenderperiode.
   Die Tabelle nennt Zeitraum, Garmin-Zonennummer, konkreten bpm-Bereich, Messzeit und Anteil.

Die gewählte Referenz stammt aus dem **aktuellen Garmin-Profil zum angezeigten
Abrufdatum**, wie vom Anwender freigegeben. Sie ist **kein Beleg für damalige Garmin-
Zonengrenzen**. Die Anwendung liest für diese Referenzauswertung keine historischen
Zonendefinitionen aus früheren FIT-Dateien. Nach Änderungen in Garmin erneut abrufen.
Die Profile werden mit Abrufdatum in der Einstellungstabelle des Datenordners
gespeichert und sind damit auch in Datensicherungen enthalten. Es werden keine
Passwörter oder Bearer-Token gespeichert. Ein fehlgeschlagener Abruf überschreibt
eine vorhandene gültige Referenz nicht.

Offizieller Endpunkt:
`GET /biometric-service/heartRateZones/v2?generateDefaults=false`.
Format laut offiziellem OpenAPI: Array von `HeartRateZonesDTO`, `sport` und
`zone1Floor` bis `zone5Floor` als streng aufsteigende bpm-Untergrenzen.
Keine Grenzen aus Alter, Maximalpuls oder Standardprozentsätzen erfinden.
Unterhalb der ersten Grenze wird **Unter Zone 1** ausgewiesen; Zone 5 ist
**ab … bpm** nach oben offen. Der konfigurierte Maximalpuls ist keine harte
Obergrenze für tatsächlich gemessene Werte.

Ohne gewähltes gültiges Profil bzw. mit **10-bpm-Bereiche statt Garmin-Zonen**
werden gemessene Zehnerbereiche angezeigt, ausdrücklich **ohne Garmin-Zuordnung**.

### Messmethode und Grenzen

- Pulszeiten stammen aus den tatsächlichen FIT-Messpunkten; der durchschnittliche
  Tourpuls verteilt keine ganze Tour auf eine einzelne Zone.
- Für zwei aufeinanderfolgende gültige Messpunkte wird die positive Differenz der
  aktiven Timerzeit dem **vorherigen Pulswert** zugeordnet. Das ist eine
  zeitgewichtete Referenzauswertung, keine Behauptung der Garmin-eigenen Auswertelogik.
- Maximal 60 Sekunden tatsächlicher Messabstand; größere Lücken werden nicht
  aufgefüllt. Pausen zählen durch die aktive FIT-Timerzeit nicht mit.
- Fehlender/ungültiger Puls zählt nicht als 0-bpm-Zone und wird nicht interpoliert.
  Ohne zwei gültige Nachbarpunkte entsteht keine auswertbare Dauer.
- Hinter dem letzten Messpunkt wird keine Zeit extrapoliert.
- Fehlende bzw. nicht lesbare FIT-Anhänge werden gezählt, nicht mit Tourmittelwerten
  ersetzt. Touren können für km/hm/Fahrzeit auswertbar sein, ohne FIT-Pulszeiten zu haben.
- Prozente beziehen sich nur auf die **auswertbare Messzeit derselben Periode**,
  nicht auf die gesamte Fahrzeit; Rundungsdifferenzen sind möglich. Die Summe der
  ausgewerteten Zeiten und die Fahrzeit laut Tourdaten werden getrennt angegeben.
- Über Mitternacht verlaufende Aktivitäten werden als Einheit ihrem Tourstartdatum
  zugeordnet; die Messintervalle werden nicht auf Kalendertage aufgeteilt.
- Filter-, Datenordner- oder Profilwechsel leert alte Ergebnisse. Läuft noch ein
  Lesevorgang, werden dessen inzwischen veraltete Ergebnisse nicht übernommen.
  Danach explizit erneut laden. FIT-Dateien werden sequenziell verarbeitet.

## Abnahme und Build

Neue Tests sind bereitgestellt und **nicht ausgeführt**:
- `AktivitaetsperiodenTests`: Datumseinschluss, leere Monate, Zukunftsjahr 2046,
  ISO-Woche über Jahreswechsel, Schalttag, Fahrzeit und Grenzvalidierung.
- `PulszeitenTests`: exakte Zonengrenzen, echte Zeitgewichtung, Pausen, Fehlwerte,
  Messlücken und bpm-Fallback.
- `GarminPulsprofileTests`: offizieller Pfad und Header per Mock, unvollständige
  Grenzen, Profil-/Abrufdatum-Cache. Keine echten Zugangsdaten erforderlich.

Windows-Cross-Build der gesamten Solution auf Testserver 192.168.178.32.
Installiertes SDK 10.0.401 wird nur für die Buildprüfung vorübergehend ausgewählt;
global.json bleibt mit 10.0.402 unverändert. Keine Builds auf dem OpenClaw-Host.
WPF-Bedienung und echter Garmin-Zonenabruf sind noch durch den Anwender abzunehmen.

Manuelle Prüfliste:
1. Start mit Standardbereich: keine automatische Achsenverlängerung bis 2046.
2. Wechsel aller vier Kennzahlen und aller vier Raster, einschließlich KW 53 über Silvester.
3. Benutzerdefinierter Mehrjahresbereich, leere Perioden, Von > Bis, anschließend Standard zurücksetzen.
4. Bestleistungen und Summen entsprechen dem eingestellten Zeitraum.
5. Garmin-Zonen laden, Sportprofil wechseln, bpm-Grenzen und Abrufdatum prüfen.
6. FIT-Daten mit Pausen, ohne Pulsgurt, mit fehlendem Anhang und mit Messlücken auswerten.
7. Während des FIT-Lesens Filter oder Datenordner wechseln: keine alten Ergebnisse unter neuer Auswahl.
8. Temporärer Garmin-Fehler: bestehende Referenz bleibt, keine Zugangsdaten in Meldungen.

## Verteilung

Es wird **keine EXE im Repository bereitgestellt**. Die vorher hinzugefügte Binärdatei
und ihre Downloadhinweise werden aus dem aktuellen Stand entfernt, ohne Git-Historie
umzuschreiben. Lokal bauen/starten wie in README beschrieben.

## Quellen

- Garmin Personal API:
  https://developer.garmin.com/gc-developer-program/personal/swagger-docs/
- Das auf dieser offiziellen Seite eingebettete OpenAPI-Schema
  `HeartRateZonesDTO` dokumentiert Profilart und fünf bpm-Untergrenzen.
- Bestehende FIT-Auswertung über `Garmin.FIT.Sdk`; keine fremde SSO-Anmeldung.
