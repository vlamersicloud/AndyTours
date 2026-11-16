namespace AndyTours.Contracts.V1;

/// <summary>
/// Request records for overlay mutations. Every Version is the aggregate snapshot CAS
/// version, including visibility and color updates; stale versions require reload and
/// an explicit user decision. Creation additionally carries a stable request ID so a
/// retry after a lost response cannot create a duplicate folder or overlay.
/// </summary>
public sealed record OverlayOrdnerAnlegenAnfrage(
    string Version,
    Guid AnfrageId,
    long? ElternId,
    string Name);

/// <summary>Folder rename/move guarded by the aggregate snapshot version.</summary>
public sealed record OverlayOrdnerAendernAnfrage(
    string Version,
    long? ElternId,
    string Name);

/// <summary>Overlay rename/move/color update guarded by the aggregate snapshot version.</summary>
public sealed record OverlayAendernAnfrage(
    string Version,
    long? OrdnerId,
    string Name,
    OverlayFarbe? Farbe);

/// <summary>Bulk visibility update guarded by the aggregate snapshot version.</summary>
public sealed record OverlaySichtbarkeitAnfrage(
    string Version,
    IReadOnlyList<long> OverlayIds,
    bool IstEingeschaltet);

/// <summary>Shared overlay color update guarded by the aggregate snapshot version.</summary>
public sealed record OverlayGemeinsameFarbeAnfrage(string Version, OverlayFarbe Farbe);

/// <summary>The refreshed aggregate snapshot and the ID created by an idempotent creation request.</summary>
public sealed record OverlayAnlegenAntwort(OverlayBestandAntwort Bestand, long Id);
