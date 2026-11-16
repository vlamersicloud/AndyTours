using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteGarminImportJobs
{
    private readonly string _datenbankpfad;
    private readonly TimeProvider _zeit;

    public SqliteGarminImportJobs(string datenbankpfad, TimeProvider? zeit = null)
    {
        _datenbankpfad = datenbankpfad;
        _zeit = zeit ?? TimeProvider.System;
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        GarminImportJobSchema.Ensure(db, tx);
        tx.Commit();
    }

    public Task<GarminImportJobAntwort> Starten(GarminImportStartAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null || anfrage.AnfrageId == Guid.Empty) throw Ungueltig("Garmin-Importanfrage ungültig.");
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        using (var replay = db.CreateCommand())
        {
            replay.Transaction = tx;
            replay.CommandText = "SELECT JobId FROM GarminImportJob WHERE StartAnfrageId=$id;";
            replay.Parameters.AddWithValue("$id", anfrage.AnfrageId.ToString("D"));
            var found = replay.ExecuteScalar() as string;
            if (found is not null)
            {
                var prior = ReadJob(db, Guid.Parse(found));
                tx.Commit();
                return Task.FromResult(prior!);
            }
        }

        if (ScalarLong(db, tx, "SELECT COUNT(*) FROM GarminImportJob WHERE Zustand BETWEEN 0 AND 3;") > 0)
            throw Konflikt("Ein Garmin-Import ist bereits aktiv.");
        if (string.IsNullOrWhiteSpace(EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminPersonalApiKey)))
            throw Ungueltig("Garmin-Schlüssel ist nicht konfiguriert.");
        if (ScalarLong(db, tx, "SELECT COUNT(*) FROM Aktivitaet WHERE IstHaupt<>0;") == 0)
            throw Ungueltig("Es ist keine Hauptaktivität konfiguriert.");

        var now = Format(_zeit.GetUtcNow());
        var id = Guid.NewGuid();
        var auto = EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminAllePaketeAutomatisch) == "true";
        using (var insert = db.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO GarminImportJob
                (JobId,StartAnfrageId,Zustand,Revision,Paketnummer,WeitereMoeglich,Automatisch,ErstelltUtc,AktualisiertUtc,WarncodesJson,LeaseGeneration)
                VALUES ($job,$request,0,1,1,1,$auto,$created,$updated,'[]',0);
                """;
            insert.Parameters.AddWithValue("$job", id.ToString("D"));
            insert.Parameters.AddWithValue("$request", anfrage.AnfrageId.ToString("D"));
            insert.Parameters.AddWithValue("$auto", auto ? 1 : 0);
            insert.Parameters.AddWithValue("$created", now);
            insert.Parameters.AddWithValue("$updated", now);
            ct.ThrowIfCancellationRequested();
            try { insert.ExecuteNonQuery(); }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067) { throw Konflikt("Ein Garmin-Import ist bereits aktiv."); }
        }

        ct.ThrowIfCancellationRequested();
        var result = ReadJob(db, id)!;
        tx.Commit();
        return Task.FromResult(result);
    }

    public Task<GarminImportJobAntwort?> Laden(Guid jobId, CancellationToken ct = default)
    {
        ValidateId(jobId);
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var result = ReadJob(db, jobId);
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    public Task<GarminImportJobAntwort?> AktuellenLaden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        Guid? id;
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "SELECT JobId FROM GarminImportJob WHERE Zustand<>5 ORDER BY CASE WHEN Zustand BETWEEN 0 AND 3 THEN 0 ELSE 1 END,AktualisiertUtc DESC LIMIT 1;";
            var value = command.ExecuteScalar() as string;
            id = value is null ? null : Guid.Parse(value);
        }
        var result = id is null ? null : ReadJob(db, id.Value);
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    public Task<GarminImportKandidatenAntwort> Kandidaten(Guid jobId, int start, int anzahl, CancellationToken ct = default)
    {
        ValidateId(jobId);
        if (start < 0 || anzahl is < 1 or > 200) throw Ungueltig("Garmin-Kandidatenbereich ungültig.");
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        if (ReadJob(db, jobId) is null)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Garmin-Import wurde nicht gefunden.");
        var rows = new List<GarminImportKandidatAntwort>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = """
                SELECT KandidatId,GarminAktivitaetId,Zustand,Datum,Name,DistanzKm,FahrzeitSekunden,Warncode
                FROM GarminImportKandidat WHERE JobId=$job ORDER BY Paketnummer,GarminAktivitaetId LIMIT $count OFFSET $start;
                """;
            command.Parameters.AddWithValue("$job", jobId.ToString("D"));
            command.Parameters.AddWithValue("$count", anzahl);
            command.Parameters.AddWithValue("$start", start);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new GarminImportKandidatAntwort(Guid.Parse(reader.GetString(0)), reader.GetInt64(1),
                    (GarminImportKandidatStatus)reader.GetInt32(2), reader.IsDBNull(3) ? null : DateOnly.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                    reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetDouble(5),
                    reader.IsDBNull(6) ? null : reader.GetInt64(6), reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }
        var total = checked((int)ScalarLong(db, tx, "SELECT COUNT(*) FROM GarminImportKandidat WHERE JobId=$job;", ("$job", jobId.ToString("D"))));
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(new GarminImportKandidatenAntwort(jobId, start, total, rows));
    }

    private static GarminImportJobAntwort? ReadJob(SqliteConnection db, Guid id)
    {
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT j.JobId,j.Revision,j.Zustand,j.Paketnummer,j.WeitereMoeglich,j.Automatisch,j.ErstelltUtc,j.AktualisiertUtc,j.WarncodesJson,
            (SELECT COUNT(*) FROM GarminImportKandidat c WHERE c.JobId=j.JobId AND c.Zustand<>0),
            (SELECT COUNT(*) FROM GarminImportKandidat c WHERE c.JobId=j.JobId AND c.Zustand=2),
            (SELECT COUNT(*) FROM GarminImportKandidat c WHERE c.JobId=j.JobId AND c.Zustand=3)
            FROM GarminImportJob j WHERE j.JobId=$id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        var revision = reader.GetInt64(1);
        var warnings = JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [];
        return new GarminImportJobAntwort(Guid.Parse(reader.GetString(0)), Version(id, revision),
            (GarminImportJobStatus)reader.GetInt32(2), reader.GetInt32(3), checked((int)reader.GetInt64(9)),
            checked((int)reader.GetInt64(10)), checked((int)reader.GetInt64(11)), reader.GetBoolean(4), reader.GetBoolean(5),
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture), DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture), warnings);
    }

    private static long ScalarLong(SqliteConnection db, SqliteTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Version(Guid id, long revision) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id:D}|{revision}")));
    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw Ungueltig("Garmin-Importkennung ungültig."); }
    private static StammdatenException Ungueltig(string message) => new(StammdatenFehlerart.Ungueltig, message);
    private static StammdatenException Konflikt(string message) => new(StammdatenFehlerart.Konflikt, message);
}
