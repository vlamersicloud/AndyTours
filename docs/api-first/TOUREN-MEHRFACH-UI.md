# Touren-Mehrfachaktionen: WPF-Entwurf

**WPF-Design only; nicht implementiert.**

Das API-Hauptfenster erfasst beim Start unveränderliche ausgewählte TourAntwort-IDs und -Versionen, bevor es Stammdaten frisch lädt. Danach nutzt es die Auswahl-Dialoge KategorienZuordnenViewModel und EintragWaehlenViewModel weiter als reine Picker. Ein eigenes, dem Hauptfenster gehörendes modales Aktionsfenster zeigt die Zusammenfassung und bietet ausdrückliches Ausführen oder Abbrechen.

Ein testbares, UI-unabhängiges ViewModel besitzt die normalisierte unveränderliche Anfrage samt UUID und kennt bereit, beschäftigt, abgelehnt, ungewiss und bestätigt. Bei ungewissem ACK darf ausschließlich dieselbe Anfrage erneut gesendet werden; Schließen warnt. Nach gültigem ACK ist erneutes Senden dauerhaft gesperrt. Kein DB- oder FIT-Code im UI.

Während Vorbereitung und Dialog sperrt das Hauptfenster konkurrierende Aktionen. Nach Dialogschluss Busy lösen und Übersicht neu laden, auch bei ungewissem Ausgang. Ein Reload-Fehler nach bestätigtem ACK darf nie die Mutation wiederholen. Auswahl- und Löschbefehle in ApiHauptfenster.Tourdetail.cs prüfen; Guards/Lebenszyklus in App.Api.cs. Lokale Referenzen: KategorienZuordnenViewModel.cs und HauptfensterViewModel.Zuordnen.cs.
