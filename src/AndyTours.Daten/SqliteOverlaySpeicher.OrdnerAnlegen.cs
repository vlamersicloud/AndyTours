using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteOverlaySpeicher
{
    private const string OrdnerAnlegenArt = "ordner-anlegen";

    public Task<OverlayAnlegenAntwort> OrdnerAnlegen(
        OverlayOrdnerAnlegenAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        PruefeVersion(anfrage.Version);
        if (anfrage.AnfrageId == Guid.Empty)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        PruefeOptionaleId(anfrage.ElternId);
        string name = NormalisiereName(anfrage.Name);
        cancellationToken.ThrowIfCancellationRequested();

        string requestHash = CreationHash(OrdnerAnlegenArt, new
        {
            version = anfrage.Version.ToUpperInvariant(),
            parent = anfrage.ElternId,
            name
        });
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReceiptSchema(db, tx);
        var replay = ReadCreationReceipt(db, tx, anfrage.AnfrageId, OrdnerAnlegenArt, requestHash);
        if (replay is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tx.Commit();
            return Task.FromResult(replay);
        }

        var before = ReadBestand(db, tx, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(before.Version), Convert.FromHexString(anfrage.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, FehlerKonflikt);
        if (anfrage.ElternId is long parentId && !before.Ordner.Any(folder => folder.Id == parentId))
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Übergeordneter Overlay-Ordner nicht gefunden.");

        var existing = before.Ordner.Select(folder => new AndyTours.Core.Modell.Overlayordner(
            folder.Id, folder.ElternId, folder.Name)).ToArray();
        if (PruefeOrdnername.Ausfuehren(name, anfrage.ElternId, existing) is not null)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);

        cancellationToken.ThrowIfCancellationRequested();
        using var insert = db.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO Overlayordner (ElternId, Name) VALUES ($parent, $name) RETURNING Id;";
        insert.Parameters.AddWithValue("$parent", (object?)anfrage.ElternId ?? DBNull.Value);
        insert.Parameters.AddWithValue("$name", name);
        long id = Convert.ToInt64(insert.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        cancellationToken.ThrowIfCancellationRequested();

        var after = ReadBestand(db, tx, cancellationToken);
        var response = new OverlayAnlegenAntwort(after, id);
        WriteCreationReceipt(db, tx, anfrage.AnfrageId, OrdnerAnlegenArt, requestHash, response);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(response);
    }
}
