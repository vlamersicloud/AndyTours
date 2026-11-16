using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Read-side state and fixed-key helpers for routing configuration mutations.</summary>
public sealed partial class SqliteRoutingKonfiguration
{
    private readonly string _databasePath;

    public SqliteRoutingKonfiguration(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!Path.IsPathFullyQualified(databasePath)) throw new ArgumentException("Datenbankpfad muss absolut sein.", nameof(databasePath));
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<RoutingKonfigurationAntwort> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_databasePath);
        using var tx = db.BeginTransaction(deferred: true);
        var state = Read(db);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(ToResponse(state));
    }

    private static State Read(SqliteConnection db)
    {
        string? rawDienst = EinstellungTabelle.Lesen(db, Einstellungsschluessel.RoutingDienst);
        string? rawAdresse = EinstellungTabelle.Lesen(db, Einstellungsschluessel.RoutingBRouterAdresse);
        string? rawKey = EinstellungTabelle.Lesen(db, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel);
        var dienst = Enum.TryParse(rawDienst, out Routingdienst parsed) && Enum.IsDefined(parsed)
            ? parsed : Routingeinstellungen.Standard.Dienst;
        string adresse = string.IsNullOrWhiteSpace(rawAdresse) ? Routingeinstellungen.Standard.BRouterAdresse : rawAdresse;
        var versionJson = JsonSerializer.Serialize(new
        {
            Dienst = rawDienst,
            Adresse = rawAdresse,
            Schluessel = rawKey
        });
        string version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(versionJson)));
        return new State(dienst == Routingdienst.BRouter ? RoutingAnbieter.BRouter : RoutingAnbieter.Openrouteservice,
            adresse, rawKey, version);
    }

    private static RoutingKonfigurationAntwort ToResponse(State state)
    {
        bool visible = RoutingKonfigurationRegeln.IstAdresseSichtbar(state.RawAdresse);
        return new RoutingKonfigurationAntwort(state.Version, state.Dienst,
            visible ? state.RawAdresse : null, AdresseAusgeblendet: !visible,
            HatOrsSchluessel: !string.IsNullOrWhiteSpace(state.RawKey));
    }

    private sealed record State(RoutingAnbieter Dienst, string RawAdresse, string? RawKey, string Version)
    {
        public override string ToString() => "RoutingKonfigurationState { [REDACTED] }";
    }
}
