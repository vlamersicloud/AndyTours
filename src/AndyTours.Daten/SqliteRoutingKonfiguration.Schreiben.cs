using System.Security.Cryptography;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

public sealed partial class SqliteRoutingKonfiguration : IRoutingKonfiguration
{
    public Task<RoutingKonfigurationAntwort> DienstSpeichern(RoutingDienstAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        RoutingKonfigurationRegeln.PruefeDienst(anfrage.Dienst);
        return Mutate(anfrage.Version, Einstellungsschluessel.RoutingDienst,
            anfrage.Dienst == RoutingAnbieter.BRouter ? Routingdienst.BRouter.ToString() : Routingdienst.Openrouteservice.ToString(), cancellationToken);
    }

    public Task<RoutingKonfigurationAntwort> AdresseSpeichern(RoutingAdresseAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        string value = RoutingKonfigurationRegeln.Adresse(anfrage.Adresse);
        return Mutate(anfrage.Version, Einstellungsschluessel.RoutingBRouterAdresse, value, cancellationToken);
    }

    public Task<RoutingKonfigurationAntwort> AdresseZuruecksetzen(RoutingVersionAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        return Mutate(anfrage.Version, Einstellungsschluessel.RoutingBRouterAdresse, null, cancellationToken);
    }

    public Task<RoutingKonfigurationAntwort> OrsKeySpeichern(RoutingOrsSchluesselAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        string value = RoutingKonfigurationRegeln.OrsSchluessel(anfrage.Schluessel);
        return Mutate(anfrage.Version, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel, value, cancellationToken);
    }

    public Task<RoutingKonfigurationAntwort> OrsKeyEntfernen(RoutingVersionAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        return Mutate(anfrage.Version, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel, null, cancellationToken);
    }

    private Task<RoutingKonfigurationAntwort> Mutate(string version, string key, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_databasePath);
        using var tx = db.BeginTransaction(deferred: false);
        var current = Read(db);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(version), Convert.FromHexString(current.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Routingkonfiguration wurde zwischenzeitlich geändert.");
        cancellationToken.ThrowIfCancellationRequested();
        EinstellungTabelle.Schreiben(db, tx, key, value);
        cancellationToken.ThrowIfCancellationRequested();
        var updated = Read(db);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(ToResponse(updated));
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Routingkonfiguration ungültig.");
}
