using AndyTours.Contracts.V1;

namespace AndyTours.Application.Auswertung;

/// <summary>
/// Server-only source metadata for detail assembly. Relative attachment paths are internal coordination data:
/// never serialize them to HTTP or include them in logs.
/// </summary>
public sealed record TourdetailQuelletappe(TourAntwort Tour, string? RelativerFitPfad);

/// <summary>Backend snapshot of requested tours and their server-selected attachment references.</summary>
public sealed record TourdetailQuellbestand(IReadOnlyList<TourdetailQuelletappe> Etappen);

/// <summary>Server-only port; callers request tour IDs and attachment selection remains backend-owned.</summary>
public interface ITourdetailQuelldaten
{
    Task<TourdetailQuellbestand> Laden(IReadOnlyList<long> tourIds, CancellationToken cancellationToken);
}
