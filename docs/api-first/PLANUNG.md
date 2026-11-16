# API-first: Planung und Routing

**Status:** Tourenplanung und Routing sind noch nicht API-migriert. Der vorhandene Course-Export (src/AndyTours.App/Funktionen/ApiCourseUebertragung.cs, src/AndyTours.Client/CourseExportHttpClient.cs) überträgt aufgezeichnete Tracks; er ist keine geplante-Tour-Parität.

## Quellseams

- Planungsmodell/-regeln: src/AndyTours.Core/Modell/Tourenplanung.cs, Planungseinstellungen.cs, Routingeinstellungen.cs; src/AndyTours.Core/Berechnungen/Planungsaenderung.cs, BildeRoutinganfrage.cs, PlanungsGpx.cs.
- Bestehende UI und Dateiabläufe: src/AndyTours.App/ViewModels/TourenplanungViewModel.cs, TourenplanungViewModel.Datei.cs, HauptfensterViewModel.Tourenplanung.cs, src/AndyTours.App/Views/TourenplanungFenster.xaml(.cs).
- Lokale Persistenz: src/AndyTours.Daten/PlanungEinstellung.cs, RoutingEinstellung.cs. Geheimnis ist Routingeinstellungen.OpenrouteserviceSchluessel; niemals aus GETs, Protokollen oder Fehlermeldungen zurückgeben.

## Begrenzte Umsetzungsslices

1. Versionierte Contracts/Ports und API für nichtgeheime Einstellungen; CAS für Änderungen, Schlüssel nur gezielt setzen/rotieren.
2. Serverseitiges Routing über konfigurierte Provider mit Profil-/Punkt-/Antwortgrößenlimits; keine frei wählbaren URLs oder beliebigen Header vom Client.
3. Geplante Geometrie bearbeiten, validieren und persistent speichern; bestehende Planungsaenderung- und Namensregeln wiederverwenden.
4. Planned-course Export/GPX und Geräteadapter-Parität mit expliziten Abbruch-, Wiederholungs- und Lebenszyklusgrenzen.

Bis diese Slices und Windows-/Provider-Laufzeitprüfungen abgeschlossen sind, keine Vollständigkeitsbehauptung.
