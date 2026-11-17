namespace AndyTours.Tests.Sicherung;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class SicherungsdatenbankExtraktionTests
{
    private static readonly byte[] DbBytes = [1, 3, 5, 7, 9, 11];
    private static readonly byte[] PayloadBytes = [2, 4, 6];
    private static readonly DateTimeOffset Utc = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly SicherungsarchivGrenzen Bounds = new(new(10, 1_000_000, 2_000_000, 512), 4_000_000, 100_000, 200_000);
    private static readonly string[] Excluded = [];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task KopiertNurDatenbankPayloadUndLaesstCallerStreamsOffen()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var destination = new MemoryStream();
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));

        var manifest = await SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash);

        Assert.Equal(2, manifest.Eintraege.Count);
        Assert.Equal(1, manifest.Formatversion);
        Assert.Equal(Utc, manifest.ErstelltUtc);
        Assert.Equal(DbBytes, destination.ToArray());
        Assert.True(archive.CanRead);
        Assert.True(destination.CanWrite);
    }

    [Fact]
    public async Task FalscherGesamtHashLehntVorSchreibenSicherAb()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var destination = new MemoryStream();
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, new string('0', 64)));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.Empty(destination.ToArray());
        Assert.DoesNotContain("PRIVATE", ex.Message);
    }

    [Fact]
    public async Task BeschaedigterNichtDatenbankPayloadWirdVorAusgabeAbgelehnt()
    {
        using var archive = BuildArchive(DbBytes, [2, 4, 6], manifestPayloadHash: new string('0', 64));
        using var destination = new MemoryStream();
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash));
        Assert.Empty(destination.ToArray());
    }

    [Fact]
    public async Task AbbruchVorBeginnLaesstStreamsOffenUndSchreibtNichts()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var destination = new MemoryStream();
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash, cts.Token));
        Assert.Empty(destination.ToArray());
        Assert.True(archive.CanRead);
        Assert.True(destination.CanWrite);
    }

    [Fact]
    public async Task NichtLeeresZielBleibtUnveraendert()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var destination = new MemoryStream([42]);
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash));
        Assert.Equal([42], destination.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GrenzenLehnenBeiLeeremZielVorAusgabeAb(int grenze)
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var destination = new MemoryStream();
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        var limits = grenze switch
        {
            0 => Bounds with { MaxArchivBytes = 1 },
            1 => Bounds with { Manifest = Bounds.Manifest with { MaxDateiBytes = DbBytes.Length - 1 } },
            2 => Bounds with { Manifest = Bounds.Manifest with { MaxGesamtBytes = DbBytes.Length + PayloadBytes.Length - 1 } },
            _ => throw new ArgumentOutOfRangeException(nameof(grenze))
        };
        await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, limits, Excluded, hash));
        Assert.Empty(destination.ToArray());
    }

    [Fact]
    public async Task SelbststreamWirdAbgelehntUndArchivBleibtUnveraendert()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        var before = archive.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(before));
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, archive, Bounds, Excluded, hash));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.Equal(before, archive.ToArray());
    }

    [Fact]
    public async Task SchreibgeschuetztesZielWirdAbgelehntUndBleibtOffen()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var destination = new MemoryStream(Array.Empty<byte>(), writable: false);
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.True(archive.CanRead);
        Assert.True(destination.CanRead);
        Assert.False(destination.CanWrite);
    }

    [Fact]
    public async Task SchreibfehlerWirdUnveraendertWeitergegebenUndStreamsBleibenOffen()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var destination = new ThrowingWriteStream();
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        var ex = await Assert.ThrowsAsync<IOException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash));
        Assert.Same(destination.ExpectedException, ex);
        Assert.True(archive.CanRead);
        Assert.True(destination.CanWrite);
    }

    [Fact]
    public async Task AbbruchWaehrenKopieLaesstCallerStreamsOffen()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        using var cts = new CancellationTokenSource();
        using var destination = new CancellingWriteStream(cts);
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash, cts.Token));
        Assert.True(archive.CanRead);
        Assert.True(destination.CanWrite);
        Assert.NotEmpty(destination.ToArray());
    }

    [Fact]
    public async Task ArchivmutationNachKopieWirdDurchAbschlusshashErkannt()
    {
        using var archive = BuildArchive(DbBytes, PayloadBytes);
        var hash = Convert.ToHexString(SHA256.HashData(archive.ToArray()));
        var originalLength = archive.Length;
        var mutationRan = false;
        using var destination = new MutatingFlushStream(() =>
        {
            archive.GetBuffer()[0] ^= 1;
            mutationRan = true;
        });
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsdatenbankExtraktion.Kopiere(archive, destination, Bounds, Excluded, hash));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.True(mutationRan);
        Assert.Equal(originalLength, archive.Length);
        Assert.True(archive.CanRead);
        Assert.True(destination.CanWrite);
    }

    private static MemoryStream BuildArchive(byte[] db, byte[] payload, string? manifestPayloadHash = null)
    {
        var entries = new[]
        {
            new SicherungsmanifestEintrag("andytours.db", SicherungsmanifestEintragart.Datenbank, db.Length, Convert.ToHexString(SHA256.HashData(db))),
            new SicherungsmanifestEintrag("example.fit", SicherungsmanifestEintragart.Datei, payload.Length,
                manifestPayloadHash ?? Convert.ToHexString(SHA256.HashData(payload)))
        };
        var manifest = new Sicherungsmanifest(1, Utc, entries);
        var json = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var output = zip.CreateEntry("manifest.json").Open()) output.Write(json);
            using (var output = zip.CreateEntry("andytours.db").Open()) output.Write(db);
            using (var output = zip.CreateEntry("example.fit").Open()) output.Write(payload);
        }
        stream.Position = 0;
        return stream;
    }

    private sealed class ThrowingWriteStream : MemoryStream
    {
        public IOException ExpectedException { get; } = new("fixture write failure");

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(ExpectedException);
    }

    private sealed class CancellingWriteStream(CancellationTokenSource cts) : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await base.WriteAsync(buffer, cancellationToken);
            cts.Cancel();
        }
    }

    private sealed class MutatingFlushStream(Action mutate) : MemoryStream
    {
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            mutate();
            return Task.CompletedTask;
        }
    }
}
