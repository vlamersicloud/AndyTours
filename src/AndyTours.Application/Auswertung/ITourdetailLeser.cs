using AndyTours.Contracts.V1;

namespace AndyTours.Application.Auswertung;

/// <summary>Application port for loading ordered tour detail data for the authenticated dataset.</summary>
public interface ITourdetailLeser
{
    Task<TourdetailAntwort> Laden(TourdetailAnfrage anfrage, CancellationToken cancellationToken);
}
