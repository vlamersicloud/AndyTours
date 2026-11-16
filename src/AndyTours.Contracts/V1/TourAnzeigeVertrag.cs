namespace AndyTours.Contracts.V1;

/// <summary>Optionale Legacy-kompatible Anzeigeprojektion einer Tour; keine fachliche Identität.</summary>
public sealed record TourAnzeigeAntwort(
    DateOnly Datum,
    TimeOnly? Startzeit,
    string Name,
    string Aktivitaet,
    string Kategorien,
    string Equipment,
    double? DistanzKm,
    int Hoehenmeter,
    long? FahrzeitSekunden,
    double? DurchschnittKmh,
    double? MaxKmh,
    int? HfDurchschnitt,
    int? HfMax,
    int? Trittfrequenz,
    int? Temperatur,
    int? MaxHoehe,
    string Trainingsbereich,
    string Kommentar,
    bool IstBestDistanz,
    bool IstBestHoehenmeter,
    bool IstBestFahrzeit,
    bool IstBestDurchschnittKmh,
    bool IstBestMaxKmh);
