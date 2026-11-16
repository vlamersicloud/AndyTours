using AndyTours.Contracts.V1;
namespace AndyTours.Application.Garmin;
public interface IGarminEinstellungen
{
    Task<GarminEinstellungenAntwort> Laden(CancellationToken ct = default);
    Task<GarminEinstellungenAntwort> SchluesselSpeichern(GarminSchluesselAnfrage anfrage, CancellationToken ct = default);
    Task<GarminEinstellungenAntwort> SchluesselEntfernen(GarminVersionAnfrage anfrage, CancellationToken ct = default);
    Task<GarminEinstellungenAntwort> AutomatikSpeichern(GarminAutomatikAnfrage anfrage, CancellationToken ct = default);
}
