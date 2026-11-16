# Routingkonfiguration im API-Modus

Status: implementiert und in der API-WPF-Oberfläche unter Datei → Routing-Einstellungen erreichbar. Der Backend-Port bietet sechs begrenzte Operationen: Metadaten laden sowie Dienst, Adresse, Adress-Reset, ORS-Schlüssel speichern und ORS-Schlüssel entfernen. Jede Mutation ist feldbezogen und nutzt einen CAS über die Rohwerte der Routing-Schlüssel; der ORS-Schlüssel wird gehasht, nicht im Klartext als Version geführt.

GET liefert nur öffentliche Metadaten: nie den Schlüssel und keine URI mit Zugangsdaten; unsichere gespeicherte URI bleibt verborgen und unverändert. Schlüsselwechsel und Resets benötigen explizite Aktionen. Der typisierte Client führt keine Providerabfrage aus. Das unabhängige Einstellungsfenster hängt am bestehenden API-Client, hält Eingaben als Entwürfe, verlangt manuelles Neuladen nach Konflikt oder unklarem ACK und unterstützt Abbruch. Es gibt keine automatischen Speichervorgänge.

Die strukturellen und ViewModel-Tests sind vorbereitet und kompiliert, wurden aber nicht ausgeführt. Windows- und Provider-Laufzeitverhalten bleiben unverified.
