using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteGarminImportJobs : IGarminImportJobs
{
    public Task<GarminImportJobAntwort> Abbrechen(Guid jobId, GarminImportSteuerAnfrage anfrage, CancellationToken ct = default) =>
        Steuern(jobId, anfrage, "Abbrechen", ct);

    public Task<GarminImportJobAntwort> Fortsetzen(Guid jobId, GarminImportSteuerAnfrage anfrage, CancellationToken ct = default) =>
        Steuern(jobId, anfrage, "Fortsetzen", ct);

    private Task<GarminImportJobAntwort> Steuern(Guid jobId, GarminImportSteuerAnfrage anfrage, string aktion, CancellationToken ct)
    {
        ValidateId(jobId);
        if (anfrage is null || anfrage.AnfrageId == Guid.Empty || !GueltigeVersion(anfrage.Version))
            throw Ungueltig("Garmin-Steueranfrage ungültig.");
        ct.ThrowIfCancellationRequested();

        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(aktion + "|" + anfrage.Version)));
        using (var receipt = db.CreateCommand())
        {
            receipt.Transaction = tx;
            receipt.CommandText = "SELECT Aktion,AnfrageHash,AntwortJson FROM GarminImportSteuerung WHERE JobId=$job AND AnfrageId=$request;";
            receipt.Parameters.AddWithValue("$job", jobId.ToString("D"));
            receipt.Parameters.AddWithValue("$request", anfrage.AnfrageId.ToString("D"));
            using var reader = receipt.ExecuteReader();
            if (reader.Read())
            {
                if (reader.GetString(0) != aktion || reader.GetString(1) != hash)
                    throw Konflikt("Anfragekennung wurde bereits verwendet.");
                var previous = JsonSerializer.Deserialize<GarminImportJobAntwort>(reader.GetString(2))
                    ?? throw Konflikt("Gespeicherte Anfrageantwort ungültig.");
                return Task.FromResult(previous);
            }
        }

        var current = ReadJob(db, jobId) ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Garmin-Import wurde nicht gefunden.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(current.Version), Convert.FromHexString(anfrage.Version)))
            throw Konflikt("Garmin-Import wurde zwischenzeitlich geändert.");

        var nextPacket = current.Paketnummer;
        var nextState = current.Status;
        if (aktion == "Abbrechen")
        {
            if (current.Status == GarminImportJobStatus.Fertig)
                throw Konflikt("Abgeschlossene Importe können nicht abgebrochen werden.");
            nextState = GarminImportJobStatus.Abgebrochen;
        }
        else
        {
            if (current.Status is not (GarminImportJobStatus.PaketBereit or GarminImportJobStatus.Unterbrochen or GarminImportJobStatus.Abgebrochen))
                throw Konflikt("Dieser Garmin-Import kann derzeit nicht fortgesetzt werden.");
            if (current.Status == GarminImportJobStatus.PaketBereit)
            {
                if (!current.WeitereMoeglich) throw Konflikt("Es sind keine weiteren Garmin-Pakete verfügbar.");
                if (!current.Automatisch && ScalarLong(db, tx,
                    "SELECT COUNT(*) FROM GarminImportKandidat WHERE JobId=$job AND Paketnummer=$packet AND Zustand=2;",
                    ("$job", jobId.ToString("D")), ("$packet", current.Paketnummer)) > 0)
                    throw Konflikt("Das aktuelle Paket enthält noch nicht bestätigte Kandidaten.");
                nextPacket = checked(current.Paketnummer + 1);
            }
            if (string.IsNullOrWhiteSpace(EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminPersonalApiKey)))
                throw Ungueltig("Garmin-Schlüssel ist nicht konfiguriert.");
            if (ScalarLong(db, tx, "SELECT COUNT(*) FROM GarminImportJob WHERE JobId<>$job AND Zustand BETWEEN 0 AND 3;",
                ("$job", jobId.ToString("D"))) > 0)
                throw Konflikt("Ein anderer Garmin-Import ist bereits aktiv.");
            nextState = GarminImportJobStatus.Wartend;
        }

        var now = _zeit.GetUtcNow();
        using (var update = db.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = """
                UPDATE GarminImportJob SET Zustand=$state,Revision=Revision+1,Paketnummer=$packet,
                AktualisiertUtc=$updated,LeaseOwner=NULL,LeaseBisUtc=NULL,LeaseGeneration=LeaseGeneration+1 WHERE JobId=$job;
                """;
            update.Parameters.AddWithValue("$state", (int)nextState);
            update.Parameters.AddWithValue("$packet", nextPacket);
            update.Parameters.AddWithValue("$updated", Format(now));
            update.Parameters.AddWithValue("$job", jobId.ToString("D"));
            ct.ThrowIfCancellationRequested();
            try { update.ExecuteNonQuery(); }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067) { throw Konflikt("Ein anderer Garmin-Import ist bereits aktiv."); }
        }

        ct.ThrowIfCancellationRequested();
        var result = ReadJob(db, jobId)!;
        using (var saveReceipt = db.CreateCommand())
        {
            saveReceipt.Transaction = tx;
            saveReceipt.CommandText = """
                INSERT INTO GarminImportSteuerung(JobId,AnfrageId,Aktion,AnfrageHash,AntwortJson)
                VALUES($job,$request,$action,$hash,$response);
                """;
            saveReceipt.Parameters.AddWithValue("$job", jobId.ToString("D"));
            saveReceipt.Parameters.AddWithValue("$request", anfrage.AnfrageId.ToString("D"));
            saveReceipt.Parameters.AddWithValue("$action", aktion);
            saveReceipt.Parameters.AddWithValue("$hash", hash);
            saveReceipt.Parameters.AddWithValue("$response", JsonSerializer.Serialize(result));
            saveReceipt.ExecuteNonQuery();
        }
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    private static bool GueltigeVersion(string? version) =>
        version is { Length: 64 } && version.All(Uri.IsHexDigit);
}
