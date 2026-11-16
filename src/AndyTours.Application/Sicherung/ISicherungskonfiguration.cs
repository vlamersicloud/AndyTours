namespace AndyTours.Application.Sicherung;

using AndyTours.Contracts.V1;

public interface ISicherungskonfiguration
{
    Task<SicherungskonfigurationAntwort> Laden(CancellationToken cancellationToken);
    Task<SicherungskonfigurationAntwort> Speichern(SicherungskonfigurationAnfrage anfrage, CancellationToken cancellationToken);
}
