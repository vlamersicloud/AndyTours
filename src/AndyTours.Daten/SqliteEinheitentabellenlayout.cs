using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Tabellenlayout;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Daten.Datensicherung;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed class SqliteEinheitentabellenlayout : IEinheitentabellenlayout
{
    private readonly string _path;
    public SqliteEinheitentabellenlayout(string path) => _path = Path.GetFullPath(path);

    public Task<EinheitentabellenlayoutAntwort> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var db = Open(SqliteOpenMode.ReadOnly);
            using var tx = db.BeginTransaction(deferred: true);
            var result = Read(db, tx);
            cancellationToken.ThrowIfCancellationRequested(); tx.Commit();
            return Task.FromResult(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) when (IsUnavailable(e)) { throw new EinheitentabellenlayoutNichtVerfuegbarException(); }
    }

    public Task<EinheitentabellenlayoutAntwort> Speichern(EinheitentabellenlayoutAnfrage anfrage, CancellationToken cancellationToken)
    {
        EinheitentabellenlayoutRegeln.Pruefe(anfrage);
        var snapshot = new EinheitentabellenlayoutAnfrage(anfrage.Version,
            Array.AsReadOnly(anfrage.Anordnung.Select(x => new Einheitentabellenspalte(x.Schluessel, x.Breite)).ToArray()),
            Array.AsReadOnly(anfrage.Ausgeblendet.ToArray()));
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var db = Open(SqliteOpenMode.ReadWrite);
            using var tx = db.BeginTransaction(deferred: false);
            var current = Read(db, tx);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(snapshot.Version), Convert.FromHexString(current.Version)))
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Tabellenlayout wurde zwischenzeitlich geändert.");
            cancellationToken.ThrowIfCancellationRequested();
            var order = snapshot.Anordnung.Select(x => x.Schluessel + ":" + (x.Breite is double width ? Math.Round(width, 1).ToString(CultureInfo.InvariantCulture) : string.Empty));
            Write(db, tx, Einstellungsschluessel.Spaltenanordnung, string.Join(",", order));
            var hidden = snapshot.Ausgeblendet.Count == 0 ? null : string.Join(",", snapshot.Ausgeblendet.Order().Select(x => x.ToString()));
            Write(db, tx, Einstellungsschluessel.AusgeblendeteSpalten, hidden);
            cancellationToken.ThrowIfCancellationRequested();
            var updated = Read(db, tx);
            tx.Commit();
            return Task.FromResult(updated);
        }
        catch (StammdatenException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) when (IsUnavailable(e)) { throw new EinheitentabellenlayoutNichtVerfuegbarException(); }
    }

    private SqliteConnection Open(SqliteOpenMode mode)
    {
        var db = new SqliteConnection(Sicherungsdatei.Verbindungstext(_path, mode));
        try { db.Open(); return db; }
        catch { db.Dispose(); throw; }
    }

    private static bool IsUnavailable(Exception e) => e is SqliteException or IOException or UnauthorizedAccessException;

    private static EinheitentabellenlayoutAntwort Read(SqliteConnection db, SqliteTransaction tx)
    {
        var rawOrder = Raw(db, tx, Einstellungsschluessel.Spaltenanordnung);
        var rawHidden = Raw(db, tx, Einstellungsschluessel.AusgeblendeteSpalten);
        var parsed = new List<Spalteneinstellung>();
        foreach (var token in (rawOrder.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':');
            if (int.TryParse(parts[0], out _) || !Enum.TryParse(parts[0], false, out Einheitenspalte key) || !Enum.IsDefined(key)) continue;
            double? width = parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
            parsed.Add(new Spalteneinstellung(key, width));
        }
        var order = VervollstaendigeSpaltenanordnung.Ausfuehren(parsed).Select(x => new Einheitentabellenspalte(Enum.Parse<Einheitentabellenschluessel>(x.Spalte.ToString(), false), x.Breite)).ToArray();
        var hiddenSet = new HashSet<Einheitenspalte>();
        foreach (var name in (rawHidden.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!int.TryParse(name, out _) && Enum.TryParse(name, false, out Einheitenspalte parsedKey) && Enum.IsDefined(parsedKey)) hiddenSet.Add(parsedKey);
        if (hiddenSet.Count >= Enum.GetValues<Einheitenspalte>().Length) hiddenSet.Clear();
        var hidden = hiddenSet.Select(x => Enum.Parse<Einheitentabellenschluessel>(x.ToString(), false)).ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { OrderExists = rawOrder.Exists, Order = rawOrder.Value, HiddenExists = rawHidden.Exists, Hidden = rawHidden.Value });
        var version = Convert.ToHexString(SHA256.HashData(bytes));
        return new EinheitentabellenlayoutAntwort(version, Array.AsReadOnly(order), Array.AsReadOnly(hidden));
    }

    private static void Write(SqliteConnection db, SqliteTransaction tx, string key, string? value)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = value is null ? "DELETE FROM Einstellung WHERE Schluessel = $key;" : "INSERT OR REPLACE INTO Einstellung (Schluessel, Wert) VALUES ($key, $value);";
        cmd.Parameters.AddWithValue("$key", key);
        if (value is not null) cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    private readonly record struct RawValue(bool Exists, string? Value);
    private static RawValue Raw(SqliteConnection db, SqliteTransaction tx, string key)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel = $key;"; cmd.Parameters.AddWithValue("$key", key);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return new RawValue(false, null);
        return new RawValue(true, reader.IsDBNull(0) ? null : reader.GetString(0));
    }
}
