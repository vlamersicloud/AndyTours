# Funde 5 – App (Oberfläche)

Geprüft: alle 91 C#-Dateien und 26 XAML-Dateien unter `src/AndyTours.App`, dazu für die ganze
Projektmappe die Analyzer-Regeln für ungenutzte private Teile, Parameter, Zuweisungen und
`using`-Zeilen (vorübergehend als Warnung eingeschaltet).

Die Funde sind wenige und betreffen mehrere Fenster zugleich; deshalb sind sie nach Art statt nach
Fenstern gegliedert.

Freigabe: alle Punkte freigegeben am 15.11.2026

## Ungenutzter Code
Kein ungenutzter Code. Zu weit sichtbar bzw. überflüssig:

| Nr. | Fund | Dateien | Vorschlag |
|---|---|---|---|
| 5.1 | 17 öffentliche Mitglieder werden nur in ihrer eigenen Klasse verwendet, meist Meldungstexte (`KeineAntwort`, `NichtEingestellt`, `Ebenenname`, `ErgebnisMitWarnung`, `FindeZiele`, `AendernMoeglich`, `Bereichshinweis`, `Entwicklungshinweis`, `Schluesselhinweis`, `UngueltigeAdresse`, `IstEigenesZiel`, `KeineGpsDaten`, `KeineHoehendaten`, `KeinePlanung`, `DienstNichtErreichbar`, `EinstellungenUnvollstaendig`, `HatWegpunkte`) | `Abbiegungsabruf`, `Overlaydarstellung`, `CourseUebertragenViewModel`, `EinstellungenViewModel`, `Saisonzielzeile`, `StreckenkarteViewModel`, `TourdetailViewModel`, `TourenplanungViewModel` | `private` (bzw. `private set`); wo `[RelayCommand(CanExecute = …)]` oder Bindungen sie brauchen, bleiben sie öffentlich – das prüft der Build |
| 5.2 | Analyzer-Funde außerhalb der App: eine überflüssige Variable (`is long anzahl and > 0`) und zwei unnötige `using`-Zeilen | `Daten/OeffneDatenbank.cs`, `Core/Berechnungen/SaisonzielEingaben.cs`, `tests/…/Daten/WartungenTests.cs` | entfernen |

## Doppelte Logik
| Nr. | Fund | Dateien | Vorschlag | Wirkung |
|---|---|---|---|---|
| 5.3 | Karten: Umrechnung Grad → Kartenkoordinaten (3×), Linie aus Positionen (3×), runder/farbiger Punkt (2×), neue Karte ohne Widgets und Drehung (2×) | `TourdetailFenster.xaml.cs`, `TourenplanungFenster.xaml.cs`, `Overlaydarstellung.cs` | gemeinsame Klasse `Funktionen/Kartenzeichnung.cs` mit `ErzeugeKarte`, `InKartenkoordinaten`, `InGeoposition`, `Linie`, `Zeichen` | ca. −40 Zeilen |
| 5.4 | Tabellen: „nächstes Element eines Typs aufwärts im Baum“ (3×), „Rechtsklick markiert die Zeile“ (2×), „mehrere Zeilen markieren und die erste zeigen“ (2×), „Befehl bei Doppelklick bzw. Entf ausführen“ | `EinheitenTabelle.xaml.cs`, `WartungenAnsicht.xaml.cs`, `OverlaysFenster.xaml.cs` | gemeinsame Klasse `Funktionen/Tabellenbedienung.cs` | ca. −40 Zeilen |
| 5.5 | Viermal derselbe Ablauf „Einstellung speichern, bei Fehler Meldung“ | `EinstellungenViewModel.cs` | eine private Methode `Speichere(Action<SqliteConnection>)` | ca. −25 Zeilen |
| 5.6 | Der Pfad der Datenbank wird sechsmal aus Datenordner und Dateiname zusammengesetzt | `HauptfensterViewModel*.cs` | Eigenschaft `Datenbankdatei` wie im `EinstellungenViewModel` | kürzere Zeilen |
| 5.7 | Namensvergleich ausgeschrieben | `HauptfensterViewModel.Auswahllisten.cs`, `AlarmBearbeitenViewModel.cs`, `WartungBearbeitenViewModel.cs` | `VergleicheNamen` | einheitlich |

## Veraltete Kommentare
| Nr. | Datei | Heute | Vorschlag |
|---|---|---|---|
| 5.8 | `Funktionen/KopiereMitFortschritt.cs`, `ViewModels/FortschrittsViewModel.cs` | „wiederverwendbar … z. B. die spätere Datensicherung“ – die Datensicherung kopiert ohne dieses Fenster | sagen, wofür es heute dient: Wechsel des Datenordners |
| 5.9 | `Views/Hauptfenster.xaml` | Filter „wirken später auf alle Karteikarten“ | „Filter für Jahr und Aktivität“ ohne „später“ |

## Projektregeln
Eingehalten: Jede XAML-Datei beginnt mit einem Kommentar zum Aufbau, jedes Fenster meldet sich bei
der Skalierung an, eigene Stile bauen auf dem Fluent-Stil auf (Ausnahmen nur `Border` und
`ContentPresenter`), eine Klasse je Datei.
