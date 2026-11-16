using System.Security.Cryptography;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Tests.Datenordner;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteOverlayGpxSpeicherTests
{
    [Fact]
    public async Task ReceiptLookupIsReadOnlyAndImportReplayPrecedesPreparedGeometryAndParentChecks()
    {
        using var temp = new TempOrdner();
        string path = Path.Combine(temp.Pfad, "overlay.db");
        var store = new SqliteOverlaySpeicher(path);
        var baseline = await store.Laden(CancellationToken.None);
        var probe = new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, "Probe.gpx");
        string probeHash = Convert.ToHexString(SHA256.HashData("probe"u8));
        Assert.Null(await store.Quittung(probe, probeHash, CancellationToken.None));
        using (var db = OeffneDatenbank.Ausfuehren(path))
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OverlayQuittung';";
            Assert.Equal(0L, Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
        }
        long parent = await CreateFolder(store, baseline.Version);
        var snapshot = await store.Laden(CancellationToken.None);
        var request = new OverlayGpxImportAnfrage(snapshot.Version, Guid.NewGuid(), parent, "Track.gpx");
        string hash = Convert.ToHexString(SHA256.HashData("body"u8));

        var prepared = Prepare(request, hash, [new Geoposition(48, 11)]);
        var created = await store.Importieren(prepared, CancellationToken.None);
        Assert.Single(created.Bestand.Overlays);
        var replayPrepared = prepared with { Abschnitte = null! };
        using (var db = OeffneDatenbank.Ausfuehren(path))
        {
            using var command = db.CreateCommand();
            command.CommandText = "DELETE FROM Overlayordner WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", parent);
            command.ExecuteNonQuery();
        }
        var replay = await store.Importieren(replayPrepared, CancellationToken.None);
        Assert.Equal(created.Id, replay.Id);
        Assert.Equal(created.Bestand.Version, replay.Bestand.Version);
        Assert.Empty((await store.Laden(CancellationToken.None)).Overlays);

        var changedHash = await Assert.ThrowsAsync<StammdatenException>(() =>
            store.Quittung(request, new string('A', 64), CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, changedHash.Art);
    }

    [Fact]
    public async Task ReceiptFailureRollsBackGeometryAndInvalidPreparedGeometryNeverWrites()
    {
        using var temp = new TempOrdner();
        string path = Path.Combine(temp.Pfad, "overlay.db");
        var store = new SqliteOverlaySpeicher(path);
        var initial = await store.Laden(CancellationToken.None);
        _ = await CreateFolder(store, initial.Version);
        var baseline = await store.Laden(CancellationToken.None);
        var request = new OverlayGpxImportAnfrage(baseline.Version, Guid.NewGuid(), null, "Track.gpx");
        string hash = new string('B', 64);
        var prepared = Prepare(request, hash, [new Geoposition(12, 34)]);

        using (var db = OeffneDatenbank.Ausfuehren(path))
            Exec(db, "CREATE TRIGGER fail_overlay_receipt BEFORE INSERT ON OverlayQuittung BEGIN SELECT RAISE(ABORT,'injected'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => store.Importieren(prepared, CancellationToken.None));
        Assert.Equal(baseline.Version, (await store.Laden(CancellationToken.None)).Version);
        Assert.Equal(baseline.Version, (await store.Laden(CancellationToken.None)).Version);
        Assert.Empty((await store.Laden(CancellationToken.None)).Overlays);
        Assert.Null(await store.Quittung(request, hash, CancellationToken.None));

        using (var db = OeffneDatenbank.Ausfuehren(path))
            Exec(db, "DROP TRIGGER fail_overlay_receipt;");
        var created = await store.Importieren(prepared, CancellationToken.None);
        Assert.Single(created.Bestand.Overlays);
        Assert.NotNull(await store.Quittung(request, hash, CancellationToken.None));

        var current = await store.Laden(CancellationToken.None);
        var invalidRequest = new OverlayGpxImportAnfrage(current.Version, Guid.NewGuid(), null, "Bad.gpx");
        var invalid = Prepare(invalidRequest, new string('C', 64), [new Geoposition(0, 0)]);
        invalid = invalid with { Abschnitte = new IReadOnlyList<Geoposition>[] { new Geoposition[] { null! } } };
        var badPoint = await Assert.ThrowsAsync<StammdatenException>(() => store.Importieren(invalid, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Ungueltig, badPoint.Art);
        var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.Importieren(prepared, new CancellationToken(true)));
        Assert.IsAssignableFrom<OperationCanceledException>(canceled);
        Assert.Single((await store.Laden(CancellationToken.None)).Overlays);
    }

    private static OverlayGpxVorbereitung Prepare(
        OverlayGpxImportAnfrage request, string hash, IReadOnlyList<Geoposition> points) =>
        new(request, hash, new IReadOnlyList<Geoposition>[] { points });

    private static async Task<long> CreateFolder(SqliteOverlaySpeicher store, string version) =>
        (await store.OrdnerAnlegen(new OverlayOrdnerAnlegenAnfrage(version, Guid.NewGuid(), null, "GPX"), CancellationToken.None)).Id;

    private static void Exec(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
