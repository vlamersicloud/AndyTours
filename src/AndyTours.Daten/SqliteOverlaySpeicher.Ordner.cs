using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteOverlaySpeicher
{
    public Task<OverlayBestandAntwort> OrdnerAendern(
        long ordnerId, OverlayOrdnerAendernAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        PruefeId(ordnerId);
        PruefeVersion(anfrage.Version);
        PruefeOptionaleId(anfrage.ElternId);
        string name = NormalisiereName(anfrage.Name);

        var result = ExecuteMutation(anfrage.Version, (db, tx, before) =>
        {
            var target = before.Ordner.SingleOrDefault(folder => folder.Id == ordnerId)
                ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay-Ordner nicht gefunden.");
            var folders = before.Ordner.Select(folder => new Overlayordner(folder.Id, folder.ElternId, folder.Name)).ToArray();
            if (anfrage.ElternId is long parentId && !folders.Any(folder => folder.Id == parentId))
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Übergeordneter Overlay-Ordner nicht gefunden.");
            if (!PruefeVerschiebeziel.Ausfuehren(ordnerId, anfrage.ElternId, folders))
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
            if (PruefeOrdnername.Ausfuehren(name, anfrage.ElternId, folders, ordnerId) is not null)
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);

            FuehreAus(db, tx,
                "UPDATE Overlayordner SET ElternId = $parent, Name = $name WHERE Id = $id;",
                ("$parent", (object?)anfrage.ElternId ?? DBNull.Value), ("$name", name), ("$id", ordnerId));
        }, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<OverlayBestandAntwort> OrdnerLoeschen(
        long ordnerId, string version, CancellationToken cancellationToken)
    {
        PruefeId(ordnerId);
        PruefeVersion(version);
        var result = ExecuteMutation(version, (db, tx, before) =>
        {
            if (!before.Ordner.Any(folder => folder.Id == ordnerId))
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay-Ordner nicht gefunden.");
            FuehreAus(db, tx, "DELETE FROM Overlayordner WHERE Id = $id;", ("$id", ordnerId));
        }, cancellationToken);
        return Task.FromResult(result);
    }
}
