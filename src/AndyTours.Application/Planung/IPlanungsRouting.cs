using AndyTours.Contracts.V1;

namespace AndyTours.Application.Planung;

/// <summary>Berechnet eine geplante Route über einen serverseitig konfigurierten Routinganbieter.</summary>
public interface IPlanungsRouting
{
    Task<PlanungsRoutingAntwort> Berechnen(PlanungsRoutingAnfrage anfrage, CancellationToken cancellationToken);
}
