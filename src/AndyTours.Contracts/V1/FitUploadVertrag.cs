namespace AndyTours.Contracts.V1;

public enum FitUploadStatus
{
    Hochgeladen,
    Importiert,
    Uebersprungen
}

public sealed record FitUploadAntwort(
    Guid UploadId,
    string Version,
    string Dateiname,
    string InhaltHash,
    long Byteanzahl,
    FitUploadStatus Status,
    long? TourId,
    DateTimeOffset ErstelltUtc);

public sealed record FitUploadListeAntwort(
    int Start,
    int Gesamt,
    IReadOnlyList<FitUploadAntwort> Eintraege);

public sealed record FitUploadPruefanfrage(IReadOnlyList<Guid> UploadIds);

public sealed record FitUploadPruefzeile(
    Guid UploadId,
    string DateiVersion,
    DateOnly Datum,
    TimeOnly Startzeit,
    long FahrzeitSekunden,
    double DistanzKm,
    int Hoehenmeter,
    string Geraet,
    string Profil,
    long? EmpfohlenesEquipmentId,
    bool BereitsVorhanden,
    string? Warncode);

public sealed record FitUploadPruefungAntwort(
    string Version,
    IReadOnlyList<FitUploadPruefzeile> Zeilen,
    IReadOnlyList<EquipmentAntwort> Equipments)
{
    public string? FallbackZeitzoneId { get; init; }
}

public sealed record FitUploadAuswahl(
    Guid UploadId,
    string DateiVersion,
    bool Uebernehmen,
    long? EquipmentId);

public sealed record FitUploadUebernahmeAnfrage(
    string Version,
    Guid AnfrageId,
    IReadOnlyList<FitUploadAuswahl> Auswahl);

public sealed record FitUploadUebernahmeZeile(
    Guid UploadId,
    long? TourId,
    bool Importiert,
    string? Hinweiscode);

public sealed record FitUploadUebernahmeAntwort(
    IReadOnlyList<FitUploadAntwort> Uploads,
    IReadOnlyList<FitUploadUebernahmeZeile> Ergebnisse);
