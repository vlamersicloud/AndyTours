namespace AndyTours.Contracts.V1;

/// <summary>Requests a non-destructive restore preview for an opaque catalog ID and expected complete-file SHA-256.</summary>
public sealed record WiederherstellungsvorschauAnfrage(
    Guid SicherungsId,
    string ErwarteterInhaltHash);

/// <summary>Preview metadata only; neither validation fields nor this response authorize applying a restore.</summary>
/// <param name="Sicherung">Catalog metadata selected by opaque ID.</param>
/// <param name="ManifestFormatversion">Null when unknown or unchecked; do not infer it from archive kind.</param>
/// <param name="Dateianzahl">Count of database plus payload files; null means unknown or unchecked, not zero.</param>
/// <param name="NutzdatenBytes">Expanded database and payload bytes; null means unknown or unchecked, not zero.</param>
/// <param name="DatenbankGeprueft">False for metadata-only preview until an actual isolated database check succeeds.</param>
/// <param name="AnwendungFreigegeben">False for metadata-only preview; not an authorization to apply.</param>
/// <param name="Hinweise">Safe user-facing warnings and unmet requirements; no paths or raw exception data.</param>
public sealed record WiederherstellungsvorschauAntwort(
    SicherungAntwort Sicherung,
    int? ManifestFormatversion,
    int? Dateianzahl,
    long? NutzdatenBytes,
    bool DatenbankGeprueft,
    bool AnwendungFreigegeben,
    IReadOnlyList<string> Hinweise);
