namespace AndyTours.Contracts.V1;

/// <summary>
/// Filter und Raster für die Pulsauswertung. Ein null-ProfilSport fordert 10-bpm-Bereiche;
/// ein Sportname muss einem tatsächlich gespeicherten aktuellen Profil entsprechen.
/// </summary>
public sealed record PulsAuswertungsAnfrage(
    DateOnly Von,
    DateOnly Bis,
    Auswertungsraster Raster,
    long? AktivitaetId,
    long? KategorieId,
    string? ProfilSport);

public sealed record PulsperiodeAntwort(DateOnly Von, DateOnly Bis, string Beschriftung);

public sealed record PulsbereichAntwort(int Bereich, string Zone, string Pulsbereich);

/// <summary>
/// Anteil ist der Anteil der gemessenen Zeit innerhalb dieser Periode (0..1), nicht der Tourdauer.
/// Puls-Lücken, Pausen und Datenlücken über 60 Sekunden werden nicht interpoliert oder geschätzt.
/// </summary>
public sealed record PulszelleAntwort(DateOnly Periode, int Bereich, double Sekunden, double Anteil);

/// <summary>Gefilterte Pulszeitverteilung mit aktueller, nicht historischer Referenz.</summary>
public sealed record PulsAuswertungAntwort(
    DateOnly Von,
    DateOnly Bis,
    Auswertungsraster Raster,
    int Aktivitaeten,
    int OhneDaten,
    double MesszeitSekunden,
    double FahrzeitSekunden,
    PulsReferenzAntwort Referenz,
    string? VerwendetesProfil,
    IReadOnlyList<PulsperiodeAntwort> Perioden,
    IReadOnlyList<PulsbereichAntwort> Bereiche,
    IReadOnlyList<PulszelleAntwort> Zellen);
