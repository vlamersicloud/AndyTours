using System.Security.Cryptography;
using System.Text;
using AndyTours.Application.Garmin;
using AndyTours.Contracts.V1;
using AndyTours.Application.Stammdaten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>CAS-geschützter Speicher für die zwei fest erlaubten Garmin-Einstellungen.</summary>
public sealed class SqliteGarminEinstellungen(string datenbankpfad) : IGarminEinstellungen
{
    private const int MaxKeyLength = 4096;
    public Task<GarminEinstellungenAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var state = Read(db);
        ct.ThrowIfCancellationRequested(); tx.Commit();
        return Task.FromResult(ToResponse(state));
    }
    public Task<GarminEinstellungenAntwort> SchluesselSpeichern(GarminSchluesselAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Anfrage ungültig.");
        if (anfrage.Schluessel is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Schlüssel ungültig.");
        string trimmed = anfrage.Schluessel.Trim();
        if (trimmed.Length > MaxKeyLength || trimmed.Any(c => !char.IsAsciiLetterOrDigit(c) && "-._~+/=".IndexOf(c) < 0))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Schlüssel ungültig.");
        return Mutate(anfrage.Version, (db, tx) => EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, trimmed.Length == 0 ? null : trimmed), ct);
    }
    public Task<GarminEinstellungenAntwort> SchluesselEntfernen(GarminVersionAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Anfrage ungültig.");
        return Mutate(anfrage.Version, (db, tx) => EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, null), ct);
    }
    public Task<GarminEinstellungenAntwort> AutomatikSpeichern(GarminAutomatikAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Anfrage ungültig.");
        return Mutate(anfrage.Version, (db, tx) => EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminAllePaketeAutomatisch, anfrage.AllePaketeAutomatisch ? "true" : null), ct);
    }
    private Task<GarminEinstellungenAntwort> Mutate(string version, Action<SqliteConnection, SqliteTransaction> change, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var current = Read(db);
        if (!IsVersion(version)) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Version ungültig.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(version), Convert.FromHexString(current.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Garmin-Einstellungen wurden zwischenzeitlich geändert.");
        ct.ThrowIfCancellationRequested(); change(db, tx); ct.ThrowIfCancellationRequested();
        var updated = Read(db);
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(ToResponse(updated));
    }
    private static State Read(SqliteConnection db)
    {
        var key = EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminPersonalApiKey);
        var auto = EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminAllePaketeAutomatisch) == "true";
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key ?? string.Empty)));
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyHash + "|" + (auto ? "true" : "false"))));
        return new State(!string.IsNullOrWhiteSpace(key), auto, version);
    }
    private static bool IsVersion(string? version) => version is { Length: 64 } && version.All(Uri.IsHexDigit);
    private static GarminEinstellungenAntwort ToResponse(State state) => new(state.Version, state.HasKey, state.Auto);
    private sealed record State(bool HasKey, bool Auto, string Version);
}
