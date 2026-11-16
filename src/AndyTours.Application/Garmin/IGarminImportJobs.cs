using AndyTours.Contracts.V1;
namespace AndyTours.Application.Garmin;
public interface IGarminImportJobs
{
    Task<GarminImportJobAntwort> Starten(GarminImportStartAnfrage anfrage, CancellationToken ct = default);
    Task<GarminImportJobAntwort?> Laden(Guid jobId, CancellationToken ct = default);
    Task<GarminImportJobAntwort?> AktuellenLaden(CancellationToken ct = default);
    Task<GarminImportKandidatenAntwort> Kandidaten(Guid jobId, int start, int anzahl, CancellationToken ct = default);
    Task<GarminImportJobAntwort> Fortsetzen(Guid jobId, GarminImportSteuerAnfrage anfrage, CancellationToken ct = default);
    Task<GarminImportJobAntwort> Abbrechen(Guid jobId, GarminImportSteuerAnfrage anfrage, CancellationToken ct = default);
}
