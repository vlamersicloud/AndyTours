# Touren-Mehrfachaktionen: WPF-Ablauf

**WPF-Implementierung vorhanden; Tests nur kompiliert, nicht ausgeführt.**

Die Tourentabelle führt eine Auswahl ausschließlich auf der aktuell geladenen Seite. Der Root-Handler kopiert IDs, Versionen und Kategorien vor jedem Lookup; Bearbeiten/Kopieren bleiben Einzelauswahl. Kategorien-, Aktivitäts-, Equipment- und Löschaktionen öffnen zuerst Auswahl/Preview und danach ein eigenes besitzendes Aktionsmodal mit ausdrücklichem Ausführen. Das Modal zeigt Anzahl und Zusammenfassung; es sendet nicht automatisch. Kategorien und Stammdaten-Picker verwenden frische Lookupwerte, aber behalten die ursprünglichen Tourversionen.

Das UI-ViewModel hält eine normalisierte Anfrage und stabile UUID. Bei ungewissem ACK kann nur dieselbe Anfrage manuell wiederholt werden; bestätigte oder abgelehnte Aktionen senden nicht erneut. Schließen bei ungewissem Ergebnis warnt. Der Root sperrt konkurrierende Aktionen, aktualisiert danach die Übersicht und unterscheidet Ergebnis von Refreshstatus; Refreshfehler nach ACK wiederholen nie die Mutation. Physische Anhangsdateien bleiben bei Löschen erhalten.

Offen: Windows-Rendering/-Bedienung und echte Mehrclient-/Runtimeprüfung. Keine DB-/FIT-Logik in der Oberfläche.
