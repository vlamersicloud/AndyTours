using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteGarminImportJobs : IGarminImportArbeitspeicher
{
    public Task<GarminImportJobAntwort> PaketAbschliessen(
        GarminImportLease lease,
        bool unterbrochen,
        bool weitereMoeglich,
        IReadOnlyList<string> warncodes,
        CancellationToken ct = default)
    {
        ValidateLease(lease);
        var normalizedWarnings = ValidateWarnings(warncodes);
        ct.ThrowIfCancellationRequested();

        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var now = _zeit.GetUtcNow();
        VerifyLease(db, tx, lease, now);
        var current = ReadJob(db, lease.JobId) ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Garmin-Import wurde nicht gefunden.");
        var pending = ScalarLong(db, tx,
            "SELECT COUNT(*) FROM GarminImportKandidat WHERE JobId=$job AND Zustand=2;",
            ("$job", lease.JobId.ToString("D"))) > 0;
        var unfinished = ScalarLong(db, tx,
            "SELECT COUNT(*) FROM GarminImportKandidat WHERE JobId=$job AND Paketnummer=$packet AND Zustand IN(0,1);",
            ("$job", lease.JobId.ToString("D")), ("$packet", current.Paketnummer)) > 0;
        if (!unterbrochen && unfinished)
            throw Konflikt("Das aktuelle Garmin-Paket enthält noch nicht abgeschlossene Kandidaten.");

        GarminImportJobStatus state;
        var packet = current.Paketnummer;
        if (unterbrochen)
        {
            state = GarminImportJobStatus.Unterbrochen;
        }
        else if (current.Automatisch && weitereMoeglich)
        {
            packet = checked(packet + 1);
            state = GarminImportJobStatus.Wartend;
        }
        else if (weitereMoeglich || pending)
        {
            state = GarminImportJobStatus.PaketBereit;
        }
        else
        {
            state = GarminImportJobStatus.Fertig;
        }

        using (var update = db.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = """
                UPDATE GarminImportJob
                SET Zustand=$state,Revision=Revision+1,Paketnummer=$packet,WeitereMoeglich=$more,
                    AktualisiertUtc=$updated,WarncodesJson=$warnings,LeaseOwner=NULL,LeaseBisUtc=NULL,
                    LeaseGeneration=LeaseGeneration+1
                WHERE JobId=$job AND Zustand=1 AND LeaseOwner=$owner AND LeaseGeneration=$generation
                  AND LeaseBisUtc>$now;
                """;
            update.Parameters.AddWithValue("$state", (int)state);
            update.Parameters.AddWithValue("$packet", packet);
            update.Parameters.AddWithValue("$more", weitereMoeglich ? 1 : 0);
            update.Parameters.AddWithValue("$updated", Format(now));
            update.Parameters.AddWithValue("$warnings", System.Text.Json.JsonSerializer.Serialize(normalizedWarnings));
            update.Parameters.AddWithValue("$job", lease.JobId.ToString("D"));
            update.Parameters.AddWithValue("$owner", lease.Besitzer.ToString("D"));
            update.Parameters.AddWithValue("$generation", lease.Generation);
            update.Parameters.AddWithValue("$now", Format(now));
            ct.ThrowIfCancellationRequested();
            if (update.ExecuteNonQuery() != 1)
                throw Konflikt("Garmin-Worker-Lease ist nicht mehr gültig.");
        }

        ct.ThrowIfCancellationRequested();
        var result = ReadJob(db, lease.JobId)!;
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    private static string[] ValidateWarnings(IReadOnlyList<string> warnings)
    {
        if (warnings is null || warnings.Count > 32)
            throw Ungueltig("Garmin-Warncodes ungültig.");
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            GarminImportWarncodes.Netzwerk, GarminImportWarncodes.Zeitlimit, GarminImportWarncodes.Abgebrochen,
            GarminImportWarncodes.FitUngueltig, GarminImportWarncodes.ArchivUngueltig, GarminImportWarncodes.DateiFehler,
            GarminImportWarncodes.SchluesselFehlt, GarminImportWarncodes.RateLimit
        };
        if (warnings.Any(code => !allowed.Contains(code)))
            throw Ungueltig("Garmin-Warncodes ungültig.");
        return warnings.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
}
