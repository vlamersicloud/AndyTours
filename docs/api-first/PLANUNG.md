# API-first: Planung und Routing

**Status:** API-Planungsablauf ist teilweise angebunden: serverseitiges Routing, versionsgeschütztes Fahrprofil, GPX-Öffnen/Speichern, geplanter Course-Export und Geräteadapter verwenden typisierte Ports. Der vorhandene Course-Export für aufgezeichnete Tracks ist davon getrennt. Die API-Planungsansicht nutzt denselben Overlaykontext; Startausschnitt: gemerkte lokale Grenzen; andernfalls werden höchstens die fünf jüngsten vorhandenen FIT-Aufzeichnungen geprüft und der erste nutzbare GPS-Ausschnitt übernommen. Fehlende Dateien zählen nicht; ohne nutzbaren Ausschnitt gilt der bisherige Deutschland-Ausschnitt. Routingkonfiguration ist über Datei → Routing-Einstellungen im API-Modus verfügbar.

## Quellseams

- Planungsmodell/-regeln: src/AndyTours.Core/Modell/Tourenplanung.cs, Planungseinstellungen.cs, Routingeinstellungen.cs; src/AndyTours.Core/Berechnungen/Planungsaenderung.cs, BildeRoutinganfrage.cs, PlanungsGpx.cs.
- Bestehende UI und Dateiabläufe: src/AndyTours.App/ViewModels/TourenplanungViewModel.cs, TourenplanungViewModel.Datei.cs, HauptfensterViewModel.Tourenplanung.cs, src/AndyTours.App/Views/TourenplanungFenster.xaml(.cs).
- Lokale Persistenz: src/AndyTours.Daten/PlanungEinstellung.cs, RoutingEinstellung.cs. Geheimnis ist Routingeinstellungen.OpenrouteserviceSchluessel; niemals aus GETs, Protokollen oder Fehlermeldungen zurückgeben.

## Begrenzte Umsetzungsslices

1. Versionierte Contracts/Ports und API für nichtgeheime Einstellungen; CAS für Änderungen, Schlüssel nur gezielt setzen/rotieren.
2. Serverseitiges Routing über konfigurierte Provider mit Profil-/Punkt-/Antwortgrößenlimits; keine frei wählbaren URLs oder beliebigen Header vom Client.
3. Planung bleibt wie heute eine lokale GPX-Datei: gezielte Dateiöffnung/-speicherung bleibt im Client; Parsing und Serialisierung verwenden die bestehenden PlanungsGpx-Regeln. Keine verpflichtende Server-Planungsentität ohne fachliche Entscheidung.
4. Planned-course Export/GPX und Geräteadapter-Parität mit expliziten Abbruch-, Wiederholungs- und Lebenszyklusgrenzen.

GPX bleibt eine lokale Clientdatei; Backend-Parsing und -Serialisierung sind auf 128 MiB begrenzt. Profiländerungen verwenden CAS mit koaleszierten Änderungen; bei unbekanntem Versionsstand erfolgt ein GET nur nach manueller Synchronisierung. Viewport und GPX-Ordner bleiben lokale Einstellungen. Routing nutzt serverseitige Providerkonfiguration (8 MiB Antwortlimit, 20 s Zeitlimit); Schlüssel werden nicht an Clients ausgegeben. Windows-, Provider- und Geräte-Laufzeitprüfungen sowie die Ausführung der vorbereiteten FIT-Zeitstempelassertions bleiben offen. Keine Vollständigkeitsbehauptung.
