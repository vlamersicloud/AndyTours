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
        var folder = await store.OrdnerAnlegen(new OverlayOrdnerAnlegenAnfrage(baseline.Version, Guid.NewGuid(), null, "Karten"), CancellationToken.None);
        var importer = new OverlayGpxImportDienst(store);
        const string gpx = """
            <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1"><wpt lat="1" lon="2"/><trk><trkseg><trkpt lat="48.1" lon="11.5"/><trkpt lat="bad" lon="12"/><trkpt lat="48.2" lon="11.6"/></trkseg><trkseg><trkpt lat="49" lon="12"/></trkseg></trk><rte><rtept lat="2" lon="3"/></rte></gpx>
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
        Assert.Collection(content.Abschnitte[0], point => Assert.Equal(new AndyTours.Contracts.V1.OverlayPosition(48.1, 11.5), point), point => Assert.Equal(new AndyTours.Contracts.V1.OverlayPosition(48.2, 11.6), point));
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
        using (var cmd = db.CreateCommand()) { cmd.CommandText = "DELETE FROM Overlay WHERE Id=$id;"; cmd.Parameters.AddWithValue("$id", created.Id); cmd.ExecuteNonQuery(); }
        using var afterDeleteStream = new MemoryStream(body);
        var afterDelete = await importer.Importieren(request, afterDeleteStream, CancellationToken.None);
        Assert.Equal(created.Id, afterDelete.Id);
        Assert.Empty((await store.Laden(CancellationToken.None)).Overlays);
        Assert.NotNull(await store.Quittung(request, hash, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsMalformedDtdEmptyAndWaypointOnlyInputWithoutReceipts()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "overlay.db");
        var store = new SqliteOverlaySpeicher(dbPath);
        var baseline = await store.Laden(CancellationToken.None);
        var importer = new OverlayGpxImportDienst(store);
        string[] invalidBodies = ["", "<gpx", "<!DOCTYPE gpx [<!ENTITY x SYSTEM 'file:///secret'>]><gpx><trk><trkseg><trkpt lat='&x;' lon='0'/></trkseg></trk></gpx>", "<gpx xmlns='http://www.topografix.com/GPX/1/0'><wpt lat='1' lon='2'/></gpx>"];
        int i = 0;
        foreach (string invalid in invalidBodies)
        {
            var request = new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, $"bad{i++}.gpx");
            byte[] bytes = Encoding.UTF8.GetBytes(invalid);
            using var stream = new MemoryStream(bytes);
            var error = await Assert.ThrowsAsync<StammdatenException>(() => importer.Importieren(request, stream, CancellationToken.None));
            Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
            Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
            Assert.Null(await store.Quittung(request, Convert.ToHexString(SHA256.HashData(bytes)), CancellationToken.None));
        }
        using var preCanceled = new CountingStream(Encoding.UTF8.GetBytes("<gpx/>"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => importer.Importieren(new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, "cancel.gpx"), preCanceled, new CancellationToken(true)));
        Assert.Equal(0, preCanceled.ReadCalls);
        Assert.Empty((await store.Laden(CancellationToken.None)).Overlays);
    }

    [Fact]
    public async Task Gpx10FallsBackToRouteSegmentsWhenTrackPointsAreAllInvalid()
    {
        using var temp = new TempOrdner();
        var store = new SqliteOverlaySpeicher(Path.Combine(temp.Pfad, "overlay.db"));
        var baseline = await store.Laden(CancellationToken.None);
        var importer = new OverlayGpxImportDienst(store);
        const string xml = "<gpx xmlns=\"http://www.topografix.com/GPX/1/0\"><trk><trkseg><trkpt lat=\"bad\" lon=\"1\"/></trkseg></trk><rte><rtept lat=\"1\" lon=\"2\"/><rtept lat=\"3\" lon=\"4\"/></rte><rte><rtept lat=\"5\" lon=\"6\"/></rte><wpt lat=\"9\" lon=\"9\"/></gpx>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var result = await importer.Importieren(new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, "Route.gpx"), stream, CancellationToken.None);
        var metadata = Assert.Single(result.Bestand.Overlays);
        var content = await store.Inhalt(result.Id, metadata.Version, CancellationToken.None);
        Assert.Equal(2, content.Abschnitte.Count);
        Assert.Collection(content.Abschnitte[0], point => Assert.Equal(new OverlayPosition(1, 2), point), point => Assert.Equal(new OverlayPosition(3, 4), point));
        Assert.Equal(new OverlayPosition(5, 6), Assert.Single(content.Abschnitte[1]));
    }

    [Fact]
    public async Task Oversize_stream_stops_at_limit_without_receipt_and_invalid_name_reads_nothing()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "overlay.db");
        var store = new SqliteOverlaySpeicher(dbPath);
        var baseline = await store.Laden(CancellationToken.None);
        var importer = new OverlayGpxImportDienst(store);
        var request = new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, "large.gpx");
        using var oversized = new GeneratedStream(OverlayGpxRegeln.MaxDateiBytes + 1);
        await Assert.ThrowsAsync<OverlayGpxZuGrossException>(() => importer.Importieren(request, oversized, CancellationToken.None));
        Assert.True(oversized.CanRead);
        Assert.Equal(OverlayGpxRegeln.MaxDateiBytes + 1, oversized.BytesRead);
        Assert.Empty((await store.Laden(CancellationToken.None)).Overlays);
        Assert.Equal(0, await ReceiptCount(dbPath));

        foreach (string invalidName in new[] { "../x.gpx", ".gpx", "bad\uD800.gpx" })
        {
            using var noRead = new CountingStream(Encoding.UTF8.GetBytes("<gpx/>"));
            await Assert.ThrowsAsync<StammdatenException>(() => importer.Importieren(request with { Dateiname = invalidName }, noRead, CancellationToken.None));
            Assert.Equal(0, noRead.ReadCalls);
            Assert.True(noRead.CanRead);
        }
    }

    private static async Task<int> ReceiptCount(string path)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite }.ToString());
        await db.OpenAsync();
        using var command = db.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OverlayQuittung';";
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        public int ReadCalls { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { ReadCalls++; return base.ReadAsync(buffer, cancellationToken); }
    }

    private sealed class GeneratedStream(long remaining) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int count = (int)Math.Min(buffer.Length, remaining);
            buffer[..count].Fill((byte)'a'); remaining -= count; BytesRead += count; return count;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Read(buffer.Span)); }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Read(buffer.AsSpan(offset, count))); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
