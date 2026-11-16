using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Server-only reader for the fixed Garmin key and current pulse reference settings.</summary>
public sealed partial class SqlitePulsQuelldaten
{
    private readonly string _datenbankpfad;

    public SqlitePulsQuelldaten(string datenbankpfad)
    {
        if (string.IsNullOrWhiteSpace(datenbankpfad) || !Path.IsPathFullyQualified(datenbankpfad))
            throw new ArgumentException("Database path must be absolute.", nameof(datenbankpfad));
        _datenbankpfad = Path.GetFullPath(datenbankpfad);
    }

    public Task<PulsReferenzServerbestand> ReferenzLaden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        cancellationToken.ThrowIfCancellationRequested();
        var result = ReadReference(db, tx);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    private static PulsReferenzServerbestand ReadReference(SqliteConnection db, SqliteTransaction tx)
    {
        var rawKey = ReadSetting(db, tx, Einstellungsschluessel.GarminPersonalApiKey);
        var rawReference = ReadSetting(db, tx, GarminPulszonenEinstellung.Schluessel);
        GarminPulszonenStand? stand = null;
        if (!string.IsNullOrWhiteSpace(rawReference))
        {
            try
            {
                stand = JsonSerializer.Deserialize<GarminPulszonenStand>(rawReference);
                if (!IstGueltigeReferenz(stand, erlaubeLeereProfile: true)) stand = null;
            }
            catch (JsonException) { }
        }

        var canonical = JsonSerializer.Serialize(new { Key = rawKey, Reference = rawReference });
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new PulsReferenzServerbestand(version, stand,
            string.IsNullOrWhiteSpace(rawKey) ? null : rawKey.Trim());
    }

    private static string? ReadSetting(SqliteConnection db, SqliteTransaction tx, string key)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static bool IstGueltigeReferenz(GarminPulszonenStand? stand, bool erlaubeLeereProfile)
    {
        if (stand?.Profile is null || (!erlaubeLeereProfile && stand.Profile.Count == 0))
            return false;
        var sports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in stand.Profile)
        {
            if (profile is null || string.IsNullOrWhiteSpace(profile.Sport) ||
                !sports.Add(profile.Sport) || profile.Untergrenzen is null ||
                profile.Untergrenzen.Count != 5 ||
                profile.Untergrenzen.Any(boundary => boundary is < 1 or > 254) ||
                profile.Untergrenzen.Zip(profile.Untergrenzen.Skip(1))
                    .Any(pair => pair.First >= pair.Second))
                return false;
        }

        return true;
    }

    private static StammdatenException Ungueltig() =>
        new(StammdatenFehlerart.Ungueltig, "Pulsreferenz ist ungültig.");

    private static StammdatenException Konflikt() =>
        new(StammdatenFehlerart.Konflikt, "Pulsreferenz wurde zwischenzeitlich geändert.");
}
