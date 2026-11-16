namespace AndyTours.Contracts.V1;

/// <summary>Tabellensumme über sämtliche Treffer der aktiven Tabellenfilter, vor Sortierung und Paging.</summary>
/// <param name="Anzahl">Anzahl aller gefilterten Touren.</param>
/// <param name="DistanzKm">Core-Summe der Distanzen, auf zwei Nachkommastellen gerundet.</param>
/// <param name="Hoehenmeter">Summe der Höhenmeter.</param>
/// <param name="FahrzeitSekunden">Gesamte Core-Fahrzeit, nach Addition der Originalticks in Sekunden übertragen.</param>
/// <remarks>Reiner Wire-Vertrag ohne Datenbank- oder Fachmodelltypen; optionales Metadatum bleibt mit älteren Responses kompatibel.</remarks>
public sealed record TourenTabellensummeAntwort(
    int Anzahl,
    double DistanzKm,
    int Hoehenmeter,
    long FahrzeitSekunden);
