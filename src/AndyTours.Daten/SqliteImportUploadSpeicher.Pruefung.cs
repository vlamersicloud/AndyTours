using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteImportUploadSpeicher
{
    public Task<FitUploadPruefbestand> Pruefbestand(IReadOnlyList<Guid> uploadIds, CancellationToken cancellationToken)
    {
        ValidateUploadIds(uploadIds);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var uploads = new List<FitUploadAntwort>(uploadIds.Count);
        foreach (var id in uploadIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uploads.Add(Read(db, tx, id) ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "FIT-Upload wurde nicht gefunden."));
        }
        var bestand = DatenbestandTabellen.Laden(db);
        var result = new FitUploadPruefbestand(FitUploadVersionen.Bestand(uploads), bestand, uploads);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(result);
    }

    public Task<FitUploadUebernahmeAntwort?> Quittung(FitUploadUebernahmeAnfrage anfrage, CancellationToken cancellationToken)
    {
        var hash = ValidateUebernahmeAnfrage(anfrage);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var response = ReadReceipt(db, tx, anfrage.AnfrageId, hash);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(response);
    }

    private static string ValidateUebernahmeAnfrage(FitUploadUebernahmeAnfrage anfrage)
    {
        if (anfrage is null || anfrage.AnfrageId == Guid.Empty || !ValidHash(anfrage.Version) ||
            anfrage.Auswahl is null || anfrage.Auswahl.Count is < 1 or > 10000 ||
            anfrage.Auswahl.Any(x => x is null || x.UploadId == Guid.Empty || !ValidHash(x.DateiVersion) ||
                (x.EquipmentId is not null && x.EquipmentId <= 0)) ||
            anfrage.Auswahl.Select(x => x.UploadId).Distinct().Count() != anfrage.Auswahl.Count)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Importanfrage ungültig.");

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("version", anfrage.Version.ToUpperInvariant());
            writer.WritePropertyName("auswahl");
            writer.WriteStartArray();
            foreach (var row in anfrage.Auswahl.OrderBy(x => x.UploadId))
            {
                writer.WriteStartObject();
                writer.WriteString("uploadId", row.UploadId.ToString("D"));
                writer.WriteString("dateiVersion", row.DateiVersion.ToUpperInvariant());
                writer.WriteBoolean("uebernehmen", row.Uebernehmen);
                if (row.EquipmentId is long equipmentId) writer.WriteNumber("equipmentId", equipmentId);
                else writer.WriteNull("equipmentId");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }

    private static void ValidateUploadIds(IReadOnlyList<Guid> uploadIds)
    {
        if (uploadIds is null || uploadIds.Count is < 1 or > 10000 ||
            uploadIds.Any(x => x == Guid.Empty) || uploadIds.Distinct().Count() != uploadIds.Count)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Uploadauswahl ungültig.");
    }

    private static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
}
