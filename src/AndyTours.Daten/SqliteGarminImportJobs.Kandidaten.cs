using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteGarminImportJobs
{
    public Task<IReadOnlyList<GarminImportBeobachtung>> BeobachteteKandidaten(
        GarminImportLease lease, CancellationToken ct = default)
    {
        ValidateLease(lease);
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        VerifyLease(db, tx, lease, _zeit.GetUtcNow());
        var result = ReadCandidates(db, tx, lease.JobId);
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult<IReadOnlyList<GarminImportBeobachtung>>(result);
    }

    public Task<GarminImportKandidatAntwort> DownloadKandidatSpeichern(
        GarminImportLease lease, GarminImportKandidatAntwort kandidat, CancellationToken ct = default)
    {
        ValidateLease(lease);
        ValidateCandidate(kandidat);
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var now = _zeit.GetUtcNow();
        VerifyLease(db, tx, lease, now);

        ExistingCandidate? existing = FindByActivity(db, tx, lease.JobId, kandidat.GarminAktivitaetId);
        if (existing is not null)
        {
            if (existing.Id != kandidat.KandidatId)
                throw Konflikt("Garmin-Kandidatkennung stimmt nicht überein.");
            if (existing.Status is GarminImportKandidatStatus.FitBereit or GarminImportKandidatStatus.Importiert or GarminImportKandidatStatus.Uebersprungen)
            {
                tx.Commit();
                return Task.FromResult(existing.ToResponse());
            }
            if (!CanAdvance(existing.Status, kandidat.Status))
            {
                tx.Commit();
                return Task.FromResult(existing.ToResponse());
            }
            ct.ThrowIfCancellationRequested();
            UpdateCandidate(db, tx, lease.JobId, existing.Paketnummer, existing.Id, kandidat);
        }
        else
        {
            if (FindById(db, tx, kandidat.KandidatId) is not null)
                throw Konflikt("Garmin-Kandidatkennung ist bereits vergeben.");
            var packet = Convert.ToInt32(ScalarLong(db, tx, "SELECT Paketnummer FROM GarminImportJob WHERE JobId=$job;", ("$job", lease.JobId.ToString("D"))), System.Globalization.CultureInfo.InvariantCulture);
            var count = ScalarLong(db, tx, "SELECT COUNT(*) FROM GarminImportKandidat WHERE JobId=$job AND Paketnummer=$packet;",
                ("$job", lease.JobId.ToString("D")), ("$packet", packet));
            if (count >= 20) throw Konflikt("Das Garmin-Paket ist vollständig.");
            ct.ThrowIfCancellationRequested();
            InsertCandidate(db, tx, lease.JobId, packet, kandidat);
        }

        using (var updateJob = db.CreateCommand())
        {
            updateJob.Transaction = tx;
            updateJob.CommandText = "UPDATE GarminImportJob SET Revision=Revision+1,AktualisiertUtc=$now WHERE JobId=$job;";
            updateJob.Parameters.AddWithValue("$now", Format(now));
            updateJob.Parameters.AddWithValue("$job", lease.JobId.ToString("D"));
            ct.ThrowIfCancellationRequested();
            updateJob.ExecuteNonQuery();
        }
        ct.ThrowIfCancellationRequested();
        var saved = FindByActivity(db, tx, lease.JobId, kandidat.GarminAktivitaetId)!.ToResponse();
        tx.Commit();
        return Task.FromResult(saved);
    }

    private static void VerifyLease(SqliteConnection db, SqliteTransaction tx, GarminImportLease lease, DateTimeOffset now)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            SELECT 1 FROM GarminImportJob
            WHERE JobId=$job AND Zustand=1 AND LeaseOwner=$owner AND LeaseGeneration=$generation AND LeaseBisUtc>$now;
            """;
        command.Parameters.AddWithValue("$job", lease.JobId.ToString("D"));
        command.Parameters.AddWithValue("$owner", lease.Besitzer.ToString("D"));
        command.Parameters.AddWithValue("$generation", lease.Generation);
        command.Parameters.AddWithValue("$now", Format(now));
        if (command.ExecuteScalar() is null) throw Konflikt("Garmin-Worker-Lease ist nicht mehr gültig.");
    }

    private static List<GarminImportBeobachtung> ReadCandidates(SqliteConnection db, SqliteTransaction tx, Guid jobId)
    {
        var items = new List<GarminImportBeobachtung>();
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            SELECT Paketnummer,KandidatId,GarminAktivitaetId,Zustand,Datum,Name,DistanzKm,FahrzeitSekunden,Warncode
            FROM GarminImportKandidat WHERE JobId=$job ORDER BY Paketnummer,GarminAktivitaetId;
            """;
        command.Parameters.AddWithValue("$job", jobId.ToString("D"));
        using var reader = command.ExecuteReader();
        while (reader.Read())
            items.Add(new GarminImportBeobachtung(reader.GetInt32(0),
                new GarminImportKandidatAntwort(Guid.Parse(reader.GetString(1)), reader.GetInt64(2),
                (GarminImportKandidatStatus)reader.GetInt32(3), reader.IsDBNull(4) ? null : DateOnly.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
                reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetDouble(6),
                reader.IsDBNull(7) ? null : reader.GetInt64(7), reader.IsDBNull(8) ? null : reader.GetString(8))));
        return items;
    }

    private static ExistingCandidate? FindByActivity(SqliteConnection db, SqliteTransaction tx, Guid jobId, long activityId)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT KandidatId,GarminAktivitaetId,Paketnummer,Zustand,Datum,Name,DistanzKm,FahrzeitSekunden,Warncode FROM GarminImportKandidat WHERE JobId=$job AND GarminAktivitaetId=$activity;";
        command.Parameters.AddWithValue("$job", jobId.ToString("D"));
        command.Parameters.AddWithValue("$activity", activityId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadCandidate(reader) : null;
    }

    private static ExistingCandidate? FindById(SqliteConnection db, SqliteTransaction tx, Guid candidateId)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT KandidatId,GarminAktivitaetId,Paketnummer,Zustand,Datum,Name,DistanzKm,FahrzeitSekunden,Warncode FROM GarminImportKandidat WHERE KandidatId=$id;";
        command.Parameters.AddWithValue("$id", candidateId.ToString("D"));
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadCandidate(reader) : null;
    }

    private static ExistingCandidate ReadCandidate(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), reader.GetInt64(1), reader.GetInt32(2), (GarminImportKandidatStatus)reader.GetInt32(3),
            reader.IsDBNull(4) ? null : DateOnly.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
            reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetDouble(6),
            reader.IsDBNull(7) ? null : reader.GetInt64(7), reader.IsDBNull(8) ? null : reader.GetString(8));

    private static void InsertCandidate(SqliteConnection db, SqliteTransaction tx, Guid jobId, int packet, GarminImportKandidatAntwort candidate)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO GarminImportKandidat
            (KandidatId,JobId,GarminAktivitaetId,Paketnummer,Zustand,Datum,Name,DistanzKm,FahrzeitSekunden,Warncode)
            VALUES($id,$job,$activity,$packet,$state,$date,$name,$distance,$seconds,$warning);
            """;
        BindCandidate(command, jobId, packet, candidate);
        command.ExecuteNonQuery();
    }

    private static void UpdateCandidate(SqliteConnection db, SqliteTransaction tx, Guid jobId, int packet, Guid candidateId, GarminImportKandidatAntwort candidate)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            UPDATE GarminImportKandidat SET Zustand=$state,Datum=$date,Name=$name,DistanzKm=$distance,FahrzeitSekunden=$seconds,Warncode=$warning
            WHERE JobId=$job AND GarminAktivitaetId=$activity;
            """;
        BindCandidate(command, jobId, packet, candidate);
        command.ExecuteNonQuery();
    }

    private static void BindCandidate(SqliteCommand command, Guid jobId, int packet, GarminImportKandidatAntwort candidate)
    {
        command.Parameters.AddWithValue("$id", candidate.KandidatId.ToString("D"));
        command.Parameters.AddWithValue("$job", jobId.ToString("D"));
        command.Parameters.AddWithValue("$activity", candidate.GarminAktivitaetId);
        command.Parameters.AddWithValue("$packet", packet);
        command.Parameters.AddWithValue("$state", (int)candidate.Status);
        command.Parameters.AddWithValue("$date", candidate.Datum is null ? DBNull.Value : candidate.Datum.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$name", candidate.Name);
        command.Parameters.AddWithValue("$distance", candidate.DistanzKm is null ? DBNull.Value : candidate.DistanzKm.Value);
        command.Parameters.AddWithValue("$seconds", candidate.FahrzeitSekunden is null ? DBNull.Value : candidate.FahrzeitSekunden.Value);
        command.Parameters.AddWithValue("$warning", candidate.Warncode is null ? DBNull.Value : candidate.Warncode);
    }

    private static void ValidateCandidate(GarminImportKandidatAntwort candidate)
    {
        if (candidate is null || candidate.KandidatId == Guid.Empty || candidate.GarminAktivitaetId <= 0 ||
            !Enum.IsDefined(candidate.Status) || candidate.Status == GarminImportKandidatStatus.Importiert ||
            candidate.Name is null || candidate.Name.Length > 512 || candidate.Name.Any(char.IsControl) ||
            candidate.DistanzKm is double distance && (!double.IsFinite(distance) || distance < 0) ||
            candidate.FahrzeitSekunden < 0 || candidate.Warncode is not null && !IsAllowedWarning(candidate.Warncode))
            throw Ungueltig("Garmin-Kandidat ungültig.");
    }

    private static bool CanAdvance(GarminImportKandidatStatus current, GarminImportKandidatStatus next)
    {
        if (current is GarminImportKandidatStatus.FitBereit or GarminImportKandidatStatus.Importiert or GarminImportKandidatStatus.Uebersprungen)
            return false;
        return current switch
        {
            GarminImportKandidatStatus.Wartend => next is GarminImportKandidatStatus.Wartend or GarminImportKandidatStatus.ZipBereit or GarminImportKandidatStatus.FitBereit or GarminImportKandidatStatus.Fehler or GarminImportKandidatStatus.Uebersprungen,
            GarminImportKandidatStatus.ZipBereit => next is GarminImportKandidatStatus.ZipBereit or GarminImportKandidatStatus.FitBereit or GarminImportKandidatStatus.Fehler or GarminImportKandidatStatus.Uebersprungen,
            GarminImportKandidatStatus.Fehler => next is GarminImportKandidatStatus.Fehler or GarminImportKandidatStatus.Wartend or GarminImportKandidatStatus.ZipBereit or GarminImportKandidatStatus.FitBereit or GarminImportKandidatStatus.Uebersprungen,
            _ => false
        };
    }

    private static bool IsAllowedWarning(string warning) =>
        warning is GarminImportWarncodes.Netzwerk or GarminImportWarncodes.Zeitlimit or GarminImportWarncodes.Abgebrochen or
            GarminImportWarncodes.FitUngueltig or GarminImportWarncodes.ArchivUngueltig or GarminImportWarncodes.DateiFehler or
            GarminImportWarncodes.SchluesselFehlt or GarminImportWarncodes.RateLimit;

    private sealed record ExistingCandidate(Guid Id, long GarminAktivitaetId, int Paketnummer, GarminImportKandidatStatus Status, DateOnly? Datum,
        string Name, double? Distanz, long? Seconds, string? Warning)
    {
        public GarminImportKandidatAntwort ToResponse() => new(Id, GarminAktivitaetId, Status, Datum, Name, Distanz, Seconds, Warning);
    }
}
