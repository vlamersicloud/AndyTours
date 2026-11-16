using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteOverlaySpeicher : IOverlayVerwaltung
{
    public Task<OverlayBestandAntwort> OverlayAendern(
        long overlayId, OverlayAendernAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        PruefeId(overlayId);
        PruefeVersion(anfrage.Version);
        PruefeOptionaleId(anfrage.OrdnerId);
        PruefeFarbe(anfrage.Farbe);
        string name = NormalisiereName(anfrage.Name);

        var result = ExecuteMutation(anfrage.Version, (db, tx, before) =>
        {
            if (!before.Overlays.Any(item => item.Id == overlayId))
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay nicht gefunden.");
            if (anfrage.OrdnerId is long folderId && !before.Ordner.Any(folder => folder.Id == folderId))
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay-Ordner nicht gefunden.");
            FuehreAus(db, tx,
                "UPDATE Overlay SET OrdnerId=$folder, Name=$name, Farbe=$color WHERE Id=$id;",
                ("$folder", (object?)anfrage.OrdnerId ?? DBNull.Value),
                ("$name", name),
                ("$color", (object?)anfrage.Farbe?.ToString() ?? DBNull.Value),
                ("$id", overlayId));
        }, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<OverlayBestandAntwort> OverlayLoeschen(
        long overlayId, string version, CancellationToken cancellationToken)
    {
        PruefeId(overlayId);
        PruefeVersion(version);
        var result = ExecuteMutation(version, (db, tx, before) =>
        {
            if (!before.Overlays.Any(item => item.Id == overlayId))
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay nicht gefunden.");
            FuehreAus(db, tx, "DELETE FROM Overlay WHERE Id=$id;", ("$id", overlayId));
        }, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<OverlayBestandAntwort> Sichtbarkeit(
        OverlaySichtbarkeitAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        PruefeVersion(anfrage.Version);
        if (anfrage.OverlayIds is null || anfrage.OverlayIds.Count == 0 ||
            anfrage.OverlayIds.Any(id => id <= 0) ||
            anfrage.OverlayIds.Distinct().Count() != anfrage.OverlayIds.Count)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);

        var result = ExecuteMutation(anfrage.Version, (db, tx, before) =>
        {
            var known = before.Overlays.Select(item => item.Id).ToHashSet();
            if (anfrage.OverlayIds.Any(id => !known.Contains(id)))
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay nicht gefunden.");
            using var command = db.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "UPDATE Overlay SET Eingeschaltet=$enabled WHERE Id=$id;";
            var idParameter = command.Parameters.Add("$id", SqliteType.Integer);
            command.Parameters.AddWithValue("$enabled", anfrage.IstEingeschaltet);
            foreach (long id in anfrage.OverlayIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                idParameter.Value = id;
                command.ExecuteNonQuery();
            }
        }, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<OverlayBestandAntwort> GemeinsameFarbe(
        OverlayGemeinsameFarbeAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);
        PruefeVersion(anfrage.Version);
        if (!Enum.IsDefined(anfrage.Farbe))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, FehlerUngueltig);

        var result = ExecuteMutation(anfrage.Version, (db, tx, _) =>
        {
            EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.OverlayFarbe, anfrage.Farbe.ToString());
        }, cancellationToken);
        return Task.FromResult(result);
    }
}
