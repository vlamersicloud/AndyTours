namespace AndyTours.Contracts.V1;

/// <summary>
/// Metadata accompanying one separately streamed GPX body. The stable request ID,
/// normalized basename, aggregate snapshot version, and physical body hash identify an exact replay.
/// The request contains no client path or XML document field.
/// </summary>
public sealed record OverlayGpxImportAnfrage(
    string Version,
    Guid AnfrageId,
    long? OrdnerId,
    string Dateiname);
