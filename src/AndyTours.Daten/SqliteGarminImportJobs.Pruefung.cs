using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteGarminImportJobs
{
    public Task<GarminImportPruefbestand> Pruefbestand(Guid jobId, CancellationToken cancellationToken = default)
    {
        ValidateId(jobId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var job = ReadJob(db, jobId) ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Garmin-Import wurde nicht gefunden.");
        if (job.Status is GarminImportJobStatus.Wartend or GarminImportJobStatus.Laeuft)
            throw Konflikt("Garmin-Import ist noch nicht prüfbereit.");
        var bestand = DatenbestandTabellen.Laden(db);
        var kandidaten = ReadCandidates(db, tx, jobId).Select(x => x.Kandidat)
            .Where(x => x.Status == GarminImportKandidatStatus.FitBereit).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(new GarminImportPruefbestand(job, bestand, kandidaten));
    }

    public Task<GarminImportUebernahmeAntwort?> Quittung(Guid jobId, GarminImportUebernahmeAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        ValidateId(jobId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var result = ReadUebernahmeQuittung(db, tx, jobId, anfrage);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    private static string ValidateUebernahmeAnfrage(GarminImportUebernahmeAnfrage anfrage)
    {
        if (anfrage is null || !GueltigeVersion(anfrage.Version) || anfrage.AnfrageId == Guid.Empty || anfrage.Auswahl is null || anfrage.Auswahl.Count is < 1 or > 10000)
            throw Ungueltig("Garmin-Übernahmeanfrage ungültig.");
        var ids = new HashSet<Guid>();
        foreach (var item in anfrage.Auswahl)
        {
            if (item is null || item.KandidatId == Guid.Empty || !ids.Add(item.KandidatId) || !GueltigeVersion(item.DateiVersion) || item.EquipmentId is <= 0)
                throw Ungueltig("Garmin-Übernahmeanfrage ungültig.");
        }
        var canonical = anfrage.Auswahl.OrderBy(x => x.KandidatId)
            .Select(x => new { x.KandidatId, DateiVersion = x.DateiVersion.ToUpperInvariant(), x.Uebernehmen, x.EquipmentId }).ToArray();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Version = anfrage.Version.ToUpperInvariant(), Auswahl = canonical }))));
    }

    private static GarminImportUebernahmeAntwort? ReadUebernahmeQuittung(SqliteConnection db, SqliteTransaction tx, Guid jobId, GarminImportUebernahmeAnfrage anfrage)
    {
        var hash = ValidateUebernahmeAnfrage(anfrage);
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT Aktion,AnfrageHash,AntwortJson FROM GarminImportSteuerung WHERE JobId=$job AND AnfrageId=$request;";
        command.Parameters.AddWithValue("$job", jobId.ToString("D"));
        command.Parameters.AddWithValue("$request", anfrage.AnfrageId.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        if (reader.GetString(0) != "Uebernehmen" || reader.GetString(1) != hash)
            throw Konflikt("Anfragekennung wurde bereits verwendet.");
        return JsonSerializer.Deserialize<GarminImportUebernahmeAntwort>(reader.GetString(2))
            ?? throw Konflikt("Gespeicherte Übernahmeantwort ungültig.");
    }
}
