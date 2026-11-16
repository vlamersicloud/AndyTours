using AndyTours.Contracts.V1;

namespace AndyTours.Application.Garmin;

public interface IGarminImportPruefung
{
    Task<GarminImportPruefungAntwort> Laden(Guid jobId, CancellationToken cancellationToken);
    Task<GarminImportUebernahmeAntwort> Uebernehmen(Guid jobId, GarminImportUebernahmeAnfrage anfrage, CancellationToken cancellationToken);
}
