using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;

namespace AndyTours.Tests.Sicherung;

public sealed class SqlitePruefRunnerTests
{
    private static readonly SicherungsarchivGrenzen Limits = new(new SicherungsmanifestGrenzen(8, 4096, 8192, 200), 1_000_000, 32_768, 32_768);
    private static readonly VollarchivKatalogGrenzen CatalogLimits = new(Limits, 8, 32_768);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string SuccessJson = """{"ProtocolVersion":1,"Status":"ok","SchemaVersion":5,"CoreTablesChecked":8,"QuickCheck":"ok"}""";

    [Fact]
    public async Task ExitBeforeDelayedReaderStillConsumesCompleteProtocol()
    {
        using var fixture = new StageFixture();
        var stream = new ControlledStream();
        var child = new FakeProcess(stream, new MemoryStream(), 0, initiallyExited: true);
        var task = StageFixture.Runner(child).PruefenAsync(fixture.Stage, CancellationToken.None);
        await stream.Started.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            Assert.False(task.IsCompleted);
            stream.Release(Encoding.UTF8.GetBytes(SuccessJson));
            Assert.Equal("ok", (await task).Status);
            Assert.True(child.Disposed);
        }
        finally { stream.Release([]); await task.WaitAsync(TimeSpan.FromSeconds(2)); }
    }

    [Fact]
    public async Task CallerCancellationWhileReaderPendingPropagatesAfterOwnershipTransfer()
    {
        using var fixture = new StageFixture();
        var stream = new ControlledStream();
        var child = new FakeProcess(stream, new MemoryStream(), 0, initiallyExited: false);
        using var cts = new CancellationTokenSource();
        var task = StageFixture.Runner(child).PruefenAsync(fixture.Stage, cts.Token);
        await stream.Started.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(SqlitePruefQuarantaene.Haelt(fixture.Stage));
            Assert.True(File.Exists(fixture.Stage.DatenbankPfad));
            Assert.False(child.Disposed);
        }
        finally
        {
            child.SetExited(); stream.Release([]);
            if (SqlitePruefQuarantaene.Haelt(fixture.Stage))
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await SqlitePruefQuarantaene.BereinigeStageWennSicherAsync(fixture.Stage, cleanup.Token);
            }
        }
    }

    [Fact]
    public async Task ReaderFaultKillsOwnedFake()
    {
        using var fixture = new StageFixture();
        var stream = new ControlledStream();
        var child = new FakeProcess(stream, new MemoryStream(), 1, initiallyExited: false);
        var task = StageFixture.Runner(child).PruefenAsync(fixture.Stage, CancellationToken.None);
        await stream.Started.WaitAsync(TimeSpan.FromSeconds(2));
        stream.Fail(new IOException("fake"));
        Assert.Equal("failed", (await task).Status);
        Assert.True(child.Killed);
        Assert.True(child.Disposed);
    }

    [Fact]
    public async Task KillFailureAndUnconfirmedExitQuarantineStageUntilFakeSettles()
    {
        using var fixture = new StageFixture();
        var stream = new ControlledStream();
        var child = new FakeProcess(stream, new MemoryStream(), 0, initiallyExited: false, killThrows: true);
        using var cts = new CancellationTokenSource();
        var task = StageFixture.Runner(child).PruefenAsync(fixture.Stage, cts.Token);
        await stream.Started.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(SqlitePruefQuarantaene.Haelt(fixture.Stage));
            Assert.True(File.Exists(fixture.Stage.DatenbankPfad));
            Assert.False(child.Disposed);
        }
        finally
        {
            child.SetExited(); stream.Release([]);
            if (SqlitePruefQuarantaene.Haelt(fixture.Stage))
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await SqlitePruefQuarantaene.BereinigeStageWennSicherAsync(fixture.Stage, cleanup.Token);
            }
        }
        Assert.True(child.Disposed);
    }

    [Fact]
    public async Task MemoryStreamSuccessWithExitZeroReturnsOkAndDisposesChild()
    {
        using var fixture = new StageFixture();
        var child = new FakeProcess(new MemoryStream(Encoding.UTF8.GetBytes(SuccessJson)), new MemoryStream(), 0);
        Assert.Equal("ok", (await StageFixture.Runner(child).PruefenAsync(fixture.Stage, CancellationToken.None)).Status);
        Assert.True(child.Disposed);
    }

    [Fact]
    public async Task MemoryStreamSuccessWithExitOneReturnsProtocolInvalid()
    {
        using var fixture = new StageFixture();
        var child = new FakeProcess(new MemoryStream(Encoding.UTF8.GetBytes(SuccessJson)), new MemoryStream(), 1);
        Assert.Equal("protocol_invalid", (await StageFixture.Runner(child).PruefenAsync(fixture.Stage, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task OverflowWhileExitPendingKillsFakeChild()
    {
        using var fixture = new StageFixture();
        var child = new FakeProcess(new MemoryStream(new byte[2048]), new MemoryStream(), 1, initiallyExited: false);
        Assert.Equal("failed", (await StageFixture.Runner(child, 512).PruefenAsync(fixture.Stage, CancellationToken.None)).Status);
        Assert.True(child.Killed);
    }

    private sealed class FakeStarter(FakeProcess child) : IPruefprozessStarter
    {
        public IPruefprozess Start(string trustedExecutable, string databasePath) => child;
    }

    private sealed class FakeProcess : IPruefprozess
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Stream StandardOutput { get; }
        public Stream StandardError { get; }
        public bool HasExited { get; private set; }
        public int ExitCode { get; }
        public bool Killed { get; private set; }
        public bool Disposed { get; private set; }
        public bool KillThrows { get; }
        public FakeProcess(Stream stdout, Stream stderr, int exitCode, bool initiallyExited = true, bool killThrows = false)
        {
            StandardOutput = stdout; StandardError = stderr; ExitCode = exitCode; HasExited = initiallyExited; KillThrows = killThrows;
            if (initiallyExited) _exit.TrySetResult();
        }
        public Task WaitForExitAsync(CancellationToken cancellationToken) => _exit.Task.WaitAsync(cancellationToken);
        public void SetExited() { HasExited = true; _exit.TrySetResult(); }
        public void KillOwnedTree()
        {
            Killed = true;
            if (KillThrows) throw new IOException("fake kill");
            SetExited();
        }
        public void Dispose() => Disposed = true;
    }

    private sealed class ControlledStream : Stream
    {
        private readonly TaskCompletionSource<(byte[]? Data, Exception? Error)> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Started => _started.Task;
        private byte[]? _data;
        private int _offset;
        private bool _released;
        public void Release(byte[] bytes) => _release.TrySetResult((bytes, null));
        public void Fail(Exception error) => _release.TrySetResult((null, error));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();
            if (!_released)
            {
                var released = await _release.Task.ConfigureAwait(false); // deliberately ignores cancellation
                _released = true;
                if (released.Error is not null) throw released.Error;
                _data = released.Data ?? [];
            }
            if (_data is null || _offset >= _data.Length) return 0;
            var count = Math.Min(buffer.Length, _data.Length - _offset);
            _data.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StageFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "andytours-sqlite-runner-" + Guid.NewGuid().ToString("N"));
        private string Backup => Path.Combine(_root, "backup");
        private string Check => Path.Combine(_root, "check");
        private string Active => Path.Combine(_root, "active");
        private string ArchivePath => Path.Combine(Backup, "andytours_full_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.zip");
        public BereitgestelltePruefdatenbank Stage { get; }

        public StageFixture()
        {
            Directory.CreateDirectory(Backup); Directory.CreateDirectory(Check); Directory.CreateDirectory(Active);
            File.WriteAllBytes(ArchivePath, MakeArchive());
            using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
            var metadata = catalog.Liste(CancellationToken.None).GetAwaiter().GetResult().Eintraege.Single();
            var coordinator = new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, Limits, Array.Empty<string>());
            Stage = coordinator.Bereitstellen(new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash)).GetAwaiter().GetResult();
        }

        public static SqlitePruefRunner Runner(FakeProcess child, int outputLimit = 4096) =>
            new(typeof(SqlitePruefRunner).Assembly.Location, TimeSpan.FromSeconds(5), outputLimit, 1024 * 1024, new FakeStarter(child));

        private static byte[] MakeArchive()
        {
            byte[] db = [1, 2, 3, 4];
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                using (var entry = zip.CreateEntry("andytours.db", CompressionLevel.NoCompression).Open()) entry.Write(db);
                var item = new SicherungsmanifestEintrag("andytours.db", SicherungsmanifestEintragart.Datenbank, db.Length, Convert.ToHexString(SHA256.HashData(db)));
                var manifest = new Sicherungsmanifest(1, new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), [item]);
                using var stream = zip.CreateEntry("manifest.json").Open();
                JsonSerializer.Serialize(stream, manifest, JsonOptions);
            }
            return memory.ToArray();
        }

        public void Dispose()
        {
            if (SqlitePruefQuarantaene.Haelt(Stage)) return;
            Stage.Dispose();
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
