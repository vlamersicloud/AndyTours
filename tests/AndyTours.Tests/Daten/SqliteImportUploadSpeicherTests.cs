using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Xunit;

namespace AndyTours.Tests.Daten;

public sealed class SqliteImportUploadSpeicherTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "FitUpload-" + Guid.NewGuid().ToString("N") + ".db");
        public SqliteImportUploadSpeicher Store { get; }
        public Fixture() => Store = new SqliteImportUploadSpeicher(_path);
        public SqliteImportUploadSpeicher Another() => new(_path);
        public void Dispose() { try { File.Delete(_path); } catch (IOException) { } }
    }

    [Fact]
    public async Task IdentischeRegistrierungReplaytUndAbweichungenKonfligieren()
    {
        using var f = new Fixture();
        var id = Guid.NewGuid();
        var store2 = f.Another();
        var first = await f.Store.Registrieren(id, "ride.bin", new string('a', 64), 20, CancellationToken.None);
        var replay = await store2.Registrieren(id, "ride.bin", new string('A', 64), 20, CancellationToken.None);
        Assert.Equal(first.Version, replay.Version);
        Assert.Equal(1, (await f.Store.Liste(0, 10, CancellationToken.None)).Gesamt);

        await Assert.ThrowsAsync<StammdatenException>(() =>
            store2.Registrieren(id, "ride.bin", new string('B', 64), 20, CancellationToken.None));
        await Assert.ThrowsAsync<StammdatenException>(() =>
            store2.Registrieren(id, "other.bin", new string('A', 64), 20, CancellationToken.None));
        var unchanged = (await f.Store.Laden(id, CancellationToken.None))!;
        Assert.Equal(first.Version, unchanged.Version);
        Assert.Equal("ride.bin", unchanged.Dateiname);
        Assert.Equal(1, (await f.Store.Liste(0, 10, CancellationToken.None)).Gesamt);
    }

    [Fact]
    public async Task UngueltigeMetadatenWerdenAbgelehntOhneZeilen()
    {
        using var f = new Fixture();
        var hash = new string('A', 64);
        async Task Reject(Guid id, string name, string contentHash, long bytes) =>
            await Assert.ThrowsAsync<StammdatenException>(() =>
                f.Store.Registrieren(id, name, contentHash, bytes, CancellationToken.None));

        var validId = Guid.NewGuid();
        await Reject(validId, "../ride.fit", hash, 20);
        await Reject(validId, "a/b.fit", hash, 20);
        await Reject(validId, "a\\b.fit", hash, 20);
        await Reject(validId, "bad\nname.fit", hash, 20);
        await Reject(validId, new string((char)0xD800, 1), hash, 20);
        await Reject(Guid.Empty, "ride.fit", hash, 20);
        await Reject(validId, "ride.fit", "bad", 20);
        await Reject(validId, "ride.fit", hash, 0);
        await Reject(validId, "ride.fit", hash, 134217729);
        Assert.Equal(0, (await f.Store.Liste(0, 10, CancellationToken.None)).Gesamt);
    }

    [Fact]
    public async Task PagingAndPreCanceledRegistrationPreserveState()
    {
        using var f = new Fixture();
        await f.Store.Registrieren(Guid.NewGuid(), "one.bin", new string('1', 64), 1, CancellationToken.None);
        await f.Store.Registrieren(Guid.NewGuid(), "two.bin", new string('2', 64), 2, CancellationToken.None);
        var page = await f.Store.Liste(0, 1, CancellationToken.None);
        Assert.Equal(2, page.Gesamt);
        Assert.Single(page.Eintraege);

        await Assert.ThrowsAsync<StammdatenException>(() => f.Store.Liste(-1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Store.Liste(0, 0, CancellationToken.None));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Store.Liste(0, 201, CancellationToken.None));

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            f.Store.Registrieren(Guid.NewGuid(), "cancel.bin", new string('C', 64), 3, canceled.Token));
        Assert.Equal(2, (await f.Store.Liste(0, 10, CancellationToken.None)).Gesamt);
    }
}
