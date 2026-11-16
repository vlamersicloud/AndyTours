using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Daten;
using AndyTours.Import.Dateien;
using Xunit;

namespace AndyTours.Tests.Import;

public sealed class FitUploadDienstTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FitUploadService-" + Guid.NewGuid().ToString("N"));
        public SqliteImportUploadSpeicher Store { get; }
        public FitUploadDienst Dienst { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            var database = Path.Combine(Root, "db.sqlite");
            Store = new SqliteImportUploadSpeicher(database);
            Dienst = new FitUploadDienst(Store, Path.Combine(Root, "data"));
        }
        public string Original(Guid id) => Path.Combine(Root, "data", "FitUploads", id.ToString("N"), "original.fit");
        public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task GleicherUploadReplaytUnveraendertenInhaltUndSchliesstCallerStreamNicht()
    {
        using var f = new Fixture();
        var id = Guid.NewGuid();
        var bytes = new byte[] { 7, 1, 4, 9 };
        using var firstStream = new MemoryStream(bytes);
        var first = await f.Dienst.Hochladen(id, "ride.bin", firstStream, CancellationToken.None);
        Assert.True(firstStream.CanRead);
        using var retryStream = new MemoryStream(bytes);
        var retry = await f.Dienst.Hochladen(id, "ride.bin", retryStream, CancellationToken.None);
        Assert.True(retryStream.CanRead);
        Assert.Equal(first.Version, retry.Version);
        Assert.Equal(first.InhaltHash, retry.InhaltHash);
        Assert.Equal(bytes, File.ReadAllBytes(f.Original(id)));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(f.Original(id))!, ".upload-*.tmp"));
        Assert.Equal(1, (await f.Store.Liste(0, 10, CancellationToken.None)).Gesamt);
    }

    [Fact]
    public async Task AndereBytesMitGleicherIdKonfligierenOhneOriginalZuUeberschreiben()
    {
        using var f = new Fixture();
        var id = Guid.NewGuid();
        var original = new byte[] { 1, 2, 3 };
        using var first = new MemoryStream(original);
        var metadata = await f.Dienst.Hochladen(id, "ride.fit", first, CancellationToken.None);
        using var different = new MemoryStream(new byte[] { 9, 8, 7 });
        var error = await Assert.ThrowsAsync<StammdatenException>(() =>
            f.Dienst.Hochladen(id, "ride.fit", different, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Equal(original, File.ReadAllBytes(f.Original(id)));
        Assert.Equal(metadata.Version, (await f.Store.Laden(id, CancellationToken.None))!.Version);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(f.Original(id))!, ".upload-*.tmp"));
    }

    [Fact]
    public async Task LeereUndVorabAbgebrocheneUploadsHinterlassenKeineMetadatenOderTempdateien()
    {
        using var f = new Fixture();
        var emptyId = Guid.NewGuid();
        using var empty = new MemoryStream([]);
        await Assert.ThrowsAsync<StammdatenException>(() =>
            f.Dienst.Hochladen(emptyId, "empty.bin", empty, CancellationToken.None));

        var canceledId = Guid.NewGuid();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var body = new MemoryStream([3, 2, 1]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            f.Dienst.Hochladen(canceledId, "canceled.bin", body, canceled.Token));

        Assert.Null(await f.Store.Laden(emptyId, CancellationToken.None));
        Assert.Null(await f.Store.Laden(canceledId, CancellationToken.None));
        var uploadRoot = Path.Combine(f.Root, "data", "FitUploads");
        Assert.False(Directory.Exists(uploadRoot) && Directory.GetFiles(uploadRoot, ".upload-*.tmp", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public async Task UploadUeber128MiBWirdAbgebrochenUndNichtRegistriert()
    {
        using var f = new Fixture();
        var id = Guid.NewGuid();
        using var overLimit = new RepeatedStream(FitUploadRegeln.MaxDateiBytes + 1);
        await Assert.ThrowsAsync<FitUploadZuGrossException>(() =>
            f.Dienst.Hochladen(id, "large.bin", overLimit, CancellationToken.None));
        Assert.Null(await f.Store.Laden(id, CancellationToken.None));
    }

    private sealed class RepeatedStream(long remaining) : Stream
    {
        private long _remaining = remaining;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _remaining;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_remaining == 0) return ValueTask.FromResult(0);
            var count = (int)Math.Min(buffer.Length, _remaining);
            buffer.Span[..count].Fill(0x5A);
            _remaining -= count;
            return ValueTask.FromResult(count);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
