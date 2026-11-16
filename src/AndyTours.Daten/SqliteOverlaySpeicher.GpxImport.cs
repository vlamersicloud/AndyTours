using System.Security.Cryptography;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteOverlaySpeicher : IOverlayGpxSpeicher
{
    public Task<OverlayAnlegenAntwort> Importieren(
        OverlayGpxVorbereitung vorbereitet, CancellationToken cancellationToken)
    {
        if (vorbereitet is null || vorbereitet.Anfrage is null || vorbereitet.Abschnitte is null)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Overlay-GPX-Anfrage ungültig.");
        var request = OverlayGpxRegeln.Normalisieren(vorbereitet.Anfrage);
        PruefeGpxHash(vorbereitet.InhaltHash);
        string requestHash = OverlayGpxAnfrageHash(request, vorbereitet.InhaltHash);
        cancellationToken.ThrowIfCancellationRequested();

        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReceiptSchema(db, tx);
        var replay = ReadCreationReceipt(db, tx, request.AnfrageId, GpxImportArt, requestHash);
        if (replay is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tx.Commit();
            return Task.FromResult(replay);
        }

        var before = ReadBestand(db, tx, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(before.Version), Convert.FromHexString(request.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, FehlerKonflikt);
        if (request.OrdnerId is long folderId && !before.Ordner.Any(folder => folder.Id == folderId))
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay-Ordner nicht gefunden.");

        string name = OverlayGpxRegeln.Anzeigename(request.Dateiname);
        int pointBytes = PruefeGeometrie(prepared: vorbereitet.Abschnitte, cancellationToken);
        byte[] packed;
        try { packed = OverlayPunkte.Verpacken(vorbereitet.Abschnitte); }
        catch (OverflowException) { throw InvalidGeometry(); }
        if (packed.Length > OverlayGpxRegeln.MaxDateiBytes || packed.Length != pointBytes)
            throw InvalidGeometry();

        cancellationToken.ThrowIfCancellationRequested();
        using var insert = db.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO Overlay (OrdnerId, Name, Farbe, Eingeschaltet, Punkte)
            VALUES ($folder, $name, NULL, 1, $points) RETURNING Id;
            """;
        insert.Parameters.AddWithValue("$folder", (object?)request.OrdnerId ?? DBNull.Value);
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$points", packed);
        long id = Convert.ToInt64(insert.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        cancellationToken.ThrowIfCancellationRequested();

        var after = ReadBestand(db, tx, cancellationToken);
        var response = new OverlayAnlegenAntwort(after, id);
        WriteCreationReceipt(db, tx, request.AnfrageId, GpxImportArt, requestHash, response);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(response);
    }

    private static int PruefeGeometrie(
        IReadOnlyList<IReadOnlyList<AndyTours.Core.Modell.Geoposition>>? prepared,
        CancellationToken ct)
    {
        if (prepared is null || prepared.Count == 0) throw InvalidGeometry();
        long bytes = 5;
        foreach (var section in prepared)
        {
            ct.ThrowIfCancellationRequested();
            if (section is null || section.Count == 0) throw InvalidGeometry();
            bytes = checked(bytes + 4L + 16L * section.Count);
            if (bytes > OverlayGpxRegeln.MaxDateiBytes) throw InvalidGeometry();
            foreach (var point in section)
            {
                ct.ThrowIfCancellationRequested();
                if (!double.IsFinite(point.Breite) || !double.IsFinite(point.Laenge) ||
                    point.Breite is < -90 or > 90 || point.Laenge is < -180 or > 180)
                    throw InvalidGeometry();
            }
        }
        return checked((int)bytes);
    }

    private static StammdatenException InvalidGeometry() =>
        new(StammdatenFehlerart.Ungueltig, "Overlay-GPX-Strecke ungültig.");
}
