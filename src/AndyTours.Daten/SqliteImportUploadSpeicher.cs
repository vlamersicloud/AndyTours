using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteImportUploadSpeicher
{
    private readonly string _datenbankpfad;
    private readonly TimeProvider _zeit;

    public SqliteImportUploadSpeicher(string datenbankpfad, TimeProvider? zeit = null)
    {
        if (string.IsNullOrWhiteSpace(datenbankpfad) || !Path.IsPathFullyQualified(datenbankpfad))
            throw new ArgumentException("Datenbankpfad ungültig.", nameof(datenbankpfad));
        _datenbankpfad = Path.GetFullPath(datenbankpfad);
        _zeit = zeit ?? TimeProvider.System;
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        FitUploadSchema.Ensure(db, tx);
        tx.Commit();
    }

    public Task<FitUploadAntwort> Registrieren(Guid uploadId, string dateiname, string inhaltHash, long byteanzahl, CancellationToken cancellationToken)
    {
        ValidateId(uploadId);
        dateiname = NormalizeFilename(dateiname);
        if (inhaltHash is null || inhaltHash.Length != 64 || !inhaltHash.All(Uri.IsHexDigit))
            throw Invalid("FIT-Upload ungültig.");
        inhaltHash = inhaltHash.ToUpperInvariant();
        if (byteanzahl is < 1 or > FitUploadRegeln.MaxDateiBytes) throw Invalid("FIT-Upload ungültig.");
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var existing = Read(db, tx, uploadId);
        if (existing is not null)
        {
            if (!string.Equals(existing.Dateiname, dateiname, StringComparison.Ordinal) ||
                !string.Equals(existing.InhaltHash, inhaltHash, StringComparison.Ordinal) || existing.Byteanzahl != byteanzahl)
                throw Conflict("FIT-Uploadkennung ist bereits mit anderem Inhalt registriert.");
            cancellationToken.ThrowIfCancellationRequested();
            tx.Commit();
            return Task.FromResult(existing);
        }
        var now = Format(_zeit.GetUtcNow());
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = """
                INSERT INTO FitUpload(UploadId,Dateiname,InhaltHash,Byteanzahl,Revision,Zustand,TourId,ErstelltUtc,AktualisiertUtc)
                VALUES($id,$name,$hash,$bytes,1,0,NULL,$created,$updated);
                """;
            command.Parameters.AddWithValue("$id", uploadId.ToString("D"));
            command.Parameters.AddWithValue("$name", dateiname);
            command.Parameters.AddWithValue("$hash", inhaltHash);
            command.Parameters.AddWithValue("$bytes", byteanzahl);
            command.Parameters.AddWithValue("$created", now);
            command.Parameters.AddWithValue("$updated", now);
            cancellationToken.ThrowIfCancellationRequested();
            command.ExecuteNonQuery();
        }
        var result = Read(db, tx, uploadId)!;
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    public Task<FitUploadAntwort?> Laden(Guid uploadId, CancellationToken cancellationToken)
    {
        ValidateId(uploadId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var result = Read(db, tx, uploadId);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    public Task<FitUploadListeAntwort> Liste(int start, int anzahl, CancellationToken cancellationToken)
    {
        if (start < 0 || anzahl is < 1 or > 200) throw Invalid("FIT-Uploadbereich ungültig.");
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var items = new List<FitUploadAntwort>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "SELECT UploadId FROM FitUpload ORDER BY ErstelltUtc DESC,UploadId LIMIT $count OFFSET $start;";
            command.Parameters.AddWithValue("$count", anzahl);
            command.Parameters.AddWithValue("$start", start);
            using var reader = command.ExecuteReader();
            var ids = new List<Guid>();
            while (reader.Read()) ids.Add(Guid.Parse(reader.GetString(0)));
            reader.Close();
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = Read(db, tx, id);
                if (item is not null) items.Add(item);
            }
        }
        var total = checked((int)ScalarLong(db, tx, "SELECT COUNT(*) FROM FitUpload;"));
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(new FitUploadListeAntwort(start, total, items));
    }

    private static FitUploadAntwort? Read(SqliteConnection db, SqliteTransaction tx, Guid id)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            SELECT u.UploadId,u.Revision,u.Dateiname,u.InhaltHash,u.Byteanzahl,u.Zustand,
                   CASE WHEN t.Id IS NULL THEN NULL ELSE u.TourId END,
                   u.ErstelltUtc,u.AktualisiertUtc
            FROM FitUpload u LEFT JOIN Trainingseinheit t ON t.Id=u.TourId
            WHERE u.UploadId=$id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        var version = Version(Guid.Parse(reader.GetString(0)), reader.GetInt64(1), reader.GetString(2),
            reader.GetString(3), reader.GetInt64(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetInt64(6),
            reader.GetString(7), reader.GetString(8));
        var persisted = (FitUploadStatus)reader.GetInt32(5);
        long? tourId = reader.IsDBNull(6) ? null : reader.GetInt64(6);
        var state = persisted == FitUploadStatus.Importiert && tourId is null ? FitUploadStatus.Hochgeladen : persisted;
        return new FitUploadAntwort(Guid.Parse(reader.GetString(0)), version, reader.GetString(2), reader.GetString(3),
            reader.GetInt64(4), state, tourId, DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture));
    }

    private static string NormalizeFilename(string value) => FitUploadRegeln.Dateiname(value);

    private static long ScalarLong(SqliteConnection db, SqliteTransaction tx, string sql)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string Version(Guid id, long revision, string name, string hash, long bytes, int state, long? tourId, string created, string updated)
    {
        var effectiveState = state == (int)FitUploadStatus.Importiert && tourId is null ? (int)FitUploadStatus.Hochgeladen : state;
        var payload = string.Join("|", id.ToString("D"), revision.ToString(CultureInfo.InvariantCulture), name, hash,
            bytes.ToString(CultureInfo.InvariantCulture), effectiveState.ToString(CultureInfo.InvariantCulture),
            tourId?.ToString(CultureInfo.InvariantCulture) ?? "", created, updated);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw Invalid("FIT-Uploadkennung ungültig."); }
    private static StammdatenException Invalid(string message) => new(StammdatenFehlerart.Ungueltig, message);
    private static StammdatenException Conflict(string message) => new(StammdatenFehlerart.Konflikt, message);
}
