# API-Saisonfortschritt – Designnotiz

Die Legacy-UI zeigt Zeitraumbalken und -summen; nur beim Zeitraum exakt 1. Januar bis heute ergänzt sie den Saison-Stand, Soll, Rest/Vorsprung und Wochenleistung. Zukünftige Touren werden für diese Kennzahlen ausgeschlossen. Die Core-Jahreskurve belegt nicht, dass die UI eine historische Zielkurve darstellt.

Vorschlag: optionale typisierte Saisonprojektion mit Stichtag und je Kennzahl Stand sowie nullablem Ziel/Soll/Rest/Wochenwert; vier Größen: km, hm, Anzahl und Dauer, ohne Fahrzeitziel. Berechnung serverseitig mit gemeinsam erfasstem Datum und innerhalb desselben SQLite-Lesesnapshots aus Core-Regeln. Aktivitätenfilter gilt; Kategorie filtert Ist-Werte, nicht Jahresziele. Tabellenfilter beeinflussen die Saison nicht. Nur exakt 1. Januar bis Stichtag darf als YTD erscheinen; beliebige Teilzeiträume dürfen nicht als Saisonstand ausgegeben werden. Bestehende Perioden, Rekorde und Kalenderjahresziele bleiben unverändert.

Noch offen: Vertrag und Randfalltests (Jahresanfang/-ende, Schaltjahr, keine Aktivität/Ziele, Zukunftstouren, Kategorien). Keine clientseitige Datenbestandsberechnung. Design-only; keine Implementierung oder Laufzeitbehauptung. Gesamtmigration weiterhin offen.
