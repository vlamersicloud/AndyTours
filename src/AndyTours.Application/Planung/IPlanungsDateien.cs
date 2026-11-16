using AndyTours.Contracts.V1;

namespace AndyTours.Application.Planung;

public interface IPlanungsDateien
{
    Task<PlanungsStand> Lesen(Stream stream, CancellationToken cancellationToken);
    Task<PlanungsGpxExportAntwort> Schreiben(PlanungsGpxExportAnfrage anfrage, CancellationToken cancellationToken);
}
