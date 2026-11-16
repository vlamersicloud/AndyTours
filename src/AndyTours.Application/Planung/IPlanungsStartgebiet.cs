using AndyTours.Contracts.V1;

namespace AndyTours.Application.Planung;

public interface IPlanungsStartgebiet
{
    Task<PlanungsStartgebietAntwort> Laden(CancellationToken cancellationToken);
}
