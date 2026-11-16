# Equipment-Einsatz und Haltbarkeit

**Quellenprüfung; noch keine Einsatz-API/UI.** Legacy `EquipmentEinsatzViewModel` zeigt ungefiltert über alle Touren je Equipment `VorherKm` (Anfangskilometer), `GesamtKm` (Anfang plus Distanzen) und `GesamtHm`; fehlende/unbekannte Equipment-IDs bilden „ohne Equipment“, danach kommt ggf. die Summe. Rundet `Math.Round(km)==0` leer. `SortiereEinsatzzeilen` sortiert stabil, leere Zahlen als 0, Summenzeilen stets zuletzt. Keine Tour-ID, Erst-/Letztnutzung oder Fahrzeit wird projiziert; Namen sind keine Identität.

Wartung: `BerechneKmStand` summiert bis einschließlich Wartungstag; gleichdatige Tour zählt mit. `BerechneHaltbarkeit` vergleicht km-Stände gleichartiger Wartungen desselben Equipments und Kalendertage; ohne Vorgänger beginnt Zeit ab Anschaffung, sonst erster Tour. `ErstelleWartungszeilen`/`ErstelleAlarmzeilen` speisen bestehende API-Alarme und Wartungsprojektionen; diese bleiben unverändert.

`EquipmentAntwort` enthält Stammdaten, Ausmusterung und Anfangsdistanz, keine Einsatzzahlen. `WartungsuebersichtAntwort` enthält Wartungs-/Alarmzeilen, keinen Einsatzbericht. Der neue read-only Vertrag spiegelt nur Legacy-Zeilenfelder; keine IDs, Filter, Zeit-/Haltbarkeitsgrenzen oder geänderte Ausmusterungsregeln.

Quellen: `src/AndyTours.Core/Berechnungen/{ErmittleEquipmenteinsatz,SortiereEinsatzzeilen,BerechneKmStand,BerechneHaltbarkeit,ErstelleWartungszeilen,ErstelleAlarmzeilen}.cs`; `src/AndyTours.App/ViewModels/{EquipmentEinsatzViewModel,HauptfensterViewModel.EquipmentEinsatz,ApiEquipmentsViewModel,ApiWartungsViewModel}.cs`; `src/AndyTours.Contracts/V1/{EquipmentVerwaltungVertrag,WartungsverwaltungVertrag}.cs`; `src/AndyTours.Daten/SqliteWartungsverwaltung.cs`.
