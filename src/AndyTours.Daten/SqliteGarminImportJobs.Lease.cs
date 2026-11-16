using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace AndyTours.Daten;

public sealed partial class SqliteGarminImportJobs
{
    public Task<GarminImportLease?> Beanspruchen(Guid besitzer, TimeSpan dauer, CancellationToken ct = default)
    {
        ValidateDuration(dauer);
        if (besitzer == Guid.Empty) throw Ungueltig("Garmin-Lease ungültig.");
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var now = _zeit.GetUtcNow();
        Guid? jobId;
        using (var select = db.CreateCommand())
        {
            select.Transaction = tx;
            select.CommandText = """
                SELECT JobId FROM GarminImportJob
                WHERE Zustand=0 OR (Zustand=1 AND (LeaseBisUtc IS NULL OR LeaseBisUtc<=$now))
                ORDER BY AktualisiertUtc LIMIT 1;
                """;
            select.Parameters.AddWithValue("$now", Format(now));
            var value = select.ExecuteScalar() as string;
            jobId = value is null ? null : Guid.Parse(value);
        }
        if (jobId is null)
        {
            ct.ThrowIfCancellationRequested();
            tx.Commit();
            return Task.FromResult<GarminImportLease?>(null);
        }

        var expiry = now.Add(dauer);
        long generation;
        using (var update = db.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = """
                UPDATE GarminImportJob
                SET Zustand=1,Revision=Revision+1,AktualisiertUtc=$updated,LeaseOwner=$owner,
                    LeaseBisUtc=$expiry,LeaseGeneration=LeaseGeneration+1
                WHERE JobId=$job AND (Zustand=0 OR (Zustand=1 AND (LeaseBisUtc IS NULL OR LeaseBisUtc<=$now)));
                """;
            update.Parameters.AddWithValue("$updated", Format(now));
            update.Parameters.AddWithValue("$owner", besitzer.ToString("D"));
            update.Parameters.AddWithValue("$expiry", Format(expiry));
            update.Parameters.AddWithValue("$job", jobId.Value.ToString("D"));
            update.Parameters.AddWithValue("$now", Format(now));
            ct.ThrowIfCancellationRequested();
            if (update.ExecuteNonQuery() != 1)
            {
                tx.Commit();
                return Task.FromResult<GarminImportLease?>(null);
            }
        }
        generation = ScalarLong(db, tx, "SELECT LeaseGeneration FROM GarminImportJob WHERE JobId=$job;", ("$job", jobId.Value.ToString("D")));
        var response = ReadJob(db, jobId.Value)!;
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult<GarminImportLease?>(new GarminImportLease(jobId.Value, besitzer, generation, expiry, response));
    }

    public Task<GarminImportLease?> Verlaengern(GarminImportLease lease, TimeSpan dauer, CancellationToken ct = default)
    {
        ValidateLease(lease);
        ValidateDuration(dauer);
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var now = _zeit.GetUtcNow();
        var expiry = now.Add(dauer);
        using var update = db.CreateCommand();
        update.Transaction = tx;
        update.CommandText = """
            UPDATE GarminImportJob SET LeaseBisUtc=$expiry
            WHERE JobId=$job AND Zustand=1 AND LeaseOwner=$owner AND LeaseGeneration=$generation
              AND LeaseBisUtc>$now;
            """;
        update.Parameters.AddWithValue("$expiry", Format(expiry));
        update.Parameters.AddWithValue("$job", lease.JobId.ToString("D"));
        update.Parameters.AddWithValue("$owner", lease.Besitzer.ToString("D"));
        update.Parameters.AddWithValue("$generation", lease.Generation);
        update.Parameters.AddWithValue("$now", Format(now));
        ct.ThrowIfCancellationRequested();
        var affected = update.ExecuteNonQuery();
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult<GarminImportLease?>(affected == 1
            ? lease with { GueltigBisUtc = expiry }
            : null);
    }

    private static void ValidateLease(GarminImportLease lease)
    {
        if (lease is null || lease.JobId == Guid.Empty || lease.Besitzer == Guid.Empty || lease.Generation <= 0)
            throw Ungueltig("Garmin-Lease ungültig.");
    }

    private static void ValidateDuration(TimeSpan dauer)
    {
        if (dauer < TimeSpan.FromSeconds(5) || dauer > TimeSpan.FromMinutes(5))
            throw Ungueltig("Garmin-Leasedauer ungültig.");
    }

}
