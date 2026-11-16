using System.Security.Cryptography;
using System.Text;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Import.Overlays;
using AndyTours.Tests.Datenordner;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Import;

public sealed class OverlayGpxImportDienstTests
{
    [Fact]
    public async Task ImportsNamespacedSegmentsAndReplaysExactPhysicalBody()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "overlay.db");
        var store = new SqliteOverlaySpeicher(dbPath);
        var baseline = await store.Laden(CancellationToken.None);
        var folder = await store.OrdnerAnlegen(
            new OverlayOrdnerAnlegenAnfrage(baseline.Version, Guid.NewGuid(), null, "Karten"), CancellationToken.None);
        var importer = new OverlayGpxImportDienst(store);
        const string gpx = """
            <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1">
              <wpt lat="1" lon="2"/>
              <trk><trkseg><trkpt lat="48.1" lon="11.5"/><trkpt lat="bad" lon="12"/>
                    <trkpt lat="48.2" lon="11.6"/></trkseg><trkseg><trkpt lat="49" lon="12"/></trkseg></trk>
              <rte><rtept lat="2" lon="3"/></rte>
            </gpx>
            """;
        byte[] body = Encoding.UTF8.GetBytes(gpx);
        Guid requestId = Guid.NewGuid();
        var request = new OverlayGpxImportAnfrage(folder.Bestand.Version, requestId, folder.Id, "München.gpx");
        using var stream = new MemoryStream(body);
        var created = await importer.Importieren(request, stream, CancellationToken.None);
        Assert.True(stream.CanRead);
        var meta = Assert.Single(created.Bestand.Overlays);
        Assert.Equal("München", meta.Name);
        Assert.Equal(folder.Id, meta.OrdnerId);
        Assert.True(meta.IstEingeschaltet);
        Assert.Null(meta.Farbe);

        string hash = Convert.ToHexString(SHA256.HashData(body));
        var content = await store.Inhalt(created.Id, meta.Version, CancellationToken.None);
        Assert.Equal(2, content.Abschnitte.Count);
        Assert.Collection(content.Abschnitte[0],
            point => Assert.Equal(new AndyTours.Contracts.V1.OverlayPosition(48.1, 11.5), point),
            point => Assert.Equal(new AndyTours.Contracts.V1.OverlayPosition(48.2, 11.6), point));
        Assert.Single(content.Abschnitte[1]);

        using var replayStream = new MemoryStream(body);
        var replay = await importer.Importieren(request, replayStream, CancellationToken.None);
        Assert.Equal(created.Id, replay.Id);
        Assert.Equal(created.Bestand.Version, replay.Bestand.Version);
        Assert.Single(replay.Bestand.Overlays);

        using var changedStream = new MemoryStream(Encoding.UTF8.GetBytes(gpx.Replace("48.1", "48.3", StringComparison.Ordinal)));
        var changedBody = await Assert.ThrowsAsync<StammdatenException>(() => importer.Importieren(request, changedStream, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, changedBody.Art);

        using var db = OeffneDatenbank.Ausfuehren(dbPath);
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Overlay WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", created.Id);
            cmd.ExecuteNonQuery();
        }
        var afterDelete = await importer.Importieren(request, new MemoryStream(body), CancellationToken.None);
        Assert.Equal(created.Id, afterDelete.Id);
        Assert.Empty((await store.Laden(CancellationToken.None)).Overlays);
        Assert.NotNull(await store.Quittung(request, hash, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsMalformedDtdEmptyAndRouteOnlyInputWithoutReceipts()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "overlay.db");
        var store = new SqliteOverlaySpeicher(dbPath);
        var baseline = await store.Laden(CancellationToken.None);
        var importer = new OverlayGpxImportDienst(store);
        string[] invalidBodies =
        [
            "",
            "<gpx",
            "<!DOCTYPE gpx [<!ENTITY x SYSTEM 'file:///secret'>]><gpx><trk><trkseg><trkpt lat='&x;' lon='0'/></trkseg></trk></gpx>",
            "<gpx xmlns='http://www.topografix.com/GPX/1/0'><wpt lat='1' lon='2'/></gpx>"
        ];
        int i = 0;
        foreach (string invalid in invalidBodies)
        {
            var request = new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, $"bad{i++}.gpx");
            byte[] bytes = Encoding.UTF8.GetBytes(invalid);
            using var stream = new MemoryStream(bytes);
            var error = await Assert.ThrowsAsync<StammdatenException>(() => importer.Importieren(request, stream, CancellationToken.None));
            Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
            Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            Assert.Null(await store.Quittung(request, hash, CancellationToken.None));
        }

        var preCanceled = new CountingStream(Encoding.UTF8.GetBytes("<gpx/>"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importer.Importieren(new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, "cancel.gpx"),
                preCanceled, new CancellationToken(true)));
        Assert.Equal(0, preCanceled.ReadCalls);
        Assert.Empty((await store.Laden(CancellationToken.None)).Overlays);
    }

    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        public int ReadCalls { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
