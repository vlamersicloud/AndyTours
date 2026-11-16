using AndyTours.Contracts.V1;

namespace AndyTours.Application.Planung;

public interface IPlanungsProfil
{
    Task<PlanungsProfilAntwort> Laden(CancellationToken cancellationToken);
    Task<PlanungsProfilAntwort> Speichern(PlanungsProfilSpeichernAnfrage anfrage, CancellationToken cancellationToken);
}
