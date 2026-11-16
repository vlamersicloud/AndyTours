# Tourdetail und Puls – API-First-Entwurf

Designnotiz, keine Implementierung. Bestehende `HauptfensterViewModel.Tourdetail`, `TourdetailViewModel`, `LesePulsstatistik` und `JahresfortschrittViewModel.Puls`-Funktionen bleiben erhalten.

Vorgeschlagene Verträge: `TourdetailAnfrage` mit Tour-IDs; Antwort metadatiert geordnete Etappen, typisierte Messpunkte und begrenzte Warnungen. `ITourdetailLeser` kapselt den Fall; Data liest Tour-Snapshot und Anhänge serverseitig, Import übernimmt begrenztes sicheres FIT-Lesen. Keine beliebigen Clientpfade, SQL oder FIT-Parser im Client.

Einzelne Tour ohne lesbare FIT-Datei blockiert weiterhin das Detail; bei Mehrtagestouren bleiben fehlende/unlesbare Etappen als Lücken. Höhen-, Puls-, Geschwindigkeits-, Trittfrequenz- und Temperaturdiagramme, Karte, GPX und Geräteexport müssen erhalten bleiben. Core-Etappenordnung/-Verbindung wiederverwenden; Fahrzeit stammt aus FIT-Timerereignissen ohne Pausen, Messungen bleiben nullable. Keine Wandzeit- oder Durchschnittspuls-Schätzungen.

Pulssemantik beibehalten: aktuelle Referenzgrenzen am Abfragezeitpunkt, keine historische Profilversion; Defaultprofil CYCLING, sonst DEFAULT. Sportwahl bleibt bewusst; ohne Profil/BPM-Bereiche gelten 10-bpm-Bins. Reale UTC-Messzeitpunkte bestimmen Lücken. Aggregation respektiert Datums-, Aktivitäts-, Kategorienfilter und Raster; fehlende FITs zählen als fehlende Daten.

Vor Freigabe Regressionen mit synthetischen FITs, Cancellation, sicheren Anhängenpfaden, Mehrtagelücken und UI-Adapter vorbereiten und nur kompilieren. Laufzeit- und Geräteverifikation bleiben offen.