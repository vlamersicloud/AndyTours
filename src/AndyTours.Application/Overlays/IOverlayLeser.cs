using AndyTours.Contracts.V1;

namespace AndyTours.Application.Overlays;

/// <summary>
/// Read-only access to overlay metadata and separately version-checked geometry.
/// No filesystem paths or backend storage details cross this port.
/// </summary>
public interface IOverlayLeser
{
    /// <summary>Loads the metadata snapshot and its aggregate opaque CAS version.</summary>
    Task<OverlayBestandAntwort> Laden(CancellationToken cancellationToken);

    /// <summary>
    /// Loads an item's geometry only if it still matches the expected opaque item version,
    /// so the returned segments correspond to the metadata snapshot.
    /// </summary>
    /// <exception cref="Stammdaten.StammdatenException">
    /// Uses NichtGefunden when the overlay is absent and Konflikt when its version changed.
    /// </exception>
    Task<OverlayInhaltAntwort> Inhalt(long overlayId, string erwarteteVersion, CancellationToken cancellationToken);
}
