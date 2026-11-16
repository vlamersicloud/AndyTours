using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Application.Overlays;

/// <summary>Server-parsed GPX geometry and the physical-stream replay hash.</summary>
/// <param name="Anfrage">Normalized import metadata used for idempotency and aggregate CAS.</param>
/// <param name="InhaltHash">SHA-256 of the original uploaded bytes as 64 hexadecimal characters.</param>
/// <param name="Abschnitte">Prepared geometry; segment and point order are preserved.</param>
public sealed record OverlayGpxVorbereitung(
    OverlayGpxImportAnfrage Anfrage,
    string InhaltHash,
    IReadOnlyList<IReadOnlyList<Geoposition>> Abschnitte);

/// <summary>
/// Server-side persistence coordination for GPX imports; exposes no database path,
/// credentials, transient token ownership, or filesystem operations.
/// </summary>
public interface IOverlayGpxSpeicher
{
    /// <summary>
    /// Checks an exact receipt after the caller has computed the physical stream hash,
    /// allowing a replay to return without reparsing GPX.
    /// </summary>
    Task<OverlayAnlegenAntwort?> Quittung(
        OverlayGpxImportAnfrage anfrage,
        string inhaltHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically rechecks the receipt and aggregate CAS, persists prepared geometry,
    /// and stores the idempotency receipt.
    /// </summary>
    Task<OverlayAnlegenAntwort> Importieren(
        OverlayGpxVorbereitung vorbereitet,
        CancellationToken cancellationToken);
}
