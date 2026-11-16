# API-Tourentabellenfilter

Die sieben optionalen Tabellenfilter (Equipmentname, Trainingsbereich, Distanz-/Höhenmetergrenzen und Suchbegriff) werden unverändert übertragen und durch dieselbe Core-Regel auf der Datums-/Aktivitätsauswahl angewendet – vor Anzeigeprojektion, Bestwerten, Sortierung und Paging. Ungültige Zahlenbereiche bleiben gemäß Core inaktiv. Lookup-Optionen stammen aus der vollständigen Datums-/Aktivitätsbasis vor Kategorie- und Tabellenfiltern; fehlende aktuelle Auswahlwerte bleiben enthalten.

Im Touren-Tab gibt es explizites Anwenden und Zurücksetzen. Vier Grenzfehleranzeigen folgen dem Core-Parser. Tabellenfilter leeren bzw. verändern nicht Kennzahlen, Rekorde, Ziele oder Pulsdaten. Bei Refresh, Paging und Sortierung werden Anfragewerte vor dem Laden aufgenommen; Seite und Optionen werden vor Zustandsübernahme geprüft. Tour-ID, Version und Rohwerte bleiben unverändert.

Optionen-/Übernahme-Vertragstests sind vorbereitet und kompiliert, nicht ausgeführt; der frühere versehentliche Testlauf und sein begrenzter Befund sind in VERIFIKATION.md dokumentiert. Windows-Rendering ist ungeprüft. Offen bleiben seitenübergreifende Auswahl, Inline-Bearbeitung, Spaltenausblendung/-reihenfolge/-breite und weitere Legacy-Filter.

Quellen: src/AndyTours.Core/Berechnungen/FiltereEinheiten.cs, LeseFilterbereich.cs, ErmittleFilterwerte.cs; API_FIRST-Contracts, SQLite-Speicher und Touren-ViewModel.
