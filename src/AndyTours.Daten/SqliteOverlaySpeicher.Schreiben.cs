using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteOverlaySpeicher
{
    private const string FehlerUngueltig = "Overlay-Anfrage ungültig.";
    private const string FehlerKonflikt = "Overlay-Bestand wurde geändert; bitte neu laden.";

    private static void PruefeVersion(string version)
    {
        if (version is not { Length: 64 } || !version.All(Uri.IsHexDigit))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
    }

    private static void PruefeId(long id)
    {
        if (id <= 0) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
    }

    private static void PruefeOptionaleId(long? id)
    {
        if (id is <= 0) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
    }

    private static string NormalisiereName(string name)
    {
        if (name is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        string normalized;
        try { normalized = name.Trim().Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig); }
        if (normalized.Length == 0 || normalized.Any(char.IsControl))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        return normalized;
    }

    private static void PruefeFarbe(AndyTours.Contracts.V1.OverlayFarbe? color)
    {
        if (color is not null && !Enum.IsDefined(color.Value))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
    }

    private OverlayBestandAntwort ExecuteMutation(string version, Action<SqliteConnection, SqliteTransaction, OverlayBestandAntwort> mutation, CancellationToken ct)
    {
        PruefeVersion(version);
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        ct.ThrowIfCancellationRequested();
        var before = ReadBestand(db, tx, ct);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(before.Version), Convert.FromHexString(version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, FehlerKonflikt);
        mutation(db, tx, before);
        ct.ThrowIfCancellationRequested();
        var after = ReadBestand(db, tx, ct);
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return after;
    }

    private static int FuehreAus(SqliteConnection db, SqliteTransaction tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        return command.ExecuteNonQuery();
    }

    private static void EnsureReceiptSchema(SqliteConnection db, SqliteTransaction tx) =>
        FuehreAus(db, tx, """
            CREATE TABLE IF NOT EXISTS OverlayQuittung (
                AnfrageId TEXT PRIMARY KEY,
                Art TEXT NOT NULL,
                AnfrageHash TEXT NOT NULL,
                AntwortJson TEXT NOT NULL
            );
            """);

    private static OverlayAnlegenAntwort? ReadCreationReceipt(
        SqliteConnection db, SqliteTransaction tx, Guid requestId, string art, string requestHash)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT Art, AnfrageHash, AntwortJson FROM OverlayQuittung WHERE AnfrageId = $id;";
        command.Parameters.AddWithValue("$id", requestId.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        string storedHash = reader.GetString(1);
        if (!string.Equals(reader.GetString(0), art, StringComparison.Ordinal) ||
            storedHash is not { Length: 64 } || !storedHash.All(Uri.IsHexDigit) ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(storedHash), Convert.FromHexString(requestHash)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, FehlerKonflikt);
        try
        {
            var response = JsonSerializer.Deserialize<OverlayAnlegenAntwort>(reader.GetString(2));
            if (response?.Id <= 0 || response.Bestand is null ||
                response.Bestand.Version is not { Length: 64 } || !response.Bestand.Version.All(Uri.IsHexDigit) ||
                response.Bestand.Ordner is null || response.Bestand.Overlays is null)
                throw new JsonException();
            return response;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new StammdatenException(StammdatenFehlerart.Konflikt, FehlerKonflikt);
        }
    }

    private static void WriteCreationReceipt(
        SqliteConnection db, SqliteTransaction tx, Guid requestId, string art, string requestHash, OverlayAnlegenAntwort response)
    {
        FuehreAus(db, tx,
            "INSERT INTO OverlayQuittung (AnfrageId, Art, AnfrageHash, AntwortJson) VALUES ($id, $art, $hash, $json);",
            ("$id", requestId.ToString("D")), ("$art", art), ("$hash", requestHash),
            ("$json", JsonSerializer.Serialize(response)));
    }

    private static string CreationHash<T>(string art, T request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(art + "|" + JsonSerializer.Serialize(request))));
}
