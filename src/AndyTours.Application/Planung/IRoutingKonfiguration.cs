using AndyTours.Contracts.V1;

namespace AndyTours.Application.Planung;

/// <summary>Manages server-side routing configuration without returning provider credentials.</summary>
public interface IRoutingKonfiguration
{
    Task<RoutingKonfigurationAntwort> Laden(CancellationToken cancellationToken);
    Task<RoutingKonfigurationAntwort> DienstSpeichern(RoutingDienstAnfrage anfrage, CancellationToken cancellationToken);
    Task<RoutingKonfigurationAntwort> AdresseSpeichern(RoutingAdresseAnfrage anfrage, CancellationToken cancellationToken);
    Task<RoutingKonfigurationAntwort> AdresseZuruecksetzen(RoutingVersionAnfrage anfrage, CancellationToken cancellationToken);
    Task<RoutingKonfigurationAntwort> OrsKeySpeichern(RoutingOrsSchluesselAnfrage anfrage, CancellationToken cancellationToken);
    Task<RoutingKonfigurationAntwort> OrsKeyEntfernen(RoutingVersionAnfrage anfrage, CancellationToken cancellationToken);
}
