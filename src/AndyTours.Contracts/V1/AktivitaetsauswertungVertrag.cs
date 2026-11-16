namespace AndyTours.Contracts.V1;

/// <summary>Gruppierungsraster für die Aktivitätsauswertung.</summary>
public enum Auswertungsraster
{
    /// <summary>Gruppierung nach Kalenderjahr.</summary>
    Jahr,
    /// <summary>Gruppierung nach Kalendermonat.</summary>
    Monat,
    /// <summary>Gruppierung nach Kalenderwoche.</summary>
    Woche,
    /// <summary>Gruppierung nach Kalendertag.</summary>
    Tag
}

/// <summary>Filter und Gruppierung einer Aktivitätsauswertung.</summary>
public sealed record AktivitaetsauswertungAnfrage(
    DateOnly Von,
    DateOnly Bis,
    Auswertungsraster Raster,
    long? AktivitaetId,
    long? KategorieId);

/// <summary>Kennzahlen einer einzelnen Auswertungsperiode.</summary>
public sealed record AktivitaetsperiodenAntwort(
    DateOnly Von,
    DateOnly Bis,
    string Beschriftung,
    int Anzahl,
    double DistanzKm,
    long Hoehenmeter,
    long FahrzeitSekunden);

/// <summary>Gesamtergebnis und gruppierte Perioden einer Aktivitätsauswertung.</summary>
public sealed record AktivitaetsauswertungAntwort(
    DateOnly Von,
    DateOnly Bis,
    Auswertungsraster Raster,
    int Anzahl,
    double DistanzKm,
    long Hoehenmeter,
    long FahrzeitSekunden,
    int ZukuenftigeAktivitaeten,
    IReadOnlyList<AktivitaetsperiodenAntwort> Perioden)
{
    public IReadOnlyList<BestleistungszeileAntwort> Jahresrekorde { get; init; } = [];
    public IReadOnlyList<BestleistungszeileAntwort> Einzelrekorde { get; init; } = [];
    public IReadOnlyList<JahreszielAntwort> Jahresziele { get; init; } = [];
};

public sealed record BestleistungszeileAntwort(string Bezeichnung, string Wert, string Herkunft, bool IstLaufendesJahr, long? TourId);
public sealed record JahreszielAntwort(int Jahr, long ZielKm, long ZielHm, long ZielAnzahl);
