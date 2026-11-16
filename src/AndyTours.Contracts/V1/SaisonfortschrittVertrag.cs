namespace AndyTours.Contracts.V1;

/// <summary>Saisonprojektion für ein serverseitig erfasstes Stichtagsdatum.</summary>
/// <remarks>
/// Wird nur geliefert, wenn die Anfrage exakt vom 1. Januar des Stichtagsjahres bis einschließlich Stichtag reicht.
/// Bei anderen Zeiträumen und bei älteren Backends ohne Feld ist der Wert null, nicht nullsummig.
/// Aktivität filtert die Auswahl; Kategorien begrenzen Ist-Werte, nicht Jahresziele. Tabellenfilter wirken nicht mit.
/// Zukünftige Touren zählen nicht. Dieser Wire-Vertrag enthält keine Core- oder Datenbanktypen.
/// </remarks>
public sealed record SaisonfortschrittAntwort(
    DateOnly Stichtag,
    SaisonkennzahlAntwort Kilometer,
    SaisonkennzahlAntwort Hoehenmeter,
    SaisonkennzahlAntwort Anzahl,
    SaisonkennzahlAntwort FahrzeitStunden);

/// <summary>Abbildung der bestehenden Core-Jahreskennzahlen; Fahrzeit wird in Stunden transportiert.</summary>
/// <remarks>Für Fahrzeit gibt es kein Ziel; Ziel-, Anteil-, Soll-, Abweichungs-, Rest- und Wochenfelder bleiben null.</remarks>
public sealed record SaisonkennzahlAntwort(
    double Stand,
    double? Ziel,
    double? Anteil,
    double? Soll,
    double? Abweichung,
    double? Rest,
    double? ProWoche);
