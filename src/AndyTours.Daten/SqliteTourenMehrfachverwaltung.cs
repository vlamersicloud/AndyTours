using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>SQLite-Speicher für atomare Touren-Mehrfachoperationen mit idempotenten Receipts.</summary>
/// <remarks>Operationen verwenden eine gemeinsame caller-owned Transaktion; Receipts überleben das Löschen ihrer Touren.</remarks>
public sealed partial class SqliteTourenMehrfachverwaltung
{
    private readonly string _databasePath;

    /// <summary>Erstellt den Speicher mit einem festen absoluten Datenbankpfad, ohne die Datenbank zu öffnen.</summary>
    public SqliteTourenMehrfachverwaltung(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!Path.IsPathFullyQualified(databasePath))
            throw new ArgumentException("Datenbankpfad muss absolut sein.", nameof(databasePath));
        _databasePath = Path.GetFullPath(databasePath);
    }

    private static string Hash<TRequest>(string operation, TRequest normalizedRequest)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new { Operation = operation, Anfrage = normalizedRequest });
        return Convert.ToHexString(SHA256.HashData(canonical));
    }

    private static TourenMehrfachAntwort? LeseQuittung(
        SqliteConnection connection, SqliteTransaction transaction, Guid anfrageId, string erwarteterHash)
    {
        string? gespeicherterHash = null;
        string? antwortJson = null;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT AnfrageHash, AntwortJson
                FROM ApiTourMehrfach
                WHERE AnfrageId = $AnfrageId;
                """;
            command.Parameters.AddWithValue("$AnfrageId", anfrageId.ToString("D"));
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            gespeicherterHash = reader.GetString(0);
            antwortJson = reader.GetString(1);
        }

        if (!HashGleich(gespeicherterHash, erwarteterHash))
            throw new TourVersionskonfliktException();

        try
        {
            var antwort = JsonSerializer.Deserialize<TourenMehrfachAntwort>(antwortJson!);
            if (antwort is null || antwort.AnfrageId != anfrageId ||
                antwort.Eintraege is null || antwort.GeloeschteIds is null ||
                (antwort.Eintraege.Count > 0 && antwort.GeloeschteIds.Count > 0) ||
                antwort.Eintraege.Any(e => e is null || e.Id <= 0 || e.Werte is null) ||
                antwort.GeloeschteIds.Any(id => id <= 0))
                throw UngueltigeQuittung();
            return antwort;
        }
        catch (JsonException)
        {
            throw UngueltigeQuittung();
        }
    }

    private static void SchreibeQuittung(
        SqliteConnection connection, SqliteTransaction transaction, Guid anfrageId,
        string anfrageHash, TourenMehrfachAntwort antwort)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ApiTourMehrfach (AnfrageId, AnfrageHash, AntwortJson)
            VALUES ($AnfrageId, $AnfrageHash, $AntwortJson);
            """;
        command.Parameters.AddWithValue("$AnfrageId", anfrageId.ToString("D"));
        command.Parameters.AddWithValue("$AnfrageHash", anfrageHash);
        command.Parameters.AddWithValue("$AntwortJson", JsonSerializer.Serialize(antwort));
        command.ExecuteNonQuery();
    }

    private static bool HashGleich(string? gespeicherter, string? erwartet)
    {
        if (!HashGueltig(gespeicherter) || !HashGueltig(erwartet)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(gespeicherter!), Convert.FromHexString(erwartet!));
    }

    private static bool HashGueltig(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static InvalidOperationException UngueltigeQuittung() =>
        new("Gespeicherte Mehrfachquittung ist ungültig.");
}
