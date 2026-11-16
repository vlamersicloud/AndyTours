using System.Security.Cryptography;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteOverlaySpeicher
{
    private const string GpxImportArt = "gpx-import";

    public Task<OverlayAnlegenAntwort?> Quittung(
        OverlayGpxImportAnfrage anfrage, string inhaltHash, CancellationToken cancellationToken)
    {
        var normalized = OverlayGpxRegeln.Normalisieren(anfrage);
        PruefeGpxHash(inhaltHash);
        cancellationToken.ThrowIfCancellationRequested();

        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        cancellationToken.ThrowIfCancellationRequested();
        using (var exists = db.CreateCommand())
        {
            exists.Transaction = tx;
            exists.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='OverlayQuittung' LIMIT 1;";
            if (exists.ExecuteScalar() is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tx.Commit();
                return Task.FromResult<OverlayAnlegenAntwort?>(null);
            }
        }

        string hash = OverlayGpxAnfrageHash(normalized, inhaltHash);
        var receipt = ReadCreationReceipt(db, tx, normalized.AnfrageId, GpxImportArt, hash);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(receipt);
    }

    private static void PruefeGpxHash(string hash)
    {
        if (hash is not { Length: 64 } || !hash.All(Uri.IsHexDigit))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Overlay-GPX-Anfrage ungültig.");
    }

    private static string OverlayGpxAnfrageHash(OverlayGpxImportAnfrage anfrage, string inhaltHash) =>
        CreationHash(GpxImportArt, new
        {
            version = anfrage.Version,
            folder = anfrage.OrdnerId,
            filename = anfrage.Dateiname,
            contentHash = inhaltHash.ToUpperInvariant()
        });
}
