using System.Buffers.Binary;
using System.Security.Cryptography;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteOverlaySpeicher : IOverlayLeser
{
    private const int MaxInhaltBytes = 128 * 1024 * 1024;

    public Task<OverlayInhaltAntwort> Inhalt(long overlayId, string erwarteteVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (overlayId <= 0 || erwarteteVersion is not { Length: 64 } || !erwarteteVersion.All(Uri.IsHexDigit))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Overlay-Anfrage ungültig.");

        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        cancellationToken.ThrowIfCancellationRequested();
        var bestand = ReadBestand(db, tx, cancellationToken);
        var meta = bestand.Overlays.SingleOrDefault(o => o.Id == overlayId)
            ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay nicht gefunden.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(meta.Version), Convert.FromHexString(erwarteteVersion)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Overlay wurde geändert; Bestand bitte neu laden.");

        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes;
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT Punkte FROM Overlay WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", overlayId);
            using var stream = cmd.ExecuteReader();
            if (!stream.Read()) throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay nicht gefunden.");
            using var blob = stream.GetStream(0);
            if (blob.Length > MaxInhaltBytes)
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Overlay-Inhalt überschreitet das zulässige Größenlimit.");
            using var memory = new MemoryStream((int)blob.Length);
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = blob.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (memory.Length + read > MaxInhaltBytes)
                    throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Overlay-Inhalt überschreitet das zulässige Größenlimit.");
                memory.Write(buffer, 0, read);
            }
            bytes = memory.ToArray();
        }
        cancellationToken.ThrowIfCancellationRequested();
        var sections = Decode(bytes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(new OverlayInhaltAntwort(overlayId, meta.Version, sections));
    }

    private static IReadOnlyList<IReadOnlyList<OverlayPosition>> Decode(byte[] bytes, CancellationToken ct)
    {
        try
        {
            if (bytes.Length < 5 || bytes[0] != 1) throw InvalidContent();
            int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(1, 4));
            if (sectionCount < 0 || sectionCount > (bytes.Length - 5) / 4) throw InvalidContent();
            int offset = 5;
            var result = new List<IReadOnlyList<OverlayPosition>>(sectionCount);
            for (int i = 0; i < sectionCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (offset > bytes.Length - 4) throw InvalidContent();
                int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4)); offset += 4;
                if (count <= 0 || count > (bytes.Length - offset) / 16) throw InvalidContent();
                var points = new List<OverlayPosition>(count);
                for (int j = 0; j < count; j++)
                {
                    if ((j & 1023) == 0) ct.ThrowIfCancellationRequested();
                    double lat = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset, 8)));
                    double lon = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset + 8, 8)));
                    offset += 16;
                    if (!double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180)
                        throw InvalidContent();
                    points.Add(new OverlayPosition(lat, lon));
                }
                result.Add(points);
            }
            if (offset != bytes.Length) throw InvalidContent();
            return result;
        }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException)
        {
            throw InvalidContent();
        }
    }

    private static StammdatenException InvalidContent() =>
        new(StammdatenFehlerart.Ungueltig, "Gespeicherter Overlay-Inhalt ist ungültig.");
}
