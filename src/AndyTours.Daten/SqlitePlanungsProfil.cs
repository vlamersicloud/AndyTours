using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>CAS-Speicher ausschließlich für das zuletzt gewählte Planungsprofil.</summary>
public sealed class SqlitePlanungsProfil : IPlanungsProfil
{
    private readonly string _databasePath;

    public SqlitePlanungsProfil(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!Path.IsPathFullyQualified(databasePath)) throw new ArgumentException("Datenbankpfad muss absolut sein.", nameof(databasePath));
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<PlanungsProfilAntwort> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_databasePath);
        using var tx = db.BeginTransaction(deferred: true);
        var state = Read(db);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(ToResponse(state));
    }

    public Task<PlanungsProfilAntwort> Speichern(PlanungsProfilSpeichernAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null || !ValidVersion(anfrage.Version) || !Enum.IsDefined(anfrage.Profil)) throw Invalid();
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_databasePath);
        using var tx = db.BeginTransaction(deferred: false);
        var current = Read(db);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(anfrage.Version), Convert.FromHexString(current.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Planungsprofil wurde zwischenzeitlich geändert.");
        cancellationToken.ThrowIfCancellationRequested();
        EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.PlanungProfil, ToCore(anfrage.Profil).ToString());
        cancellationToken.ThrowIfCancellationRequested();
        var updated = Read(db);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(ToResponse(updated));
    }

    private static State Read(SqliteConnection db)
    {
        string? raw = EinstellungTabelle.Lesen(db, Einstellungsschluessel.PlanungProfil);
        var profile = Enum.TryParse(raw, out Fahrprofil parsed) && Enum.IsDefined(parsed)
            ? parsed : Planungseinstellungen.Standard.Profil;
        string canonical = JsonSerializer.Serialize(new { key = Einstellungsschluessel.PlanungProfil, value = raw });
        string version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new State(profile, version);
    }

    private static PlanungsProfilAntwort ToResponse(State state) => new(state.Version, ToContract(state.Profil));
    private static CourseFahrprofil ToContract(Fahrprofil profile) => profile switch
    {
        Fahrprofil.Rennrad => CourseFahrprofil.Rennrad,
        Fahrprofil.Trekking => CourseFahrprofil.Trekking,
        Fahrprofil.Mountainbike => CourseFahrprofil.Mountainbike,
        _ => throw Invalid()
    };
    private static Fahrprofil ToCore(CourseFahrprofil profile) => profile switch
    {
        CourseFahrprofil.Rennrad => Fahrprofil.Rennrad,
        CourseFahrprofil.Trekking => Fahrprofil.Trekking,
        CourseFahrprofil.Mountainbike => Fahrprofil.Mountainbike,
        _ => throw Invalid()
    };
    private static bool ValidVersion(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungsprofil ungültig.");
    private sealed record State(Fahrprofil Profil, string Version);
}
