# AndyTours – Benutzerhandbuch

Für AndyTours Version 1.0.1

## Inhaltsverzeichnis

1. [Was macht das Programm](#kapitel-1)
   - [Was das Programm nicht kann](#nicht-enthalten)
2. [Installation](#kapitel-2)
3. [Erste Schritte](#kapitel-3)
4. [Funktionen im Detail](#kapitel-4)
   - [4.1 Das Hauptfenster](#k4-1)
   - [4.2 Menü „Datei“: Touren importieren](#k4-2)
   - [4.3 Karteikarte „Aktuell“](#k4-3)
   - [4.4 Karteikarte „Trainingseinheiten“](#k4-4)
   - [4.5 Menü „Einheit“: Einheiten anlegen, ändern und zuordnen](#k4-5)
   - [4.6 Fenster „Tourdetail“](#k4-6)
   - [4.7 Karteikarte „Wartung“](#k4-7)
   - [4.8 Menü „Tourenplanung“](#k4-8)
   - [4.9 Menü „Extras“](#k4-9)
   - [4.10 Menü „Hilfe“](#k4-10)
5. [Häufige Fragen und Probleme](#kapitel-5)
6. [Glossar (Stichwortverzeichnis)](#kapitel-6)
- [Anhang: Offene Fragen](#anhang-offene-fragen)

---

<a id="kapitel-1"></a>
## 1. Was macht das Programm

AndyTours verwaltet Ihre Fahrradtouren auf einem Windows-PC. Es ist der Nachfolger des Programms BikeXperience. Ihre bisherigen BikeXperience-Daten können Sie bei der Installation übernehmen.

Mit AndyTours können Sie:

- **Touren importieren:** Neue Touren holen Sie direkt vom angeschlossenen Garmin-Radcomputer (Edge 1040, Edge 1000 oder Edge 800). Alternativ wählen Sie Aufzeichnungsdateien von der Festplatte.
- **Touren verwalten:** Alle Touren stehen in einer Tabelle. Sie können sie sortieren, filtern, durchsuchen, bearbeiten, kopieren und löschen.
- **Touren ansehen:** Zu jeder Tour sehen Sie das Höhenprofil und die Strecke auf einer Landkarte. Mehrere Touren lassen sich als Mehrtagestour gemeinsam ansehen.
- **Statistiken auswerten:** Die Übersicht „Aktuell“ zeigt Ihre Bestleistungen und die Jahressummen über alle Jahre. Sie sehen, wie weit Sie Ihre Saisonziele schon erreicht haben.
- **Räder warten:** Sie erfassen Wartungen wie einen Kettenwechsel. Wartungsalarme erinnern Sie, wenn eine Wartung fällig ist. AndyTours zeigt auch, wie lange ein Teil gehalten hat.
- **Touren planen:** Sie klicken eine neue Strecke auf der Karte zusammen. AndyTours führt sie über passende Wege. Die Planung können Sie auf Ihren Garmin-Radcomputer übertragen.
- **Daten sichern:** AndyTours sichert Ihren Datenbestand automatisch. Eine Sicherung können Sie jederzeit wiederherstellen.

AndyTours ist für eine einzelne Person gedacht. Alle Ihre Daten liegen in einem einzigen Ordner, dem **Datenordner**. Diesen Ordner können Sie frei wählen, zum Beispiel auch auf einem NAS (Netzwerkspeicher).

Nach dem Start sehen Sie das Hauptfenster. Es öffnet die Karteikarte, die beim letzten Beenden gewählt war. Beim allerersten Start ist das die Karteikarte **Aktuell** mit Ihren Bestleistungen und dem Diagramm der Jahressummen.

<a id="nicht-enthalten"></a>
### Was das Programm nicht kann

AndyTours übernimmt nur die Funktionen von BikeXperience, die tatsächlich genutzt werden. Folgendes ist bewusst nicht enthalten:

- mehrere Benutzer
- Altdaten-Import innerhalb des Programms (das erledigt das Installationsprogramm, siehe [Kapitel 2](#kapitel-2))
- Rück-Export zu BikeXperience und gleichzeitiger Betrieb mit BikeXperience
- Schwellwerte mit Farben
- im Tourdetail: Puls- und Zonenauswertung, Rundenzeiten, Vergleich von Höhenprofilen
- die Karteikarten „Diagramme“, „Statistik“, „Kalender“, „Trainingsplan“ und „Planung“ aus BikeXperience
- weitere Auskünfte wie Jahresprognose, Top 50, Gewichtsverlauf, Ruhe- und Erholungspuls, Leistungs- und Trainingskontrolle, Temperaturauswertung, virtuelle Weltumrundung, Vergleich von Aktivitäten und Kategorien
- bei der Wartung: Kuchendiagramm sowie Export und Import als CSV-Datei
- bei Equipments: Gewicht, Radumfang, Roll- und Luftwiderstand, Kettenblätter und Ritzel, Bild
- Berechnung von Leistung und Kalorien
- Werkzeuge wie Ritzelrechner, Pulsrechner und Marathonzeitenplaner
- Import von Touren aus GPX- und TCX-Dateien (GPX-Dateien lassen sich aber als Overlay einblenden, siehe [Abschnitt 4.8](#k4-8))
- Abgleich mit Garmin Connect
- Öffnen von Anhängen einer Einheit
- erneutes Übernehmen von Werten aus der Aufzeichnung einer Einheit
- Bildergalerie mit Fotos auf der Strecke
- Übertragen von Courses direkt aus der Overlay-Verwaltung und Speichern von Courses als Datei
- dunkle Darstellung: AndyTours bleibt immer hell, auch wenn Windows auf dunkel eingestellt ist

---

<a id="kapitel-2"></a>
## 2. Installation

### 2.1 Was Sie brauchen

- einen PC mit Windows 10 (ab Version 2004) oder Windows 11, jeweils in der 64-Bit-Ausgabe
- etwa 400 MB freien Speicherplatz für das Programm, dazu Platz für Ihre Touren und Aufzeichnungen
- das Installationsprogramm **AndyTours-Setup-1.0.1.exe**; Sie erhalten es vom Autor
- für Karten und Tourenplanung eine Internetverbindung
- für den Import neuer Touren einen Garmin Edge 1040, Edge 1000 oder Edge 800 mit USB-Kabel

Weitere Programme müssen Sie nicht installieren. Sie brauchen auch keine Administratorrechte, denn AndyTours wird nur für Ihr Benutzerkonto installiert.

### 2.2 AndyTours installieren

Mit dieser Anleitung richten Sie AndyTours ein und übernehmen auf Wunsch Ihre bisherigen Daten.

1. Beenden Sie AndyTours, falls es schon läuft.
2. Starten Sie die Datei **AndyTours-Setup-1.0.1.exe** mit einem Doppelklick. Erscheint die Meldung **Der Computer wurde durch Windows geschützt**, klicken Sie auf **Weitere Informationen** und dann auf **Trotzdem ausführen**.
3. Auf der Seite **Datenordner** schlägt das Installationsprogramm den Ordner **Dokumente\AndyTours** vor. Übernehmen Sie den Vorschlag oder wählen Sie über die Schaltfläche zum Durchsuchen einen anderen Ordner. Klicken Sie auf **Weiter**.
4. Auf der Seite **Bisherige Daten übernehmen** ist **Bisherige Daten übernehmen** angehakt. Möchten Sie keine Daten übernehmen, entfernen Sie den Haken und machen bei Schritt 6 weiter.
5. Tragen Sie unter **Ordner mit den bisherigen Daten:** den Ordner ein, in dem BikeXperience seine Daten ablegt, oder wählen Sie ihn mit **Durchsuchen …**. Klicken Sie auf **Weiter**.
6. Auf der Seite für die Verknüpfungen können Sie **Verknüpfung auf dem Desktop anlegen** anhaken. Eine Verknüpfung im Startmenü legt das Installationsprogramm immer an.
7. Prüfen Sie die Zusammenfassung. Sie nennt Programmordner, Datenordner, die Übernahme der bisherigen Daten und die Verknüpfungen.
8. Klicken Sie auf **Installieren**.
9. Hatten Sie die Übernahme gewählt, erscheint nach einigen Minuten eine Zusammenfassung der Übernahme. Bestätigen Sie sie mit **OK**.
10. Lassen Sie auf der letzten Seite **AndyTours starten** angehakt und klicken Sie auf **Fertigstellen**.

Danach finden Sie AndyTours im Startmenü und, falls gewählt, auf dem Desktop.

**Hinweis:** Der Datenordner darf nicht im Programmordner von AndyTours liegen. Sonst würde er beim Deinstallieren mit entfernt. Das Installationsprogramm weist einen solchen Ordner ab.

**Hinweis:** Liegen im gewählten Datenordner schon AndyTours-Daten, bleiben sie unverändert. Die Übernahme bisheriger Daten entfällt dann, und ein Hinweis sagt das. Liegen dort andere Dateien, fragt das Installationsprogramm, ob Sie den Ordner trotzdem verwenden wollen. Auch dann entfällt die Übernahme, denn sie ist nur in einen leeren Ordner möglich.

### 2.3 Was bei der Übernahme der bisherigen Daten passiert

Das Installationsprogramm prüft zuerst, was im angegebenen Ordner liegt. Der Ordner wird dabei nur gelesen und nie verändert.

- **BikeXperience-Daten gefunden:** AndyTours übernimmt Touren, Räder und Ausrüstung, Wartungen, Aktivitäten, Kategorien, persönliche Daten und alle Aufzeichnungen. Beschädigte Umlaute in Texten werden repariert, soweit das eindeutig geht. Zugangsdaten aus BikeXperience (z. B. Dropbox) werden nicht übernommen.
- **Nur Aufzeichnungen im FIT-Format gefunden:** Das Installationsprogramm nennt die Anzahl der gefundenen Aufzeichnungen, auch aus Unterordnern. Es fragt, ob diese als Touren übernommen werden sollen. Jede lesbare Aufzeichnung wird dann eine Tour mit denselben Werten wie beim Import vom Edge. Die Touren haben noch kein Rad und die Aktivität „Radfahren“. Beides können Sie später in AndyTours ändern.
- **Nichts gefunden:** Es erscheint der Hinweis **Keine bisherigen Daten gefunden**. Wählen Sie **Anderen Ordner wählen** oder **Ohne Übernahme weiter**.

Nach der Übernahme liegt im Datenordner ein ausführlicher Bericht:

- **migrationsbericht.txt** bei BikeXperience-Daten: übernommene Mengen, Jahressummen (Anzahl, km, hm, Fahrzeit), Hinweise, Warnungen und nicht reparierte Umlaute
- **aufzeichnungsbericht.txt** bei reinen Aufzeichnungen: Anzahl der übernommenen Touren und jede übersprungene Datei mit Grund

Sie öffnen beide Dateien mit dem Windows-Editor.

Die Zusammenfassung, die das Installationsprogramm am Ende der Übernahme zeigt, nennt die übernommenen Mengen und den Ort des Berichts. Bei BikeXperience-Daten nennt sie außerdem die Anzahl der Warnungen und der nicht reparierten Umlaute.

**Hinweis:** Bricht die Übernahme mit einem Fehler ab, ist AndyTours trotzdem installiert. Der Datenordner bleibt dann so, wie er vorher war. Eine Meldung nennt den Grund.

### 2.4 Neue Version installieren

Eine neuere Version installieren Sie genauso wie die erste. Das Installationsprogramm erkennt die vorhandene Installation.

- Datenordner, Daten und Einstellungen bleiben erhalten.
- Die Seiten für den Datenordner und die Übernahme bisheriger Daten entfallen.
- Läuft AndyTours noch, bittet das Installationsprogramm, es zu beenden. Klicken Sie danach auf **Wiederholen**.

### 2.5 Installationsprotokoll

Das Installationsprogramm hält jede Änderung an Ihrem Rechner in der Datei **installationsprotokoll.txt** fest. Sie liegt im Programmordner von AndyTours. Den Programmordner finden Sie im Explorer unter **C:\Benutzer\\(Ihr Benutzername)\AppData\Local\Programs\AndyTours**. Jede Zeile nennt Zeitpunkt, Aktion (angelegt oder geändert), Art und Ziel der Änderung.

### 2.6 AndyTours deinstallieren

Mit dieser Anleitung entfernen Sie AndyTours von Ihrem Rechner.

1. Beenden Sie AndyTours.
2. Öffnen Sie die Windows-Einstellungen und dort **Apps**.
3. Suchen Sie **AndyTours** und wählen Sie **Deinstallieren**.
4. Es erscheint die Frage **Datenordner löschen?**. Wählen Sie **Datenordner behalten** oder **Datenordner mit allen Touren löschen**. Vorgewählt ist **Datenordner behalten**.

Das Deinstallationsprogramm entfernt Programmdateien, Verknüpfungen, den Eintrag unter **Apps** und die Einstellungen von AndyTours außerhalb des Datenordners. Behalten Sie den Datenordner, verwendet eine spätere Installation Ihre Daten wieder.

**Achtung:** Mit **Datenordner mit allen Touren löschen** sind alle Touren, Aufzeichnungen und Wartungen endgültig weg, soweit Sie sie nicht selbst gesichert haben.

---

<a id="kapitel-3"></a>
## 3. Erste Schritte

### 3.1 AndyTours starten

1. Öffnen Sie das Startmenü von Windows.
2. Klicken Sie auf **AndyTours**.

Alternativ doppelklicken Sie die Verknüpfung **AndyTours** auf dem Desktop.

### 3.2 Was beim Start passiert

- AndyTours öffnet den Datenordner, den Sie bei der Installation gewählt haben. Gibt es ihn noch nicht, legt AndyTours ihn an.
- Ist es Zeit für eine Sicherung, sichert AndyTours zuerst Ihren Datenbestand (siehe [Abschnitt 4.9.4](#k4-9-4), Karteikarte **Datensicherung**).
- Sind Wartungsalarme fällig, erscheint ein Hinweis, zum Beispiel **Folgende Wartungen sind fällig:** mit einer Zeile je Alarm.
- AndyTours merkt sich Größe und Position des Hauptfensters und die zuletzt gewählte Karteikarte. Beim nächsten Start sieht alles so aus wie beim Beenden.

Ist der Datenordner nicht erreichbar, zum Beispiel weil das NAS ausgeschaltet ist, lesen Sie in [Kapitel 5](#kapitel-5) nach.

### 3.3 Das Hauptfenster auf einen Blick

**Aufbau:** Das Hauptfenster besteht von oben nach unten aus vier Teilen:

1. **Menüleiste** ganz oben mit den Menüs **Datei**, **Einheit**, **Tourenplanung**, **Extras** und **Hilfe**. Die Menüs sehen aus wie graue Schaltflächen. Ein Klick klappt das Menü auf.
2. **Werkzeugleiste** darunter: links die Auswahl **Jahr:**, daneben die Auswahl **Aktivität:**, rechts davon fett die Summen dieser Auswahl, zum Beispiel **80 Einheiten · 7.016,77 km · 66.572 hm**.
3. **Karteikarten** **Aktuell**, **Trainingseinheiten** und **Wartung**. Sie füllen den größten Teil des Fensters. Die gewählte Karteikarte erkennen Sie an der halbfetten Beschriftung. Ihr Reiter geht nahtlos in die hellgraue Seite darunter über. Die übrigen Reiter sind dunkler grau.
4. **Statusleiste** am unteren Rand: links die Anzahl der Trainingseinheiten, daneben bei Bedarf eine Meldung zur letzten Aktion, rechts **Datenordner:** mit dem Pfad des Datenordners.

Eine Tour heißt in AndyTours **Einheit** oder **Trainingseinheit**.

### 3.4 Grundeinstellungen prüfen

Bevor Sie die erste Tour importieren, lohnt ein Blick in die Einstellungen.

1. Wählen Sie **Extras → Einstellungen…**.
2. Ist Ihnen die Schrift zu klein, wählen Sie auf der Karteikarte **Allgemein** unter **Größe der Oberfläche:** eine größere Stufe. Die Änderung wirkt sofort.
3. Wechseln Sie zur Karteikarte **Auswahllisten**.
4. Wählen Sie unter **Liste:** den Eintrag **Aktivität**. Prüfen Sie, ob Ihre Sportarten vorhanden sind, zum Beispiel „Bike“. Legen Sie fehlende mit **Hinzufügen** an (siehe [Abschnitt 4.9.4](#k4-9-4)).
5. Wählen Sie unter **Liste:** den Eintrag **Equipment**. Prüfen Sie, ob Ihre Räder vorhanden sind. Legen Sie fehlende mit **Hinzufügen** an.
6. Klicken Sie auf **Schließen**.

**Hinweis:** Haben Sie bei der Installation keine bisherigen Daten übernommen, enthält der Datenordner noch keine Aktivität. Legen Sie dann vor der ersten Tour mindestens eine Aktivität an. Die erste Aktivität wird automatisch zur Hauptaktivität, mit **Als Hauptaktivität** können Sie später eine andere wählen. Der Import ordnet jede Tour der Aktivität ihres Rads zu. Hat das Rad keine Aktivität, gilt die Hauptaktivität. Fehlt die Hauptaktivität, weist AndyTours beim Import darauf hin und importiert nichts.

### 3.5 Die erste Tour vom Garmin Edge importieren

Mit dieser Anleitung holen Sie Ihre neuen Touren vom Radcomputer.

1. Schließen Sie den Edge per USB-Kabel an den PC an.
2. Wählen Sie **Datei → Vom Garmin importieren…**.
3. Warten Sie, bis AndyTours den Edge gelesen hat.
4. Im Fenster **Garmin-Import** sind alle neuen Touren angehakt. Prüfen Sie für jede Tour das **Equipment**.
5. Klicken Sie auf **Importieren**.

Die neuen Touren sind danach auf der Karteikarte **Trainingseinheiten** markiert. Die Statusleiste meldet zum Beispiel **3 Touren importiert.** Einzelheiten finden Sie in [Abschnitt 4.2](#k4-2).

### 3.6 AndyTours beenden

Wählen Sie **Datei → Beenden** oder drücken Sie **Alt+F4**. Sie müssen vorher nichts speichern. AndyTours speichert jede Änderung sofort.

---

<a id="kapitel-4"></a>
## 4. Funktionen im Detail

<a id="k4-1"></a>
### 4.1 Das Hauptfenster

#### Titelleiste

Die Titelleiste zeigt **AndyTours** und die Versionsnummer.

#### Werkzeugleiste: Jahr und Aktivität

Mit der Werkzeugleiste legen Sie fest, welche Einheiten Sie sehen.

- **Jahr:** enthält alle Jahre mit Einheiten und immer das laufende Jahr, das neueste zuerst. Mit **alle Jahre** sehen Sie alle Jahre zusammen.
- **Aktivität:** enthält alle Aktivitäten. Mit **alle Aktivitäten** sehen Sie alle zusammen.
- Beim Start sind das laufende Jahr und die Hauptaktivität vorgewählt.
- Rechts daneben stehen fett die Summen der Auswahl, zum Beispiel **42 Einheiten · 1.234,56 km · 12.345 hm**.
- Gibt es noch gar keine Einheiten, steht dort **Noch keine Touren vorhanden.**

Die Auswahl wirkt sofort auf die Tabelle der Trainingseinheiten. Auf der Karteikarte **Aktuell** wirkt nur die Aktivität, nicht das Jahr.

#### Statusleiste

- Links steht die Gesamtzahl, zum Beispiel **1.234 Trainingseinheiten in der Datenbank**. Auf der Karteikarte **Wartung** steht dort die Anzahl der Wartungen.
- Daneben erscheint bei Bedarf eine gelb hinterlegte Meldung zur letzten Aktion, zum Beispiel **3 Touren importiert.**
- Rechts steht der aktuelle Datenordner, zum Beispiel **Datenordner: C:\Benutzer\…\Dokumente\AndyTours**.

#### Tastenkürzel

| Taste | Wirkung |
|---|---|
| **Strg+N** | neue Einheit anlegen |
| **Entf** | markierte Einheiten, Wartungen oder Alarme löschen (mit Rückfrage) |
| **F2** | Wert in der markierten Tabellenzelle ändern |
| **Enter** | Änderung in der Zelle speichern |
| **Esc** | Änderung in der Zelle verwerfen bzw. Fenster schließen |
| **Alt+F4** | AndyTours beenden |

Unterstrichene Buchstaben in Menüs und Schaltflächen erreichen Sie zusammen mit der **Alt**-Taste.

<a id="k4-2"></a>
### 4.2 Menü „Datei“: Touren importieren

#### Vom Garmin importieren

AndyTours liest Touren direkt vom Garmin Edge 1040, Edge 1000 und Edge 800. Der Edge 1040 erscheint am PC nicht als Laufwerk, sondern als Mediengerät (MTP). AndyTours kann trotzdem darauf zugreifen.

**Hinweis:** Getestet ist AndyTours mit dem Edge 1040. Andere Edge-Modelle, die sich am PC als Mediengerät melden, funktionieren wahrscheinlich auch. Dafür gibt es aber keine Gewähr.

Mit dieser Anleitung importieren Sie die neuen Touren.

1. Schließen Sie den Edge per USB an. Sie können auch mehrere Garmin-Geräte gleichzeitig anschließen.
2. Wählen Sie **Datei → Vom Garmin importieren…**.
3. Während AndyTours liest, zeigt ein Fenster **Garmin-Geräte werden gelesen …** den Fortschritt.
4. Das Fenster **Garmin-Import** zeigt alle Touren, die noch nicht in AndyTours sind. Spalten: **Import**, **Datum**, **Start**, **km**, **Fahrzeit**, **Profil**, **Equipment**. Bei mehreren Geräten zeigt die Spalte **Gerät**, woher die Tour stammt.
5. Entfernen Sie den Haken bei **Import** für Touren, die Sie nicht übernehmen möchten.
6. Wählen Sie bei Bedarf in der Spalte **Equipment** ein anderes Rad. Mit **(keines)** bekommt die Tour kein Equipment.
7. Klicken Sie auf **Importieren**.

Die neuen Einheiten sind danach in der Tabelle markiert, soweit sie zur aktuellen Auswahl passen. Die Statusleiste meldet die Anzahl, zum Beispiel **3 Touren importiert.**

**Aufbau des Fensters „Garmin-Import“:** Oben steht der Satz **Diese Touren sind noch nicht in AndyTours. Angehakte Touren werden mit dem gewählten Equipment importiert:**. Darunter folgt die Tabelle mit einer Zeile je Tour. Ganz links steht in der Spalte **Import** ein Kontrollkästchen, ganz rechts in der Spalte **Equipment** eine Auswahlliste. Unten rechts stehen die Schaltflächen **Importieren** und **Abbrechen**. **Importieren** ist nur wählbar, solange mindestens eine Tour angehakt ist.

**Was AndyTours übernimmt:** Datum, Startzeit, Fahrzeit, Distanz, Höhenmeter, Höhen, Geschwindigkeiten, Puls, Trittfrequenz, Temperatur und Leistung kommen aus der Aufzeichnung. Die Fahrzeit ist die gestoppte Zeit ohne Pausen, wie sie der Edge als „Zeit“ anzeigt. Der Name bleibt leer. Die Einheit bekommt die Aktivität ihres Equipments, die Standardkategorien und die Standardkategorie des Equipments. Die Aufzeichnung legt AndyTours im Datenordner im Unterordner des Jahres ab, zum Beispiel **2026**.

**Wie AndyTours das Rad erkennt:** Der Edge fragt nach der Tour, mit welchem Rad Sie gefahren sind, und speichert diese Rad-Kennung in der Aufzeichnung. AndyTours merkt sich bei jedem Import, welche Rad-Kennung und welche Geschwindigkeits- und Trittfrequenzsensoren zu welchem Equipment gehören. Beim nächsten Import schlägt AndyTours danach das passende Equipment vor. Passt nichts, schlägt es das Standard-Equipment der Hauptaktivität vor. Die gemerkten Kennungen sehen Sie im Fenster des Equipments (siehe [Abschnitt 4.9.4](#k4-9-4)).

**Hinweis:** Eine Tour mit gleichem Datum und gleicher Startzeit wie eine vorhandene Einheit gilt als schon importiert. Sie erscheint nicht in der Liste.

**Hinweis:** Ist kein Gerät angeschlossen, erscheint **Kein Garmin-Gerät gefunden. Bitte den Edge per USB anschließen.** Sind alle Touren schon vorhanden, erscheint **Keine neuen Touren gefunden.**

**Hinweis:** Kann AndyTours eine Aufzeichnung nicht lesen, überspringt es sie. Die übrigen Touren werden trotzdem importiert. Eine Meldung nennt am Ende die übersprungenen Dateien.

#### FIT-Dateien importieren

Haben Sie kein Gerät zur Hand, importieren Sie Aufzeichnungsdateien von der Festplatte. Eine FIT-Datei ist das Dateiformat, in dem Garmin-Geräte Touren aufzeichnen.

1. Wählen Sie **Datei → FIT-Dateien importieren…**.
2. Wählen Sie eine oder mehrere Dateien mit der Endung **.fit** und klicken Sie auf **Öffnen**.
3. Es erscheint dasselbe Fenster **Garmin-Import** wie beim Gerät. Gehen Sie weiter vor wie oben ab Schritt 5.

#### Beenden

**Datei → Beenden** schließt AndyTours.

<a id="k4-3"></a>
### 4.3 Karteikarte „Aktuell“

Die Karteikarte **Aktuell** zeigt Ihre Bestleistungen und Ihren Jahresfortschritt.

**Aufbau:** Oben links steht die Liste **Jahresrekorde**, rechts daneben die Liste **Einzelrekorde**. Darunter folgt die Überschrift des Diagramms, zum Beispiel **Kilometer (Summe Entfernung)**. Unter der Überschrift stehen zwei Zeilen mit Kennzahlen des laufenden Jahres. Die erste nennt Stand, Ziel, **Soll heute** und **Vorsprung** bzw. **Rückstand**. Die zweite Zeile ist grau und beginnt meist mit **noch …**. Den restlichen Platz füllt das Diagramm der Jahressummen. Reicht der Platz nicht, zum Beispiel bei großer Oberfläche, können Sie die Karteikarte nach unten rollen.

#### Welche Einheiten zählen

- Es zählen immer alle Jahre. Die Auswahl **Jahr:** der Werkzeugleiste wirkt hier nicht.
- Die Auswahl **Aktivität:** wirkt. Bei **alle Aktivitäten** werden alle zusammengezählt.
- Von der Filterleiste der Trainingseinheiten wirkt nur die **Kategorie:**.

#### Bestleistungen

Oben stehen zwei Listen:

- **Jahresrekorde:** der höchste Jahreswert für **Kilometer**, **Anzahl Einheiten**, **Höhenmeter** und **Fahrzeit**, jeweils mit dem Jahr.
- **Einzelrekorde:** **Längste Tour**, **Meiste Höhenmeter**, **Längste Fahrzeit**, **Höchste Ø-Geschwindigkeit**, **Höchste Geschwindigkeit** und **Höchster Punkt**, jeweils mit Wert, Datum und Name der Einheit.

Jede Zeile nennt links die Bezeichnung mit Doppelpunkt, daneben rechtsbündig den Wert und dahinter das Jahr bzw. Datum und Name der Einheit.

Hält das laufende Jahr einen Rekord, ist die ganze Zeile fett und blau hervorgehoben, im selben Blau wie die Linie des Diagramms. Haben mehrere Jahre oder Einheiten denselben Rekordwert, gilt der frühere.

Möchten Sie die Einheit eines Einzelrekords ansehen, doppelklicken Sie auf die Zeile. Über den Einzelrekorden wird der Mauszeiger zur Hand, und ein Hinweis sagt **Doppelklick öffnet die Einheit**. Das Bearbeitungsfenster der Einheit öffnet sich.

#### Diagramm der Jahressummen

Das Diagramm zeigt die Jahressumme für jedes Jahr mit Einheiten. Das laufende Jahr steht ganz rechts.

So wechseln Sie den gezeigten Wert:

1. Klicken Sie in das Diagramm.
2. Der Wert wechselt der Reihe nach: **Kilometer (Summe Entfernung)** → **Anzahl Einheiten** → **Höhenmeter (Summe Höhenmeter)** → **Fahrzeit** → wieder Kilometer.

Die Überschrift über dem Diagramm nennt den gezeigten Wert. Die waagrechte Achse zeigt die Jahre, die senkrechte Achse die Summe. Ihre Beschriftung nennt die Einheit, zum Beispiel **km** oder **Stunden**.

So lesen Sie das Diagramm:

- Die Jahressummen bilden eine blaue Linie mit hellblau gefüllter Fläche darunter. Jedes Jahr hat einen blauen Punkt.
- Haben Sie Saisonziele festgelegt, erscheint das Ziel eines Jahres als kurze rote gestrichelte Linie über diesem Jahr.
- Im laufenden Jahr markiert eine rote Raute den Soll-Stand für heute.

#### Kennzahlen des laufenden Jahres

Unter der Überschrift stehen zwei Zeilen zum laufenden Jahr:

- Mit Saisonziel: Stand, Ziel, Anteil in Prozent, **Soll heute** und **Vorsprung** oder **Rückstand**, zum Beispiel **2026: 4.321,5 km von 6.000 km (72 %) · Soll heute 4.900,3 km · 578,8 km Rückstand**. Darunter steht grau die nötige Leistung bis zum 31. Dezember, zum Beispiel **noch 1.678,5 km – 167,9 km pro Woche**. Ist das Ziel erreicht, steht dort **Ziel erreicht** mit dem Überschuss.
- Ohne Saisonziel: nur der Stand und der Hinweis **kein Saisonziel – unter Extras → Saisonziele… festlegen**.
- Für die Fahrzeit gibt es kein Saisonziel. Dort steht nur der Stand.

Der Soll-Stand ist das Ziel anteilig zu den bereits vergangenen Tagen des Jahres.

Enthält die Auswahl keine Einheiten, erscheint **Keine Einheiten in dieser Auswahl.**

<a id="k4-4"></a>
### 4.4 Karteikarte „Trainingseinheiten“

Diese Karteikarte zeigt alle Einheiten der gewählten Jahres- und Aktivitätsauswahl in einer Tabelle.

**Aufbau:** Oben steht die Filterleiste mit den Feldern **Kategorie:**, **Equipment:**, **Bereich:**, **km von** … **bis**, **hm von** … **bis**, **Suche:** und ganz rechts der Schaltfläche **Filter zurücksetzen**. Ist das Fenster schmal, bricht die Filterleiste in eine zweite Zeile um. Darunter folgt die Tabelle der Einheiten. Am unteren Rand steht die Summenzeile mit fetter Schrift auf hellgrauem Grund.

#### Die Tabelle

- Jede Zeile ist eine Einheit. Die neueste steht anfangs oben.
- Spalten: **Datum**, **Start**, **Name**, **Aktivität**, **Kategorien**, **Equipment**, **km**, **hm**, **Fahrzeit**, **Ø km/h**, **max km/h**, **Ø Puls**, **max Puls**, **Trittfrequenz**, **°C**, **max Höhe**, **Bereich**, **Kommentar**.
- Die höchsten Werte der angezeigten Einheiten in **km**, **hm**, **Fahrzeit**, **Ø km/h** und **max km/h** sind rot. In einer markierten Zeile erscheinen sie hellrot und halbfett, damit sie auf der Markierungsfarbe lesbar bleiben. Haben mehrere Einheiten denselben Bestwert, sind alle rot.
- Fehlt ein Wert, bleibt die Zelle leer. Ein fehlender Wert zählt nie als Bestwert.
- Ausgemustertes Equipment trägt den Zusatz **(ausgemustert)**.
- Die Summenzeile am unteren Rand nennt unter der Spalte **Name** die Anzahl der angezeigten Einheiten und unter **km**, **hm** und **Fahrzeit** deren Summen. Sie bleibt beim Blättern und Sortieren stehen.
- Enthält die Auswahl keine Einheiten, erscheint **Keine Einheiten in dieser Auswahl.**

#### Sortieren

1. Klicken Sie auf eine Spaltenüberschrift. Die Tabelle wird nach dieser Spalte sortiert.
2. Klicken Sie erneut darauf. Die Reihenfolge kehrt sich um.

#### Spalten ein- und ausblenden

Mit dieser Anleitung legen Sie fest, welche Spalten Sie sehen.

1. Klicken Sie mit der rechten Maustaste auf eine Spaltenüberschrift.
2. Ein Menü listet alle 18 Spalten in ihrer ursprünglichen Reihenfolge, sichtbare mit Häkchen, zum Beispiel **Distanz (km)**, **Höhenmeter (hm)**, **Temperatur (°C)**, **Maximalhöhe** und **Trainingsbereich**. Am Ende steht, durch eine Linie abgetrennt, der fette Eintrag **Übernehmen**.
3. Klicken Sie auf eine Spalte. Ist sie sichtbar, blendet AndyTours sie aus. Ist sie ausgeblendet, blendet AndyTours sie ein. Die Tabelle hinter dem Menü zeigt die Änderung sofort.
4. Klicken Sie bei Bedarf weitere Spalten an. Das Menü bleibt dabei offen.
5. Klicken Sie auf **Übernehmen**, um das Menü zu schließen.

Vor jeder sichtbaren Spalte steht im Menü ein Häkchen. Die letzte sichtbare Spalte lässt sich nicht ausblenden. Ihr Eintrag im Menü ist dann ausgegraut.

#### Spalten verschieben und Breite ändern

- Ziehen Sie eine Spaltenüberschrift mit der Maus an eine andere Stelle. Die Spalte wandert dorthin, die Summenzeile folgt.
- Ziehen Sie den Rand einer Spaltenüberschrift, um die Breite zu ändern. Ein Doppelklick auf den Rand passt die Breite dem Inhalt an.

AndyTours merkt sich sichtbare Spalten, Reihenfolge und Breite. Nach einem Neustart ist alles wie zuvor.

#### Die Filterleiste

Mit der Filterleiste grenzen Sie die Einheiten der Jahres- und Aktivitätsauswahl weiter ein. Jede Eingabe wirkt sofort.

| Feld | Wirkung |
|---|---|
| **Kategorie:** | nur Einheiten mit dieser Kategorie, auch wenn sie weitere Kategorien haben |
| **Equipment:** | nur Einheiten mit diesem Equipment |
| **Bereich:** | nur Einheiten mit diesem Trainingsbereich |
| **km von** … **bis** | nur Einheiten in diesem Distanzbereich, Grenzen eingeschlossen |
| **hm von** … **bis** | nur Einheiten in diesem Höhenmeterbereich, Grenzen eingeschlossen |
| **Suche:** | nur Einheiten, deren Name oder Kommentar den Suchbegriff enthält |

- Die Auswahllisten bieten nur Werte an, die in der aktuellen Auswahl vorkommen. Mit **(alle)** ist der Filter aus.
- Sie können **von** oder **bis** auch allein ausfüllen.
- Mehrere Filter wirken zusammen. Es erscheinen nur Einheiten, die alle Filter erfüllen.
- Summenzeile und rote Bestwerte beziehen sich dann nur auf die angezeigten Einheiten.
- Solange ein Filter gesetzt ist, ist die Schaltfläche **Filter zurücksetzen** gelb hinterlegt und fett beschriftet.
- Passt keine Einheit, erscheint **Keine Einheiten passen zum Filter.**
- Die Filter bleiben beim Wechsel von Jahr oder Aktivität erhalten. Nach einem Neustart sind sie leer.

**Hinweis:** Steht bei **km** oder **hm** keine gültige Zahl oder ist „von“ größer als „bis“, bekommt das Feld einen roten Rand und einen hellroten Hintergrund. Dieser Filter wirkt dann nicht. Zeigen Sie mit der Maus auf das Feld, nennt ein Hinweis den Grund.

#### Suchen

Mit dieser Anleitung finden Sie Touren über einen Begriff in Name oder Kommentar, zum Beispiel alle Touren „mit Sabine“.

1. Wählen Sie in der Werkzeugleiste unter **Jahr:** den Eintrag **alle Jahre**, wenn Sie in allen Jahren suchen möchten.
2. Tippen Sie den Begriff in das Feld **Suche:**.

Die Tabelle zeigt schon beim Tippen nur passende Einheiten. Groß- und Kleinschreibung spielen keine Rolle. Bei mehreren Wörtern muss jedes Wort in Name oder Kommentar vorkommen, in beliebiger Reihenfolge.

#### Filter zurücksetzen

Klicken Sie auf **Filter zurücksetzen**. Alle Filter und das Suchfeld werden geleert. Die Tabelle zeigt wieder die ganze Jahres- und Aktivitätsauswahl.

#### Werte direkt in der Tabelle ändern

Einzelne Werte ändern Sie ohne Bearbeitungsfenster direkt in der Tabelle.

1. Klicken Sie die Zelle an.
2. Drücken Sie **F2**, klicken Sie die markierte Zelle noch einmal an oder tippen Sie einfach los.
3. Geben Sie den neuen Wert ein. Bei **Aktivität**, **Equipment** und **Bereich** wählen Sie aus einer Liste.
4. Drücken Sie **Enter** oder klicken Sie in eine andere Zelle. Die Änderung ist sofort gespeichert.

Mit **Esc** verwerfen Sie die Änderung. **Ø km/h**, **Kategorien** und **Kommentar** lassen sich in der Tabelle nicht ändern. Dafür nutzen Sie das Bearbeitungsfenster.

**Hinweis:** Ein ungültiger Wert färbt die Zelle rot. Die Zelle bleibt in Bearbeitung, bis der Wert gültig ist oder Sie **Esc** drücken.

#### Rechtsklickmenü

Ein Rechtsklick auf eine Zeile öffnet ein Menü mit denselben Befehlen wie das Menü **Einheit** (siehe [Abschnitt 4.5](#k4-5)). Ein Doppelklick auf eine Zeile öffnet das Bearbeitungsfenster.

<a id="k4-5"></a>
### 4.5 Menü „Einheit“: Einheiten anlegen, ändern und zuordnen

Alle Befehle finden Sie im Menü **Einheit** und im Rechtsklickmenü der Tabelle.

| Befehl | wählbar, wenn … |
|---|---|
| **Neue Einheit…** | immer (auch **Strg+N**) |
| **Bearbeiten…**, **Kopieren…** | genau eine Einheit markiert ist |
| **Tourdetail…** | eine Einheit mit Aufzeichnung oder mehrere Einheiten markiert sind |
| **Kategorien zuordnen…**, **Aktivität ändern…**, **Equipment ändern…**, **Löschen** | mindestens eine Einheit markiert ist |

Mehrere Einheiten markieren Sie wie im Explorer mit **Strg**+Klick oder **Umschalt**+Klick.

#### Neue Einheit anlegen

Mit dieser Anleitung erfassen Sie eine Tour von Hand, zum Beispiel ohne Aufzeichnung.

1. Wählen Sie **Einheit → Neue Einheit…** oder drücken Sie **Strg+N**.
2. Das Fenster **Neue Einheit** öffnet sich. Vorbelegt sind das heutige Datum, die Aktivität aus der Werkzeugleiste (bei **alle Aktivitäten** die Hauptaktivität), das Standard-Equipment dieser Aktivität und die Standardkategorien.
3. Füllen Sie die gewünschten Felder aus. Nur **Datum:** und **Aktivität:** sind Pflicht.
4. Klicken Sie auf **Speichern**.

Die neue Einheit ist danach in der Tabelle markiert. Passt sie nicht zur aktuellen Auswahl, meldet die Statusleiste **Gespeichert – liegt außerhalb der aktuellen Auswahl.**

#### Das Bearbeitungsfenster

Dasselbe Fenster dient zum Anlegen, Bearbeiten und Kopieren. Es heißt je nach Befehl **Neue Einheit**, **Einheit bearbeiten** oder **Einheit kopieren**.

**Aufbau:** Links steht die Gruppe **Allgemein**, rechts daneben die Gruppe **Werte**. Darunter folgt über die ganze Breite das Feld **Kommentar:**. Unten rechts stehen die Schaltflächen **Speichern** und **Abbrechen**.

- **Allgemein:** untereinander **Datum:**, **Startzeit:**, **Name:**, **Aktivität:**, **Equipment:**, **Trainingsbereich:** und **Kategorien:**. Die Kategorien stehen in einer Liste mit einem Kontrollkästchen je Kategorie. Sie können mehrere ankreuzen.
- **Werte:** in zwei Spalten. Links stehen **Distanz (km):**, **Höhenmeter:**, **Fahrzeit (h:mm):**, **Ø km/h:**, **max km/h:**, **Starthöhe (m):**, **Maximalhöhe (m):** und **Temperatur (°C):**. Rechts stehen **Ø Puls:**, **max Puls:**, **min Puls:**, **Erholungspuls:**, **Trittfrequenz:**, **Leistung (W):** und **Gewicht (kg):**. **Ø km/h:** ist kein Eingabefeld. AndyTours berechnet den Wert aus Distanz und Fahrzeit und zeigt ihn grau an.
- **Kommentar:** freier Text über mehrere Zeilen

Hinweise zur Eingabe:

- Das Datum geben Sie als TT.MM.JJJJ ein, zum Beispiel 01.05.2026.
- Die Fahrzeit geben Sie als Stunden und Minuten ein, zum Beispiel **3:45**. Sekunden sind freiwillig, zum Beispiel **3:45:12**.
- Zahlen dürfen nicht negativ sein. Nur die Temperatur darf unter null liegen.
- Ein ungültiges oder fehlendes Pflichtfeld bekommt einen roten Rand, Eingabefelder zusätzlich einen hellroten Hintergrund. **Speichern** ist dann ausgegraut.
- **Equipment:** bietet **(kein)** und die Equipments an, die zur gewählten Aktivität passen oder keine Aktivität haben. Wechseln Sie bei einer neuen Einheit die Aktivität, wird deren Standard-Equipment gewählt.
- Wählen Sie ein Equipment mit Standardkategorie, wird diese Kategorie zusätzlich angehakt.
- **Trainingsbereich:** bietet einen leeren Eintrag und die Einträge der Auswahlliste an. Neue Kürzel legen Sie in den Einstellungen an.

Schließen Sie das Fenster mit **Abbrechen** oder **Esc**, nachdem Sie etwas geändert haben, fragt AndyTours **Änderungen verwerfen?**. Mit **Ja** schließt sich das Fenster ohne Speichern. Mit **Nein** bleibt es offen.

#### Einheit bearbeiten

1. Doppelklicken Sie auf die Zeile der Einheit. Oder markieren Sie sie und wählen **Einheit → Bearbeiten…**.
2. Das Fenster **Einheit bearbeiten** zeigt alle Werte der Einheit, auch solche, die in der Tabelle fehlen.
3. Ändern Sie die Werte und klicken Sie auf **Speichern**.

Die Aufzeichnung der Einheit bleibt dabei erhalten.

#### Einheit kopieren

Eine Kopie spart Tipparbeit, wenn Sie eine Tour öfter fahren.

1. Markieren Sie eine Einheit.
2. Wählen Sie **Einheit → Kopieren…**.
3. Das Fenster **Einheit kopieren** enthält alle Werte der markierten Einheit. Nur das Datum ist auf heute gesetzt. Die Aufzeichnung wird nicht mitkopiert.
4. Passen Sie die Werte an und klicken Sie auf **Speichern**.

Die ursprüngliche Einheit bleibt unverändert.

#### Einheiten löschen

1. Markieren Sie eine oder mehrere Einheiten.
2. Wählen Sie **Einheit → Löschen** oder drücken Sie **Entf**.
3. Bestätigen Sie die Rückfrage mit **Ja**. Bei einer Einheit lautet sie zum Beispiel **Einheit ‚Bründlingalm‘ vom 01.05.2026 wirklich löschen?**, bei mehreren **3 Einheiten wirklich löschen?**.

Die Aufzeichnungsdateien bleiben im Datenordner erhalten. Nur ihre Zuordnung zur Einheit entfällt.

#### Kategorien zuordnen

**Aufbau des Fensters „Kategorien zuordnen“:** Oben steht ein erklärender Satz, zum Beispiel **Kategorien der 42 markierten Einheiten (Strich = nur bei manchen; bleibt unverändert):**. Darunter stehen alle Kategorien alphabetisch, jede mit einem Kontrollkästchen. Unten rechts stehen **OK** und **Abbrechen**.

Das Kontrollkästchen vor einer Kategorie kann drei Zustände zeigen:

- **Häkchen:** Alle markierten Einheiten haben die Kategorie.
- **Leeres Kästchen:** Keine markierte Einheit hat die Kategorie.
- **Blau gefülltes Kästchen mit einem waagrechten Strich:** Nur manche der markierten Einheiten haben die Kategorie.

Mit dieser Anleitung ordnen Sie mehreren Einheiten auf einmal Kategorien zu.

1. Markieren Sie die Einheiten.
2. Wählen Sie **Einheit → Kategorien zuordnen…**.
3. Setzen oder entfernen Sie Häkchen. Bei einer Kategorie, die nur manche Einheiten haben, wechselt das Kästchen bei jedem Klick weiter: vom Strich zu leer, dann zum Häkchen, dann wieder zum Strich.
4. Klicken Sie auf **OK**.
5. Sind mehrere Einheiten markiert, fragt AndyTours zum Beispiel **42 Einheiten ändern?**. Bestätigen Sie mit **Ja**.

Eine Kategorie, deren Kästchen den Strich zeigt, ändert sich bei keiner Einheit.

#### Aktivität ändern

1. Markieren Sie die Einheiten.
2. Wählen Sie **Einheit → Aktivität ändern…**.
3. Wählen Sie im Fenster **Aktivität ändern** die neue Aktivität. Ein Doppelklick wirkt wie **OK**.
4. Klicken Sie auf **OK** und bestätigen Sie bei mehreren Einheiten die Rückfrage.

Die Einheiten behalten ihr Equipment, auch wenn es nicht zur neuen Aktivität passt.

**Aufbau der Fenster „Aktivität ändern“ und „Equipment ändern“:** Oben steht ein kurzer Satz, zum Beispiel **Neue Aktivität für 3 Einheiten:** bzw. **Neues Equipment für 3 Einheiten:**. Bei einer einzigen Einheit heißt es **… für die markierte Einheit:**. Darunter steht die Liste der wählbaren Einträge, unten rechts **OK** und **Abbrechen**. **OK** ist erst wählbar, wenn ein Eintrag markiert ist.

#### Equipment ändern

1. Markieren Sie die Einheiten.
2. Wählen Sie **Einheit → Equipment ändern…**.
3. Wählen Sie im Fenster **Equipment ändern** das neue Equipment oder **(kein)**. Angeboten werden Equipments im Einsatz, die zu einer Aktivität der markierten Einheiten passen oder keine Aktivität haben.
4. Klicken Sie auf **OK** und bestätigen Sie bei mehreren Einheiten die Rückfrage.

Die Kategorien der Einheiten bleiben dabei unverändert.

**Hinweis:** Passen geänderte Einheiten danach nicht mehr zur Auswahl oder zum Filter, nennt die Statusleiste ihre Anzahl, zum Beispiel **3 geänderte Einheiten liegen außerhalb der aktuellen Auswahl.**

#### Tourdetail

**Einheit → Tourdetail…** öffnet das Höhenprofil und die Karte der markierten Einheit. Einzelheiten stehen im nächsten Abschnitt.

<a id="k4-6"></a>
### 4.6 Fenster „Tourdetail“

Das Tourdetail zeigt den Verlauf einer Tour: das Höhenprofil und die Strecke auf der Karte. Sie können mehrere Tourdetails gleichzeitig offen haben. Das Hauptfenster bleibt dabei bedienbar.

So öffnen Sie das Tourdetail:

1. Markieren Sie auf der Karteikarte **Trainingseinheiten** eine Einheit mit Aufzeichnung.
2. Wählen Sie **Einheit → Tourdetail…** oder im Rechtsklickmenü **Tourdetail…**.

**Aufbau:** Das Fenster heißt **Tourdetail**. Ganz oben steht halbfett eine Kopfzeile mit Datum, Name, Distanz, Höhenmetern und höchstem Punkt der Einheit. Darunter liegen die zwei Karteikarten **Höhenprofil** und **Karte**.

#### Karteikarte „Höhenprofil“

Oben auf der Karteikarte steht eine Leiste mit **Achse:** und den Optionen **Distanz** und **Fahrzeit**. Rechts daneben folgt **Einblenden:** mit den Kontrollkästchen **Puls**, **Geschwindigkeit**, **Trittfrequenz** und **Temperatur**. Darunter füllt das Höhenprofil den Rest der Karteikarte. Die Höhe erscheint als blaue Linie mit hellblau gefüllter Fläche darunter.

- Die waagrechte Achse zeigt die Distanz in km, die senkrechte die Höhe in m.
- Unter **Achse:** schalten Sie zwischen **Distanz** und **Fahrzeit** um. Bei **Fahrzeit** werden Pausen herausgelassen.
- Unter **Einblenden:** setzen Sie Häkchen bei **Puls**, **Geschwindigkeit**, **Trittfrequenz** oder **Temperatur**. Jeder Wert erscheint als zusätzliche dünne Linie mit eigener Skala am rechten Rand. Die Farben: Puls rot, Geschwindigkeit grün, Trittfrequenz violett, Temperatur orange. Skala und Beschriftung haben dieselbe Farbe wie ihre Linie. Enthält die Aufzeichnung einen Wert nicht, ist sein Kontrollkästchen ausgegraut.
- Fahren Sie mit der Maus über das Profil. An dieser Stelle erscheinen Distanz, Fahrzeit, Höhe und die eingeblendeten Werte.
- Ziehen Sie mit gedrückter linker Maustaste einen Bereich auf, um ihn zu vergrößern. Ein Doppelklick zeigt wieder die ganze Tour.

Enthält die Aufzeichnung keine Höhendaten, zum Beispiel bei einer Fahrt auf der Rolle, erscheint **Die Aufzeichnung enthält keine Höhendaten.**

#### Karteikarte „Karte“

Oben auf der Karteikarte steht eine Leiste. Sie enthält von links nach rechts **Karte:** mit den Optionen **OpenStreetMap** und **OpenTopoMap**, die Schaltflächen **Ganze Strecke**, **Als GPX exportieren…**, **Auf Garmin übertragen…** und **Overlays…** und am Ende eine graue Zeichenerklärung. Darunter füllt die Karte den Platz. Unter der Karte steht die Leiste **Track bearbeiten:** (siehe unten). Ist das Fenster schmal, brechen die Leisten in eine zweite Zeile um.

Die Karte zeigt die Strecke als blaue Linie. Ein grüner Punkt markiert den Start, ein rotes Quadrat das Ziel. Die Zeichenerklärung oben wiederholt diese Zeichen: **● Start** und **■ Ziel**.

- Unter **Karte:** wählen Sie **OpenStreetMap** oder **OpenTopoMap**. OpenTopoMap zeigt zusätzlich Höhenlinien und Wege.
- Drehen Sie das Mausrad, um an der Mausposition zu vergrößern oder zu verkleinern.
- Ziehen Sie die Karte mit gedrückter linker Maustaste, um sie zu verschieben.
- **Ganze Strecke** zeigt wieder die ganze Strecke.
- **Overlays…** öffnet die Overlay-Verwaltung (siehe [Abschnitt 4.8](#k4-8)).
- Unten rechts steht der Hinweis auf die Urheber der Karte.

Einmal angesehene Kartenausschnitte erscheinen später auch ohne Internet. Fehlt die Internetverbindung bei einem neuen Ausschnitt, zeichnet AndyTours die Strecke trotzdem. Oben erscheint **Die Karte konnte nicht geladen werden. Besteht eine Internetverbindung?**

Enthält die Aufzeichnung keine GPS-Daten, erscheint **Die Aufzeichnung enthält keine GPS-Daten.**

#### Track als GPX-Datei exportieren

Eine GPX-Datei ist ein verbreitetes Dateiformat für GPS-Strecken. Andere Programme wie Garmin BaseCamp oder Komoot können sie öffnen.

1. Klicken Sie auf der Karteikarte **Karte** auf **Als GPX exportieren…**.
2. Der Speichern-Dialog schlägt den zuletzt verwendeten Ordner und einen Namen aus Datum und Name der Einheit vor, zum Beispiel **2026-05-01 Bründlingalm.gpx**.
3. Klicken Sie auf **Speichern**.

Neben der Schaltfläche erscheint zum Beispiel **Track exportiert: 2026-05-01 Bründlingalm.gpx**. Die Datei enthält Position und Höhe jedes Messpunkts, aber keine Uhrzeiten.

#### Track bearbeiten

Unter der Karte steht die Leiste **Track bearbeiten:**. Damit kürzen, drehen und glätten Sie die Strecke, etwa als Vorlage zum Weitergeben. Die Einheit und ihre Aufzeichnung bleiben dabei unverändert.

Die Leiste enthält von links nach rechts: den Regler **Start** mit seiner Stelle in km, den Regler **Ziel** mit seiner Stelle in km, das Kontrollkästchen **Umkehren**, **Glätten:** mit den Optionen **aus**, **schwach** und **stark**, die Schaltfläche **Zurücksetzen** und die Angabe **Länge:**.

1. Ziehen Sie den Regler **Start** nach rechts, um den Anfang der Strecke abzuschneiden. Daneben steht die Stelle in km.
2. Ziehen Sie den Regler **Ziel** nach links, um das Ende abzuschneiden.
3. Haken Sie **Umkehren** an, damit die Strecke rückwärts läuft.
4. Wählen Sie bei **Glätten:** **schwach** oder **stark**, um das GPS-Zittern zu entfernen. **aus** zeigt die ungeglättete Linie.
5. Rechts steht die Länge der bearbeiteten Strecke, zum Beispiel **Länge: 42,3 km**.
6. Klicken Sie auf **Als GPX exportieren…**, um das Ergebnis zu speichern. Der vorgeschlagene Name trägt den Zusatz **(bearbeitet)**.

**Zurücksetzen** stellt die ganze, unbearbeitete Strecke wieder her. Schließen Sie das Tourdetail, ist die Bearbeitung verworfen.

**Hinweis:** Die Leiste fehlt bei Mehrtagestouren und bei Aufzeichnungen ohne GPS-Daten.

#### Auf Garmin übertragen

Ein Course ist eine Strecke, die der Garmin Edge beim Nachfahren anzeigt, mit Höhenprofil und Abbiegehinweisen.

**Aufbau des Fensters „Auf Garmin übertragen“:** Oben steht eine kurze Erklärung, wo der Course auf dem Edge erscheint. Darunter folgt das Namensfeld des Course, bei einer Mehrtagestour ein Feld je Etappe. Bei einer gefahrenen Strecke folgt die Auswahl **Fahrprofil:** mit einer grauen Erklärung darunter. Nur bei mehreren angeschlossenen Geräten kommt die Auswahl **Gerät:** hinzu. Unten rechts stehen **Übertragen** und **Abbrechen**.

So übertragen Sie die gefahrene Strecke als Course auf den Edge:

1. Schließen Sie den Edge per USB an.
2. Klicken Sie auf der Karteikarte **Karte** auf **Auf Garmin übertragen…**.
3. Im Fenster **Auf Garmin übertragen** steht bei **Strecke:** ein Name aus Datum und Name der Einheit. Ändern Sie ihn bei Bedarf.
4. Wählen Sie das **Fahrprofil:**. Damit ermittelt der Routingdienst die Abbiegungen. Die Linie bleibt so, wie Sie gefahren sind.
5. Sind mehrere Geräte angeschlossen, wählen Sie unter **Gerät:** das Ziel.
6. Klicken Sie auf **Übertragen**.

Danach erscheint zum Beispiel **Course „2026-05-01 Bründlingalm“ übertragen. Er erscheint auf dem Edge nach dem Trennen vom Computer unter „Courses“.**

Haben Sie die Strecke vorher bearbeitet, wird die bearbeitete Strecke übertragen.

**Hinweis:** Lassen sich die Abbiegungen nicht ermitteln, zum Beispiel ohne Internet, überträgt AndyTours den Course ohne Abbiegehinweise. Ein Hinweis nennt den Grund.

**Hinweis:** Wartet auf dem Edge schon ein noch nicht übernommener Course mit demselben Namen, fragt AndyTours, ob er ersetzt werden soll.

#### Mehrtagestour

Mehrere Einheiten, zum Beispiel die Etappen einer Radreise, sehen Sie gemeinsam an.

1. Markieren Sie alle Etappen in der Tabelle.
2. Wählen Sie **Einheit → Tourdetail…**.

Das Tourdetail ordnet die Etappen nach Datum und Startzeit. Oben stehen der Zeitraum, die Anzahl der Etappen, die Summen von Distanz, Höhenmetern und Fahrzeit, die Durchschnittsgeschwindigkeit und der höchste Punkt.

- **Höhenprofil:** Die Etappen folgen hintereinander. Senkrechte graue gestrichelte Linien markieren jeden Etappenwechsel. Beim Überfahren mit der Maus erscheinen zusätzlich Datum und Name der Etappe.
- **Karte:** Jede Etappe hat eine eigene Farbe, der Reihe nach blau, orange, violett und blaugrün. Ab der fünften Etappe wiederholen sich die Farben. Ein kleiner dunkelgrauer Punkt markiert jeden Etappenwechsel. Die Zeichenerklärung nennt dafür zusätzlich **• Etappenwechsel**.
- **Als GPX exportieren…** speichert alle Etappen in eine Datei, zum Beispiel **2026-05-01 bis 2026-05-04 Alpencross.gpx**.
- **Auf Garmin übertragen…** überträgt jede Etappe als eigenen Course. Den Namen jeder Etappe können Sie einzeln ändern.

Etappen ohne Aufzeichnung zählen zu den Summen oben. In Profil und Karte fehlen sie, und ein orangebrauner Hinweis unter der Kopfzeile nennt sie mit Datum und Name. Hat keine Etappe eine Aufzeichnung, erscheint **Keine der Einheiten hat eine Aufzeichnung.**

Die Mehrtagestour wird nicht gespeichert. Sie entsteht jedes Mal neu aus den markierten Einheiten.

<a id="k4-7"></a>
### 4.7 Karteikarte „Wartung“

Auf dieser Karteikarte erfassen Sie Wartungen Ihrer Räder und richten Wartungsalarme ein. Sind Alarme fällig, steht ihre Anzahl im Titel der Karteikarte, zum Beispiel **Wartung (2)**.

**Aufbau:** Ganz oben steht die Auswahl **Equipment:**. Darunter folgt die Gruppe **Alarme** mit den Schaltflächen **Neuer Alarm…**, **Bearbeiten…** und **Löschen** und darunter der Liste der Alarme. Die Liste ist in der Höhe begrenzt, damit die Wartungen sichtbar bleiben. Den restlichen Platz füllt die Gruppe **Wartungen** mit den Schaltflächen **Neue Wartung…**, **Bearbeiten…** und **Löschen** und darunter der Tabelle der Wartungen.

#### Equipment-Auswahl

Oben wählen Sie unter **Equipment:** ein Rad. Beide Listen zeigen dann nur dessen Alarme und Wartungen. Mit **Alle Equipments** sehen Sie wieder alle.

Die Auswahl richtet sich nach der Aktivität in der Werkzeugleiste. Bei „Bike“ sehen Sie zum Beispiel nur die Räder, keine Laufschuhe. Bei **alle Aktivitäten** gilt die Hauptaktivität. Ausgemusterte Equipments stehen nicht in der Auswahl. Ihre Wartungen und Alarme sehen Sie aber unter **Alle Equipments**, mit dem Zusatz **(ausgemustert)**.

#### Alarme

Ein Wartungsalarm erinnert nach einer bestimmten Kilometerzahl an eine Wartung, zum Beispiel „Kettenwechsel alle 3.000 km“.

Die Liste **Alarme** hat die Spalten **Rad**, **Tätigkeit**, **Alle (km)**, **Gefahren (km)** und **Verbleibend (km)**.

- **Gefahren (km)** zählt ab der letzten Wartung mit derselben Tätigkeit an diesem Rad. Gibt es keine, zählt der Alarm ab dem Tag, an dem Sie ihn angelegt haben.
- **Rot hinterlegte Zeile mit dunkelroter, halbfetter Schrift:** Der Alarm ist fällig. Das Rad ist seit der letzten passenden Wartung bzw. seit dem Anlegen des Alarms mindestens das Intervall gefahren, **Verbleibend (km)** ist also 0 oder kleiner. Eine Überschreitung erscheint negativ, zum Beispiel **−120**.
- **Orange hinterlegte Zeile mit brauner Schrift:** Der Alarm ist bald fällig. Es sind nur noch höchstens 10 % des Intervalls übrig, bei 3.000 km also höchstens 300 km.
- **Graue Schrift ohne Hintergrund:** Der Alarm ist ausgeschaltet oder gehört zu einem ausgemusterten Rad.
- Eine Zeile ohne Farbe ist eingeschaltet und noch nicht bald fällig.
- Markieren Sie eine rote oder orange Zeile, wird ihre Farbe kräftiger.
- Gibt es keine Alarme, steht dort **Keine Alarme eingerichtet.** Ist oben ein Rad gewählt, das keine Alarme hat, steht dort **Keine Alarme für dieses Rad.**

Erfassen Sie die passende Wartung, beginnt der Alarm von selbst neu zu zählen. Sie müssen ihn nicht neu setzen.

Mit dieser Anleitung richten Sie einen Alarm ein.

1. Wählen Sie oben unter **Equipment:** das Rad. Es ist dann im Alarmfenster vorgewählt.
2. Klicken Sie in der Gruppe **Alarme** auf **Neuer Alarm…**.
3. Wählen Sie im Fenster **Neuer Alarm** das **Rad:** und die **Tätigkeit:**.
4. Tragen Sie bei **Alle (km):** das Intervall ein, zum Beispiel 3000.
5. Lassen Sie **Alarm eingeschaltet** angehakt.
6. Klicken Sie auf **OK**.

**Aufbau des Fensters „Neuer Alarm“:** Untereinander stehen die Auswahllisten **Rad:** und **Tätigkeit:** und das Feld **Alle (km):**. Darunter folgt das Kontrollkästchen **Alarm eingeschaltet**. Unten rechts stehen **OK** und **Abbrechen**. Solange ein Pflichtfeld leer oder ungültig ist, hat es einen roten Rand, und **OK** ist ausgegraut. Ein dunkelroter Hinweis über den Schaltflächen erklärt Sonderfälle, zum Beispiel eine leere Liste **Tätigkeit** oder einen schon vorhandenen Alarm. Das Fenster **Alarm bearbeiten** sieht genauso aus.

Weitere Befehle:

- **Bearbeiten…** oder Doppelklick öffnet das Fenster **Alarm bearbeiten**.
- **Löschen** oder **Entf** löscht die markierten Alarme nach einer Rückfrage.
- **Ein-/Ausschalten** im Rechtsklickmenü schaltet die markierten Alarme aus oder wieder ein. Ein ausgeschalteter Alarm wird nicht fällig und beim Start nicht gemeldet.

**Hinweis:** Für dasselbe Rad und dieselbe Tätigkeit gibt es nur einen Alarm. Das Intervall muss eine ganze Zahl größer als 0 sein.

**Hinweis:** Alarme ausgemusterter Räder werden nie fällig und beim Start nicht gemeldet.

#### Wartungen

Die Tabelle **Wartungen** zeigt alle Wartungen, die neueste oben. Die Spalten stehen von links nach rechts in der Reihenfolge **Datum**, **Rad**, **km-Stand**, **km seit letzter**, **Tage seit letzter**, **Tätigkeit** und **Kommentar**. Sie bedeuten:

- **Datum**, **Rad**, **Tätigkeit**, **Kommentar**
- **km-Stand:** Kilometerstand des Rads an diesem Tag. Das ist die Summe aller Touren mit diesem Rad bis zu diesem Tag plus seine Anfangskilometer.
- **km seit letzter:** Kilometer seit der vorherigen Wartung mit derselben Tätigkeit an diesem Rad. So sehen Sie, wie lange zum Beispiel eine Kette gehalten hat.
- **Tage seit letzter:** Kalendertage seit dieser vorherigen Wartung.

Bei der ersten Wartung einer Art zählen **km seit letzter** und **Tage seit letzter** ab der Anschaffung des Rads. Ohne Anschaffungsdatum zählen die Tage ab der ersten Tour mit diesem Rad.

Ein Klick auf eine Spaltenüberschrift sortiert, ein zweiter Klick kehrt die Reihenfolge um. Gibt es noch keine Wartung, steht dort **Noch keine Wartungen erfasst.** Ist oben ein Rad ohne Wartungen gewählt, steht dort **Keine Wartungen für dieses Rad.**

Mit dieser Anleitung erfassen Sie eine Wartung.

1. Wählen Sie oben unter **Equipment:** das Rad. Es ist dann vorgewählt.
2. Klicken Sie auf **Neue Wartung…**.
3. Im Fenster **Neue Wartung** ist das **Datum:** mit heute vorbelegt. Ändern Sie es bei Bedarf.
4. Wählen Sie **Rad:** und **Tätigkeit:**. Die häufigsten Tätigkeiten stehen oben.
5. Schreiben Sie bei Bedarf einen **Kommentar:**.
6. Klicken Sie auf **OK**.

Die Wartung erscheint sofort in der Tabelle. Ein Alarm mit derselben Tätigkeit an diesem Rad zählt ab jetzt neu.

**Aufbau des Fensters „Neue Wartung“:** Untereinander stehen das Feld **Datum:** und die Auswahllisten **Rad:** und **Tätigkeit:**. Darunter folgt das mehrzeilige Feld **Kommentar:**. Unten rechts stehen **OK** und **Abbrechen**. Solange ein Pflichtfeld leer oder ungültig ist, hat es einen roten Rand, und **OK** ist ausgegraut. Ein dunkelroter Hinweis über den Schaltflächen erklärt Sonderfälle. Das Fenster **Wartung bearbeiten** sieht genauso aus.

Weitere Befehle:

- **Bearbeiten…** oder Doppelklick öffnet das Fenster **Wartung bearbeiten**.
- **Löschen** oder **Entf** löscht die markierten Wartungen nach einer Rückfrage mit der Anzahl.

**Hinweis:** Tätigkeiten wählen Sie nur aus der Liste **Tätigkeit**. Neue Tätigkeiten legen Sie unter **Extras → Einstellungen…** auf der Karteikarte **Auswahllisten** an. Ist die Liste leer, weist das Wartungsfenster darauf hin.

**Hinweis:** Ein Datum in der Zukunft ist nicht erlaubt. Es erscheint **Das Datum liegt in der Zukunft.** Gibt es für dasselbe Rad am selben Tag schon dieselbe Tätigkeit, speichert AndyTours nicht und nennt die vorhandene Wartung.

<a id="k4-8"></a>
### 4.8 Menü „Tourenplanung“

#### Tour planen

Im Fenster **Tour planen** klicken Sie eine neue Strecke auf der Karte zusammen. AndyTours führt die Strecke automatisch über Wege, die zum gewählten Fahrprofil passen. Dafür fragt AndyTours einen Routingdienst im Internet. Sie brauchen also eine Internetverbindung.

**Aufbau:** Oben steht eine Leiste. Links enthält sie **Fahrprofil:** mit der Auswahl des Profils und die Schaltflächen **Öffnen…**, **Speichern…**, **Auf Garmin übertragen…**, **Letzten Punkt entfernen**, **Alles löschen** und **Overlays…**. Rechts in der Leiste stehen halbfett die Länge, zum Beispiel **42,7 km**, und daneben die Höhenmeter, zum Beispiel **↑ 380 hm  ↓ 375 hm**. Solange der Routingdienst rechnet, steht davor grau **Strecke wird ermittelt …**. Darunter füllt die Karte das Fenster. Am unteren Rand steht grau eine Bedienhilfe: **Klick in die Karte: Wegpunkt anhängen · Wegpunkt ziehen: verschieben · Linie ziehen: Zwischenpunkt einfügen · Rechtsklick auf einen Wegpunkt: entfernen · Karte ziehen: verschieben**. Darunter erscheinen bei Bedarf rote Hinweise.

Auf der Karte erkennen Sie die Planung so:

- Die ermittelte Strecke ist eine blaue Linie.
- Der Startpunkt ist ein grüner Kreis, der Zielpunkt ein roter Kreis, jeweils mit weißem Rand. Der Zielpunkt ist immer der letzte Wegpunkt der Strecke.
- Wegpunkte dazwischen sind kleinere weiße Kreise mit blauem Rand.
- Abbiegepunkte sind kleine orange Punkte mit weißem Rand.
- Ein Teilstück, das der Routingdienst nicht geliefert hat, ist eine graue gestrichelte gerade Linie.

Mit dieser Anleitung planen Sie eine Tour.

1. Wählen Sie **Tourenplanung → Tour planen…**.
2. Die Karte zeigt den zuletzt geplanten Ausschnitt. Beim ersten Mal zeigt sie die Gegend Ihrer letzten Tour.
3. Wählen Sie unter **Fahrprofil:** eines dieser Profile:
   - **Rennrad (nur Straße)**: nur asphaltierte Straßen und Radwege, soweit möglich
   - **Trekking/Tourenrad**: Straßen, Radwege und gut befahrbare Schotterwege
   - **Mountainbike (bevorzugt offroad)**: bevorzugt Feld-, Wald- und Schotterwege
4. Klicken Sie in die Karte, um den Startpunkt zu setzen.
5. Klicken Sie weitere Punkte an. Jeder Klick hängt einen Wegpunkt am Ende an.
6. Oben rechts sehen Sie sofort die Länge und die Höhenmeter bergauf (↑) und bergab (↓).
7. Klicken Sie auf **Speichern…**, wählen Sie Ordner und Dateinamen und klicken Sie auf **Speichern**.

So ändern Sie die Strecke:

- **Wegpunkt verschieben:** Ziehen Sie den Wegpunkt mit der Maus.
- **Zwischenpunkt einfügen:** Ziehen Sie die Linie zwischen zwei Wegpunkten an eine andere Stelle.
- **Wegpunkt entfernen:** Klicken Sie mit der rechten Maustaste auf den Wegpunkt.
- **Letzten Punkt entfernen:** entfernt den zuletzt gesetzten Wegpunkt.
- **Alles löschen:** leert nach der Rückfrage **Alle Wegpunkte der Planung löschen?** die ganze Planung.
- **Karte verschieben:** Ziehen Sie die Karte. Dabei entsteht kein Wegpunkt.

Wechseln Sie das Fahrprofil, ermittelt AndyTours die ganze Strecke neu. Die Wegpunkte bleiben an ihrer Stelle.

An jeder Abzweigung, an der die Strecke abbiegt, setzt AndyTours automatisch einen Abbiegepunkt mit der Richtung, zum Beispiel „links“ oder „Kreisverkehr 2. Ausfahrt“. Die Abbiegepunkte erscheinen als kleine orange Punkte auf der Karte und werden mitgespeichert.

**Hinweis:** Kann der Routingdienst ein Teilstück nicht liefern, zum Beispiel ohne Internet, zeichnet AndyTours dieses Teilstück als graue gestrichelte gerade Linie. Ein roter Hinweis erscheint am unteren Rand. Ein solches Teilstück hat keine Abbiegepunkte.

**Planung speichern und öffnen**

- **Speichern…** speichert die Planung als GPX-Datei. Vorgeschlagen werden der zuletzt verwendete Ordner und der Name **Planung JJJJ-MM-TT**. Eine Planung braucht mindestens zwei Wegpunkte.
- **Öffnen…** lädt eine mit AndyTours gespeicherte Planung. Wegpunkte, Strecke und Fahrprofil erscheinen wieder und lassen sich weiter bearbeiten. Die Titelleiste zeigt dann den Dateinamen.
- Schließen Sie das Fenster oder öffnen Sie eine andere Planung, obwohl Änderungen nicht gespeichert sind, fragt AndyTours **Die Planung hat ungespeicherte Änderungen. Änderungen verwerfen?**.

**Planung auf den Edge übertragen**

1. Schließen Sie den Edge per USB an.
2. Klicken Sie auf **Auf Garmin übertragen…**.
3. Prüfen Sie den Namen des Course. Vorgeschlagen wird der Dateiname der Planung oder **Planung JJJJ-MM-TT**.
4. Klicken Sie auf **Übertragen**.

Der Edge kündigt beim Nachfahren die Abbiegungen mit ihrer Richtung an.

**Overlays…** öffnet die Overlay-Verwaltung.

#### Overlays

Ein Overlay ist eine gespeicherte Strecke, die zusätzlich auf den Karten erscheint. Das kann eine geplante Tour oder eine fremde GPX-Strecke sein. So sehen Sie beim Planen, wo Sie schon gefahren sind oder was Sie sich vorgemerkt haben. Eingeschaltete Overlays erscheinen auf den Karten im Tourdetail, in der Mehrtagestour und in der Tourenplanung.

Öffnen Sie die Verwaltung mit **Tourenplanung → Overlays…** oder mit der Schaltfläche **Overlays…** neben einer Karte.

**Aufbau:** Das Fenster heißt **Overlays**. Oben stehen die Schaltflächen **Ordner anlegen**, **GPX hinzufügen…**, **Verschieben nach…** und **Löschen**. Darunter folgt der Baum mit Ordnern und Overlays. Jede Zeile beginnt mit einem Kontrollkästchen. Bei einem Overlay folgt ein kurzer farbiger Balken in der Farbe, in der es auf den Karten erscheint. Bei einem Ordner folgt ein Ordnersymbol. Danach steht der Name. Unten im Fenster stehen das Feld **Name:** mit der Schaltfläche **Übernehmen** und darunter die Auswahl **Farbe:**. Ganz unten erklärt eine graue Zeile die Bedienung. Gibt es noch keine Overlays, steht unter dem Baum ein grauer Hinweis.

Das Kontrollkästchen eines Ordners zeigt ein Häkchen, wenn sein ganzer Inhalt eingeblendet ist, und ist leer, wenn nichts davon eingeblendet ist. Ist nur ein Teil eingeblendet, zeigt es den Zwischenzustand: ein blau gefülltes Kästchen mit einem waagrechten Strich, wie im Fenster **Kategorien zuordnen**.

Mit dieser Anleitung nehmen Sie GPX-Dateien als Overlays auf.

1. Markieren Sie im Baum den Ordner, in den die Overlays sollen. Ohne Markierung landen sie auf oberster Ebene.
2. Klicken Sie auf **GPX hinzufügen…**.
3. Wählen Sie eine oder mehrere GPX-Dateien und klicken Sie auf **Öffnen**.

Die Overlays erscheinen eingeschaltet und tragen den Dateinamen ohne Endung. AndyTours übernimmt die Strecke. Sie können die ursprüngliche Datei danach verschieben oder löschen. Dateien ohne Strecke werden nicht aufgenommen. Ein roter Hinweis nennt sie.

Weitere Bedienung:

- **Einblenden:** Setzen oder entfernen Sie das Häkchen vor einem Overlay. Das Häkchen vor einem Ordner schaltet seinen ganzen Inhalt ein oder aus. Offene Karten zeigen die Änderung sofort.
- **Ganze Strecke zeigen:** Doppelklicken Sie auf ein Overlay. Die offenen Karten zeigen seine ganze Strecke.
- **Ordner anlegen:** legt den Ordner **Neuer Ordner** im markierten Ordner an.
- **Umbenennen:** Markieren Sie Ordner oder Overlay, ändern Sie den Text bei **Name:** und klicken Sie auf **Übernehmen** oder drücken Sie die Eingabetaste. Das Fenster schließt sich danach.
- **Verschieben nach…:** Wählen Sie im Fenster **Verschieben nach** den Zielordner oder **(oberste Ebene)**.
- **Löschen:** löscht das markierte Overlay oder den Ordner nach einer Rückfrage. Ein Ordner wird mit seinem gesamten Inhalt gelöscht.
- **Farbe:** Markieren Sie ein Overlay und wählen Sie eine eigene Farbe. **Gemeinsame Farbe (Violett)** setzt die eigene Farbe zurück.

Dieselben Befehle stehen im Rechtsklickmenü des Baums. Jede Änderung ist sofort gespeichert. Nach einem Neustart sind dieselben Overlays eingeschaltet.

**Hinweis:** Eine gespeicherte Planung aus **Tour planen** nehmen Sie wie jede andere GPX-Datei als Overlay auf. Eine gefahrene Tour exportieren Sie dafür zuerst mit **Als GPX exportieren…**.

<a id="k4-9"></a>
### 4.9 Menü „Extras“

<a id="k4-9-1"></a>
#### 4.9.1 Persönliche Daten

Unter **Extras → Persönliche Daten…** pflegen Sie Ihre persönlichen Angaben: **Vorname:**, **Nachname:**, **Geburtstag:**, **Größe:** (cm), **Gewicht:** (kg), **Ruhepuls:** und **Maximalpuls:** (Schläge/min) sowie **Heimatort:**. AndyTours speichert diese Angaben derzeit nur. Berechnungen und Karten verwenden sie noch nicht.

1. Wählen Sie **Extras → Persönliche Daten…**.
2. Ändern Sie die Werte. Ein leeres Feld bedeutet „unbekannt“.
3. Klicken Sie auf **OK**.

Erlaubte Werte:

- Geburtstag als TT.MM.JJJJ, nicht in der Zukunft
- Größe 100 bis 250 cm, ganze Zahl
- Gewicht 30 bis 250 kg, höchstens eine Nachkommastelle, zum Beispiel 78,5
- Ruhepuls 30 bis 120, Maximalpuls 100 bis 230, jeweils ganze Zahl
- Der Ruhepuls muss kleiner als der Maximalpuls sein.

Bei einer ungültigen Eingabe nennt ein roter Hinweis den erlaubten Bereich. **OK** ist dann ausgegraut.

**Aufbau:** Das Fenster **Persönliche Daten** zeigt die Felder untereinander, von **Vorname:** bis **Heimatort:**. Darunter erscheint bei Bedarf der rote Hinweis. Unten rechts stehen **OK** und **Abbrechen**. **Abbrechen** schließt das Fenster ohne Rückfrage und ohne zu speichern.

<a id="k4-9-2"></a>
#### 4.9.2 Saisonziele

Ein Saisonziel ist Ihr Jahresziel für eine Aktivität: Kilometer, Höhenmeter und Anzahl der Einheiten. Die Karteikarte **Aktuell** vergleicht Ihren Stand mit diesen Zielen.

1. Wählen Sie **Extras → Saisonziele…**.
2. Wählen Sie unter **Jahr:** das laufende oder das folgende Jahr.
3. Tragen Sie für jede Aktivität **Kilometer**, **Höhenmeter** und **Anzahl** ein. Ein leeres Feld oder 0 bedeutet „kein Ziel“.
4. Klicken Sie auf **OK**.

Ein Ziel gilt auch für die folgenden Jahre, bis Sie es dort ändern. Frühere Jahre behalten ihre damaligen Ziele. Die Spalte **Herkunft** zeigt, woher ein Ziel stammt, zum Beispiel **übernommen aus 2025** oder **übernommen aus BikeXperience**. Bei einem eigenen Ziel für das gewählte Jahr bleibt sie leer.

Erlaubt sind ganze Zahlen: Kilometer 0 bis 100.000, Höhenmeter 0 bis 2.000.000, Anzahl 0 bis 1.000. Bei einer ungültigen Eingabe nennt ein roter Hinweis unter der Tabelle Aktivität, Feld und erlaubten Bereich. **OK** und die Auswahl **Jahr:** sind dann ausgegraut.

**Aufbau:** Oben im Fenster **Saisonziele** steht die Auswahl **Jahr:**, rechts daneben grau der Satz **Ein Ziel gilt auch für die folgenden Jahre, bis es dort geändert wird. Leer = kein Ziel.** Darunter folgt die Tabelle mit den Spalten **Aktivität**, **Kilometer**, **Höhenmeter**, **Anzahl** und **Herkunft**, eine Zeile je Aktivität. Die Spalte **Herkunft** ist grau. Unten rechts stehen **OK** und **Abbrechen**. **OK** speichert die Ziele beider Jahre. **Abbrechen** schließt ohne Rückfrage und ändert nichts.

<a id="k4-9-3"></a>
#### 4.9.3 Equipment-Einsatz

**Extras → Equipment-Einsatz…** zeigt, wie viel Sie mit jedem Equipment gefahren sind. Es zählen alle Einheiten, unabhängig von der Auswahl im Hauptfenster.

Spalten:

- **Equipment**
- **vorher (km):** die Anfangskilometer des Equipments
- **Gesamt (km):** alle Touren mit diesem Equipment plus die Anfangskilometer. Das entspricht dem km-Stand des Rads auf der Karteikarte **Wartung**.
- **Gesamt (hm):** alle Höhenmeter mit diesem Equipment

Zuerst stehen die Equipments im Einsatz, dann die ausgemusterten, jeweils nach **Gesamt (km)** absteigend. Danach folgen **ohne Equipment** und die fette Zeile **Summe**. Leere Zellen bedeuten 0. Ein Klick auf eine Spaltenüberschrift sortiert neu, die Zeile **Summe** bleibt unten.

Schließen Sie das Fenster mit **Schließen** oder **Esc**. Gibt es noch keine Einheiten, steht statt der Tabelle **Keine Einheiten vorhanden.**

<a id="k4-9-4"></a>
#### 4.9.4 Einstellungen

**Extras → Einstellungen…** öffnet das Fenster **Einstellungen** mit vier Karteikarten. Jede gültige Änderung ist sofort gespeichert. Sie schließen das Fenster mit **Schließen**.

**Aufbau:** Oben stehen die Reiter der Karteikarten **Allgemein**, **Datensicherung**, **Tourenplanung** und **Auswahllisten**. Darunter folgt der Inhalt der gewählten Karteikarte. Unten rechts steht immer die Schaltfläche **Schließen**. Ein eigenes **OK** gibt es nicht.

Die Einstellungen gehören zum Datenordner. Wechseln Sie den Datenordner oder stellen Sie eine Sicherung wieder her, gelten die dort gespeicherten Einstellungen.

**Karteikarte „Allgemein“**

Die Karteikarte hat zwei Gruppen untereinander: oben **Datenordner**, darunter **Darstellung**.

Die Gruppe **Datenordner** zeigt in einem Feld den vollständigen Pfad des Datenordners. Rechts neben dem Pfad steht die Schaltfläche **Ändern…**. Den Pfad selbst können Sie nicht eintippen.

Mit dieser Anleitung wechseln Sie den Datenordner, zum Beispiel auf ein NAS.

1. Klicken Sie in der Gruppe **Datenordner** auf **Ändern…**.
2. Wählen Sie im Fenster **Neuen Datenordner für AndyTours wählen** den Ordner.
3. Je nach Ordner fragt AndyTours nach:
   - **Liegen dort noch keine AndyTours-Daten**, fragt AndyTours, ob die bisherigen Daten dorthin kopiert werden sollen. Mit **Ja** kopiert AndyTours alles. Ein Fenster zeigt den Fortschritt mit Anzahl der Dateien und Datenmenge. Mit **Nein** beginnen Sie im neuen Ordner mit einem leeren Datenbestand.
   - **Liegen dort schon AndyTours-Daten**, verwendet AndyTours diese Daten. Bestätigen Sie mit **OK** oder brechen Sie mit **Abbrechen** ab.
4. AndyTours arbeitet sofort mit dem neuen Ordner. Die Statusleiste zeigt ihn an.

Die Daten im bisherigen Ordner bleiben in jedem Fall unverändert. Nach einem Neustart ist der zuletzt gewählte Ordner aktiv.

**Hinweis:** Darf AndyTours im gewählten Ordner nicht schreiben oder schlägt das Kopieren fehl, erscheint eine Meldung. Der bisherige Datenordner bleibt dann aktiv.

In der Gruppe **Darstellung** wählen Sie unter **Größe der Oberfläche:** eine Stufe: 100 %, 110 %, 125 %, 150 %, 175 % oder 200 %. Alle offenen Fenster werden sofort größer oder kleiner, mit Schrift, Feldern und Tabellen. Das Kartenbild selbst bleibt gleich groß. Meldungsfenster von Windows folgen der Einstellung von Windows.

**Karteikarte „Datensicherung“**

AndyTours sichert Ihren Datenbestand automatisch beim Programmstart. Die Sicherung ist immer eingeschaltet.

Die Karteikarte enthält die Gruppe **Datensicherung** mit drei Zeilen untereinander. Ganz unten steht grau **Die Sicherung erfolgt automatisch beim Programmstart.**

- **Sicherungsordner:** Ordner der Sicherungen. Der Pfad steht in einem Feld. Anfangs ist das der Ordner **Sicherungen** im Datenordner. Mit der Schaltfläche **Ändern…** rechts daneben wählen Sie einen anderen.
- **Sichern nach Programmstarts:** Nach wie vielen Programmstarts gesichert wird. Anfangs 3.
- **Höchstens aufbewahren:** Wie viele Sicherungen bleiben. Anfangs 10. Hinter dem Feld steht **Sicherungen (die ältesten werden gelöscht)**.

Erlaubt sind ganze Zahlen von 1 bis 99. Bei einer ungültigen Eingabe erscheint unter dem Feld der rote Hinweis **Bitte eine ganze Zahl von 1 bis 99 eingeben.**

Eine Sicherung ist eine Datei mit Datum und Uhrzeit im Namen, zum Beispiel **andytours_2026-10-23_07-39-12.db**. Andere Dateien im Sicherungsordner bleiben unberührt. Ohne Touren im Datenbestand legt AndyTours keine Sicherung an.

**Achtung:** Die automatische Sicherung umfasst den Datenbestand mit Touren, Rädern, Wartungen und Einstellungen. Die Aufzeichnungsdateien in den Jahresordnern des Datenordners sind nicht darin enthalten. Sichern Sie den ganzen Datenordner deshalb zusätzlich mit Ihrer üblichen Datensicherung, zum Beispiel regelmäßig auf eine externe Festplatte oder ein NAS.

**Karteikarte „Tourenplanung“**

Die Karteikarte hat zwei Gruppen untereinander: oben **Routing**, darunter **Overlays**.

Ein Routingdienst ist ein Internetdienst, der Strecken über Straßen und Wege berechnet. In der Gruppe **Routing** wählen Sie ihn mit einer von zwei Optionen. Unter jeder Option steht leicht eingerückt ihr Eingabefeld:

- **BRouter (kostenlos, ohne Zugangsschlüssel):** voreingestellt. Unter **Serveradresse:** steht anfangs **https://brouter.de/brouter**. Ist die Adresse unvollständig, erscheint darunter rot **Bitte eine vollständige Adresse eingeben, z. B. https://brouter.de/brouter.**
- **openrouteservice (mit Zugangsschlüssel):** Ausweichdienst, falls BRouter nicht erreichbar ist. Tragen Sie unter **Zugangsschlüssel:** Ihren Schlüssel ein. Einen kostenlosen Schlüssel erhalten Sie nach einer Anmeldung auf openrouteservice.org. Der Schlüssel wird nur verdeckt angezeigt, als Reihe von Punkten. Fehlt der Schlüssel, erklärt ein roter Hinweis darunter, dass openrouteservice ohne Schlüssel keine Strecken ermitteln kann.

In der Gruppe **Overlays** wählen Sie unter **Gemeinsame Farbe:** die Farbe aller Overlays ohne eigene Farbe. Die Auswahlliste zeigt vor jedem Farbnamen ein kleines Farbfeld. Zur Wahl stehen zwölf Farben. Voreingestellt ist Violett.

**Karteikarte „Auswahllisten“**

Hier pflegen Sie die festen Werte, die AndyTours in Eingabefeldern anbietet. Unter **Liste:** wählen Sie die Liste:

- **Trainingsbereich:** Kürzel für die Spalte **Bereich**, zum Beispiel „GA1“
- **Aktivität:** Sportarten, zum Beispiel „Bike“
- **Kategorie:** Stichworte für Einheiten, zum Beispiel „Urlaub“
- **Equipment:** Räder und Ausrüstung
- **Tätigkeit:** Arten von Wartungen, zum Beispiel „Kettenwechsel“

Darunter stehen die Einträge der Liste. Ein Klick auf einen Eintrag übernimmt ihn in das Feld **Eintrag:**.

**Aufbau:** Oben steht die Auswahl **Liste:**. Darunter folgt links die Liste der Einträge. Rechts daneben stehen untereinander die Schaltflächen **Nach oben**, **Nach unten** und **Löschen**. Je nach Liste kommen darunter weitere Schaltflächen hinzu: **Als Hauptaktivität** (nur bei **Aktivität**), **Standard** (nur bei **Kategorie**), **Zusammenführen mit…** (bei **Aktivität** und **Kategorie**) und **Bearbeiten…** (nur bei **Equipment**). Unter der Liste stehen das Feld **Eintrag:** und rechts daneben die Schaltflächen **Hinzufügen** und **Umbenennen**. Ganz unten erscheint bei Bedarf ein roter Hinweis.

Einträge bearbeiten (alle Listen außer **Equipment**):

- **Hinzufügen:** Tippen Sie den Namen in **Eintrag:** und klicken Sie auf **Hinzufügen**. Der Eintrag erscheint am Ende der Liste.
- **Umbenennen:** Markieren Sie den Eintrag, ändern Sie den Text in **Eintrag:** und klicken Sie auf **Umbenennen**. Kommt ein Trainingsbereich in Einheiten oder eine Tätigkeit in Wartungen vor, fragt AndyTours mit der Anzahl, zum Beispiel **In 714 Einheiten ‚GA2‘ durch ‚GA 2‘ ersetzen?**.
- **Löschen:** Markieren Sie den Eintrag und klicken Sie auf **Löschen**. Ein Eintrag, der noch verwendet wird, lässt sich nicht löschen. Ein roter Hinweis nennt dann die Anzahl der Verwendungen.
- **Nach oben** und **Nach unten** ändern die Reihenfolge. Bei **Kategorie** und **Equipment** sind die beiden Schaltflächen ausgegraut, denn diese Listen stehen alphabetisch.

Leere oder doppelte Namen weist AndyTours mit einem roten Hinweis ab. Groß- und Kleinschreibung zählen dabei nicht.

Zusätzlich bei **Aktivität**:

- **Als Hauptaktivität** macht die markierte Aktivität zur Hauptaktivität. Sie trägt den Zusatz **(Hauptaktivität)**. Sie ist beim Start vorgewählt und gilt bei **alle Aktivitäten** für neue Einheiten und die Karteikarte **Wartung**.
- Die Hauptaktivität und die letzte verbleibende Aktivität lassen sich nicht löschen.

Zusätzlich bei **Kategorie**:

- **Standard** schaltet um, ob die markierte Kategorie eine Standardkategorie ist. Standardkategorien tragen den Zusatz **(Standard)** und sind bei neuen Einheiten angehakt.

Zusammenführen (bei **Aktivität** und **Kategorie**):

Mit dieser Anleitung führen Sie doppelte Einträge wie „Trainieren“ und „Training“ zusammen.

1. Markieren Sie den Eintrag, der verschwinden soll.
2. Klicken Sie auf **Zusammenführen mit…**.
3. Wählen Sie im Fenster **Zusammenführen mit** den Eintrag, der bleiben soll, und klicken Sie auf **OK**.
4. Bestätigen Sie die Rückfrage, zum Beispiel **42 Einheiten von ‚Trainieren‘ nach ‚Training‘ übernehmen und ‚Trainieren‘ entfernen?**, mit **Ja**.

Alle Einheiten des ersten Eintrags gehören danach zum zweiten. Der erste Eintrag ist entfernt.

Liste **Equipment**:

Equipments stehen alphabetisch mit ihrer Aktivität. Ausgemusterte stehen am Ende mit dem Zusatz **(ausgemustert)**, Standard-Equipments tragen den Zusatz **(Standard)**. Bei dieser Liste fehlen das Feld **Eintrag:** und die Schaltfläche **Umbenennen**. Name und alle übrigen Angaben ändern Sie im Fenster des Equipments.

Mit dieser Anleitung legen Sie ein neues Rad an.

1. Wählen Sie unter **Liste:** den Eintrag **Equipment**.
2. Klicken Sie auf **Hinzufügen**.
3. Füllen Sie im Fenster **Neues Equipment** die Felder aus:
   - **Name:** Name des Rads
   - **Aktivität:** zu welcher Aktivität es gehört; **(keine)** passt zu jeder Aktivität
   - **Standardkategorie:** wird bei Einheiten mit diesem Rad zusätzlich angehakt; **(keine)** ist möglich
   - **Anschaffungsdatum:** als TT.MM.JJJJ
   - **Anfangskilometer:** Kilometer, die das Rad vor AndyTours schon gefahren ist
4. Haken Sie **Standard für diese Aktivität** an, wenn das Rad bei neuen Einheiten vorbelegt sein soll. Pro Aktivität gibt es nur ein Standard-Equipment. Das Kontrollkästchen ist nur wählbar, wenn unter **Aktivität:** eine Aktivität gewählt ist.
5. Klicken Sie auf **OK**.

Ein vorhandenes Equipment ändern Sie mit **Bearbeiten…** oder einem Doppelklick. Das Fenster heißt dann **Equipment bearbeiten**.

**Aufbau der Fenster „Neues Equipment“ und „Equipment bearbeiten“:** Oben stehen untereinander die Felder **Name:**, **Aktivität:**, **Standardkategorie:**, **Anschaffungsdatum:** und **Anfangskilometer:**. Darunter folgen die Kontrollkästchen **Standard für diese Aktivität** und **ausgemustert**. Danach kommt die Liste **Gerätezuordnung (beim Import gemerkt):** mit der Schaltfläche **Entfernen** rechts daneben. Bei einer ungültigen Eingabe erscheint darunter ein roter Hinweis. Unten rechts stehen **OK** und **Abbrechen**. **OK** ist nur bei gültigen Eingaben wählbar. **Abbrechen** schließt ohne Rückfrage und ohne zu speichern.

- **ausgemustert:** Haken Sie dies an, wenn Sie ein Rad nicht mehr fahren. Es wird dann für neue Einheiten, Wartungen und Alarme nicht mehr angeboten. Alte Einheiten und Wartungen behalten das Rad. Es trägt überall den Zusatz **(ausgemustert)**. Ein ausgemustertes Rad kann kein Standard-Equipment sein: **Standard für diese Aktivität** ist dann ausgegraut, und ein vorher gesetzter Haken entfällt.
- **Gerätezuordnung (beim Import gemerkt):** zeigt die Rad-Kennungen und Sensoren, die AndyTours beim Garmin-Import diesem Rad zugeordnet hat. Jede Zeile beginnt mit ihrer Art, zum Beispiel **Rad:** für eine Rad-Kennung oder **Sensor:** für einen Sensor. Aus BikeXperience übernommene Werte beginnen mit **Altwert:**. Mit **Entfernen** löschen Sie einen markierten Eintrag.

**Hinweis:** Ein Equipment, das in Einheiten verwendet wird oder Wartungen hat, lässt sich nicht löschen. Mustern Sie es stattdessen aus.

<a id="k4-9-5"></a>
#### 4.9.5 Sicherung wiederherstellen

Mit dieser Anleitung setzen Sie den Datenbestand auf den Stand einer Sicherung zurück, etwa nach einem Bedienfehler.

1. Wählen Sie **Extras → Sicherung wiederherstellen…**.
2. Das Fenster **Sicherung wiederherstellen** zeigt alle Sicherungen des Sicherungsordners mit Wochentag, Datum und Uhrzeit. Die neueste steht oben und ist vorgewählt.
3. Markieren Sie die gewünschte Sicherung und klicken Sie auf **Wiederherstellen…**.
4. AndyTours fragt, ob der Datenbestand auf diesen Stand zurückgesetzt werden soll. Alle späteren Änderungen gehen dabei verloren.
5. Bestätigen Sie mit **Ja**.

AndyTours legt vorher eine Sicherung des aktuellen Stands an. Damit lässt sich auch das Wiederherstellen rückgängig machen. Danach zeigt das Hauptfenster sofort den wiederhergestellten Stand, ohne Neustart. Die Einstellungen der Datensicherung bleiben unverändert.

**Aufbau:** Oben im Fenster **Sicherung wiederherstellen** steht **Wählen Sie die Sicherung, auf deren Stand der Datenbestand zurückgesetzt werden soll:**, darunter grau der Pfad des Sicherungsordners. Darunter folgt die Liste der Sicherungen, eine Zeile je Sicherung, zum Beispiel **Freitag, 23.10.2026, 18:58:22 Uhr**. Unten rechts stehen **Wiederherstellen…** und **Abbrechen**. Ein Doppelklick auf eine Zeile wirkt wie **Wiederherstellen…**.

**Hinweis:** Eine Sicherung aus einer älteren AndyTours-Version wird übernommen. Eine Sicherung aus einer neueren Version lehnt AndyTours ab. Eine beschädigte Sicherung lehnt AndyTours ebenfalls ab. Der Datenbestand bleibt dann unverändert.

<a id="k4-10"></a>
### 4.10 Menü „Hilfe“

**Hilfe → Über AndyTours** öffnet ein kleines Fenster mit dem Programmicon, dem Namen, der Versionsnummer und dem Autor. Sie schließen es mit **OK**, **Enter** oder **Esc**.

**Aufbau:** Links im Fenster **Über AndyTours** steht groß das Programmicon, ein Fahrrad vor Bergen. Rechts daneben stehen untereinander **AndyTours**, die Versionsnummer, der Autor und eine kurze Beschreibung des Programms. Unten rechts steht **OK**.

---

<a id="kapitel-5"></a>
## 5. Häufige Fragen und Probleme

Finden Sie hier keine Lösung, melden Sie das Problem auf der GitHub-Seite des Projekts unter **Issues**: <https://github.com/AndySeeon/AndyTours/issues>. Beschreiben Sie, was Sie getan haben und was passiert ist. Geben Sie eine Fehlermeldung im Wortlaut an.

**Beim Start erscheint „AndyTours – Datenordner nicht erreichbar“.**
Der gespeicherte Datenordner ist nicht erreichbar, zum Beispiel weil das NAS ausgeschaltet oder das Laufwerk getrennt ist. Das Fenster nennt oben den Pfad des Ordners. Unten rechts stehen die Schaltflächen **Erneut versuchen**, **Anderen Ordner wählen…** und **Beenden**. Sie haben drei Möglichkeiten:

1. Schalten Sie das NAS ein oder verbinden Sie das Laufwerk. Klicken Sie dann auf **Erneut versuchen**.
2. Klicken Sie auf **Anderen Ordner wählen…**, um mit einem anderen Datenordner zu arbeiten.
3. Klicken Sie auf **Beenden**. Das Schließen des Fensters über das X oben rechts wirkt ebenso.

**Beim Import erscheint „Kein Garmin-Gerät gefunden. Bitte den Edge per USB anschließen.“**
Prüfen Sie, ob der Edge eingeschaltet und per USB-Kabel verbunden ist. Warten Sie, bis Windows das Gerät erkannt hat, und versuchen Sie es erneut. Ohne Gerät können Sie FIT-Dateien mit **Datei → FIT-Dateien importieren…** übernehmen.

**Beim Import erscheint „Keine neuen Touren gefunden.“**
Alle Touren auf dem Gerät sind schon in AndyTours. AndyTours erkennt das an Datum und Startzeit.

**Der Import schlägt das falsche Rad vor.**
Wählen Sie im Fenster **Garmin-Import** das richtige Equipment. AndyTours merkt sich danach Rad-Kennung und Sensoren für dieses Rad. Falsche Zuordnungen entfernen Sie im Fenster **Equipment bearbeiten** unter **Gerätezuordnung (beim Import gemerkt):**.

**Ich habe eine Einheit gespeichert, sehe sie aber nicht in der Tabelle.**
Die Einheit passt nicht zur aktuellen Auswahl oder zum Filter. Die Statusleiste meldet dann **Gespeichert – liegt außerhalb der aktuellen Auswahl.** Wählen Sie das passende Jahr und die passende Aktivität oder klicken Sie auf **Filter zurücksetzen**.

**Die Tabelle zeigt „Keine Einheiten passen zum Filter.“**
Ein Filter oder ein Suchbegriff ist gesetzt. Klicken Sie auf **Filter zurücksetzen**.

**„Tourdetail…“ ist nicht wählbar.**
Markieren Sie genau eine Einheit mit Aufzeichnung oder mehrere Einheiten. Eine einzelne Einheit ohne Aufzeichnung hat kein Tourdetail.

**Das Tourdetail meldet, dass die Aufzeichnung nicht gefunden wurde.**
Die Aufzeichnungsdatei fehlt im Datenordner, zum Beispiel **Die Aufzeichnung ‚2019/2019-11-11-06-25-39.fit‘ wurde im Datenordner nicht gefunden.** Prüfen Sie, ob die Datei verschoben oder gelöscht wurde, und legen Sie sie wieder an diese Stelle.

**Die Karte bleibt grau oder meldet „Die Karte konnte nicht geladen werden. Besteht eine Internetverbindung?“**
Für neue Kartenausschnitte braucht AndyTours eine Internetverbindung. Bereits angesehene Ausschnitte erscheinen auch ohne Internet.

**Die geplante Strecke ist eine gerade Linie.**
Der Routingdienst hat die Strecke nicht geliefert. Prüfen Sie die Internetverbindung. Steht der Hinweis auf eine unvollständige Einstellung da, prüfen Sie unter **Extras → Einstellungen…** auf der Karteikarte **Tourenplanung** die **Serveradresse:** oder den **Zugangsschlüssel:**. Ist BRouter nicht erreichbar, wählen Sie **openrouteservice (mit Zugangsschlüssel)**.

**Beim Start erscheint „Die automatische Datensicherung ist fehlgeschlagen.“**
Der Sicherungsordner ist nicht erreichbar, nicht beschreibbar oder der Datenträger ist voll. AndyTours startet trotzdem und versucht es beim nächsten Start erneut. Wählen Sie bei Bedarf unter **Extras → Einstellungen…** auf der Karteikarte **Datensicherung** einen anderen Sicherungsordner.

**Ein Fenster mit Alarmen erscheint bei jedem Start.**
Ein Wartungsalarm ist fällig. Erfassen Sie auf der Karteikarte **Wartung** die passende Wartung. Der Alarm zählt dann neu. Möchten Sie nicht erinnert werden, schalten Sie den Alarm mit **Ein-/Ausschalten** aus.

**Ich kann ein Rad, eine Aktivität oder eine Kategorie nicht löschen.**
Der Eintrag wird noch verwendet. Ein roter Hinweis nennt die Anzahl. Ein Rad mustern Sie stattdessen aus. Aktivitäten und Kategorien führen Sie mit **Zusammenführen mit…** mit einem anderen Eintrag zusammen.

**Bei der Wartung fehlt ein Rad in der Auswahl.**
Die Auswahl zeigt nur Equipments der Aktivität in der Werkzeugleiste und keine ausgemusterten. Wählen Sie die passende Aktivität. Prüfen Sie, ob das Rad ausgemustert ist oder einer anderen Aktivität zugeordnet ist.

**Die Schrift ist zu klein.**
Wählen Sie unter **Extras → Einstellungen…** auf der Karteikarte **Allgemein** eine größere **Größe der Oberfläche:**.

**Wie lege ich meine Daten auf das NAS?**
Wechseln Sie den Datenordner unter **Extras → Einstellungen…** auf der Karteikarte **Allgemein** mit **Ändern…** und lassen Sie die Daten kopieren (siehe [Abschnitt 4.9.4](#k4-9-4)).

**Wie mache ich einen Fehler rückgängig?**
Es gibt keine Rückgängig-Funktion. Sie können aber eine Sicherung wiederherstellen (siehe [Abschnitt 4.9.5](#k4-9-5)). Alle Änderungen nach dieser Sicherung gehen dabei verloren.

---

<a id="kapitel-6"></a>
## 6. Glossar (Stichwortverzeichnis)

| Begriff | Erklärung | Siehe |
|---|---|---|
| Abbiegepunkt | Automatisch gesetzte Markierung in einer Planung, an der die Strecke abbiegt, mit Richtung wie „links“. Der Edge kündigt sie beim Nachfahren an. | [4.8](#k4-8) |
| Aktivität | Sportart einer Einheit, zum Beispiel „Bike“. Wird in der Werkzeugleiste gewählt. | [4.1](#k4-1), [4.9.4](#k4-9-4) |
| Anfangskilometer | Kilometer, die ein Rad vor AndyTours schon gefahren ist. Zählen zum km-Stand. | [4.9.4](#k4-9-4) |
| Aufzeichnung | Datei des Radcomputers mit dem Verlauf einer Tour (Strecke, Höhe, Puls usw.). Liegt im Datenordner im Ordner des Jahres. | [4.2](#k4-2), [4.6](#k4-6) |
| ausgemustert | Kennzeichen für ein Equipment, das nicht mehr verwendet wird. Es wird nicht mehr angeboten, bleibt aber bei alten Einträgen erhalten. | [4.9.4](#k4-9-4) |
| Bestwert | Höchster Wert der angezeigten Einheiten in einer Spalte. Rot dargestellt. | [4.4](#k4-4) |
| BikeXperience | Vorgängerprogramm, dessen Daten AndyTours übernehmen kann. | [2.3](#kapitel-2) |
| BRouter | Kostenloser Routingdienst, in AndyTours voreingestellt. | [4.9.4](#k4-9-4) |
| Course | Strecke, die der Garmin Edge beim Nachfahren mit Höhenprofil und Abbiegehinweisen anzeigt. | [4.6](#k4-6), [4.8](#k4-8) |
| Datenordner | Ordner, in dem alle Daten von AndyTours liegen: Datenbestand, Aufzeichnungen, Sicherungen. | [4.9.4](#k4-9-4) |
| Edge | Radcomputer von Garmin. AndyTours unterstützt Edge 1040, Edge 1000 und Edge 800. | [4.2](#k4-2) |
| Einheit, Trainingseinheit | Eine Tour bzw. ein Training in AndyTours. | [4.4](#k4-4), [4.5](#k4-5) |
| Equipment | Rad oder anderes Ausrüstungsteil, mit dem eine Einheit gefahren wurde. | [4.9.4](#k4-9-4) |
| Etappe | Eine Einheit innerhalb einer Mehrtagestour. | [4.6](#k4-6) |
| Fahrprofil | Art des Rads für die Tourenplanung: Rennrad, Trekking/Tourenrad oder Mountainbike. Bestimmt, über welche Wege die Strecke führt. | [4.8](#k4-8) |
| FIT-Datei | Dateiformat, in dem Garmin-Geräte Touren aufzeichnen (Endung .fit). | [4.2](#k4-2) |
| Filterleiste | Felder über der Tabelle der Trainingseinheiten zum Eingrenzen der Einheiten. | [4.4](#k4-4) |
| Glätten | Entfernt das Zittern der GPS-Linie einer Strecke. | [4.6](#k4-6) |
| GPX-Datei | Verbreitetes Dateiformat für GPS-Strecken (Endung .gpx). Für Export, Planungen und Overlays. | [4.6](#k4-6), [4.8](#k4-8) |
| Hauptaktivität | Die wichtigste Aktivität. Beim Start vorgewählt und Vorgabe bei „alle Aktivitäten“. | [4.9.4](#k4-9-4) |
| hm | Höhenmeter: die Summe aller Anstiege einer Tour. | [4.4](#k4-4) |
| Höhenprofil | Diagramm der Höhe entlang der Strecke. | [4.6](#k4-6) |
| Jahresrekord | Höchster Jahreswert, zum Beispiel die meisten Kilometer in einem Jahr. | [4.3](#k4-3) |
| Karteikarte | Registerkarte im Fenster, zum Beispiel „Aktuell“, „Trainingseinheiten“, „Wartung“. | [3.3](#kapitel-3) |
| Kategorie | Frei wählbares Stichwort für Einheiten, zum Beispiel „Urlaub“. Eine Einheit kann mehrere haben. | [4.5](#k4-5), [4.9.4](#k4-9-4) |
| km-Stand | Gesamtkilometer eines Rads an einem Tag einschließlich Anfangskilometer. | [4.7](#k4-7) |
| Mediengerät (MTP) | Art, wie sich der Edge 1040 am PC anmeldet: nicht als Laufwerk, sondern wie ein Smartphone. AndyTours kann trotzdem darauf zugreifen. | [4.2](#k4-2) |
| Mehrtagestour | Gemeinsame Ansicht mehrerer Einheiten, zum Beispiel einer Radreise. | [4.6](#k4-6) |
| NAS | Netzwerkspeicher im Heimnetz. Kann als Datenordner dienen. | [4.9.4](#k4-9-4) |
| OpenStreetMap | Freie Weltkarte. Standardkarte in AndyTours. | [4.6](#k4-6) |
| OpenTopoMap | Kartendarstellung mit Höhenlinien und Wegen auf Basis von OpenStreetMap. | [4.6](#k4-6) |
| openrouteservice | Routingdienst mit Zugangsschlüssel, wählbar als Ausweichdienst. | [4.9.4](#k4-9-4) |
| Overlay | Gespeicherte Strecke, die zusätzlich auf den Karten eingeblendet wird. | [4.8](#k4-8) |
| Rad-Kennung | Kennung, die der Edge nach einer Tour zum gefahrenen Rad speichert. AndyTours erkennt daran das Rad. | [4.2](#k4-2) |
| Routingdienst | Internetdienst, der Strecken über Straßen und Wege berechnet. | [4.8](#k4-8), [4.9.4](#k4-9-4) |
| Saisonziel | Jahresziel für Kilometer, Höhenmeter und Anzahl je Aktivität. | [4.9.2](#k4-9-2) |
| Sensor | Geschwindigkeits- oder Trittfrequenzsensor am Rad. Hilft beim Erkennen des Rads. | [4.2](#k4-2) |
| Sicherung | Kopie des Datenbestands mit Datum und Uhrzeit im Namen. | [4.9.4](#k4-9-4), [4.9.5](#k4-9-5) |
| Soll-Stand | Anteil des Saisonziels, der bis heute erreicht sein sollte. | [4.3](#k4-3) |
| Standard-Equipment | Equipment, das bei neuen Einheiten einer Aktivität vorbelegt ist. | [4.9.4](#k4-9-4) |
| Standardkategorie | Kategorie, die bei neuen Einheiten angehakt ist. Auch ein Equipment kann eine Standardkategorie haben. | [4.9.4](#k4-9-4) |
| Statusleiste | Leiste am unteren Rand des Hauptfensters mit Anzahl, Meldungen und Datenordner. | [4.1](#k4-1) |
| Tätigkeit | Art einer Wartung, zum Beispiel „Kettenwechsel“. | [4.7](#k4-7) |
| Track | Aufgezeichnete Strecke einer Einheit. | [4.6](#k4-6) |
| Trainingsbereich | Kürzel für die Intensität einer Einheit, zum Beispiel „GA1“. Spalte **Bereich**. | [4.4](#k4-4) |
| Wartungsalarm | Erinnerung an eine Wartung nach einer bestimmten Kilometerzahl. | [4.7](#k4-7) |
| Wegpunkt | Von Ihnen gesetzter Punkt einer Planung, über den die Strecke führt. | [4.8](#k4-8) |
| Zugangsschlüssel | Persönlicher Schlüssel für openrouteservice. | [4.9.4](#k4-9-4) |
| Ø | Zeichen für „Durchschnitt“, zum Beispiel Ø km/h. | [4.4](#k4-4) |

---

<a id="anhang-offene-fragen"></a>
## Anhang: Offene Fragen

Alle offenen Fragen sind beantwortet und eingearbeitet.
