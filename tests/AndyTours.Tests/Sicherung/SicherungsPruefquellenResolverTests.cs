namespace AndyTours.Tests.Sicherung;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class SicherungsPruefquellenResolverTests : IDisposable
{
    private static readonly SicherungsarchivGrenzen ArchiveLimits =
        new(new SicherungsmanifestGrenzen(8, 4096, 8192, 200), 1_000_000, 32_768, 32_768);
    private static readonly VollarchivKatalogGrenzen FullLimits = new(ArchiveLimits, 8, 32_768);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly byte[] LegacyBytes = [11, 22, 33, 44, 55];
    private static readonly Guid FullId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UnknownId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "andytours-source-resolver-" + Guid.NewGuid().ToString("N"));
    private string BackupDir => Path.Combine(_root, "backups");
    private string LegacyPath => Path.Combine(BackupDir, "andytours_2026-11-17_12-00-00.db");
    private string FullPath => Path.Combine(BackupDir, $"andytours_full_{FullId:N}.zip");

    public SicherungsPruefquellenResolverTests() => Directory.CreateDirectory(BackupDir);

    [Fact]
    public async Task LegacySourceLeaseHasExpectedBytesAndDoesNotHoldCatalogGate()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var listed = await reader.Liste(CancellationToken.None);
        var metadata = Assert.Single(listed.Eintraege);
        var request = new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash.ToLowerInvariant());
        using var lease = await reader.OeffnePruefquelle(request, 100);
        Assert.Equal(metadata, lease.Metadaten);
        Assert.Equal(0, lease.Inhalt.Position);
        Assert.True(lease.Inhalt.CanRead);
        Assert.False(lease.Inhalt.CanWrite);
        Assert.Equal(LegacyBytes, await ReadAll(lease.Inhalt));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var relisted = await reader.Liste(timeout.Token);
        Assert.Single(relisted.Eintraege);
        var capturedStream = lease.Inhalt;
        lease.Dispose();
        Assert.False(capturedStream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Inhalt);
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Metadaten);
        Assert.Equal(LegacyBytes, File.ReadAllBytes(LegacyPath));
    }

    [Fact]
    public async Task AsyncDisposeClosesStreamAndKeepsCatalogUsable()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        await using var lease = await reader.OeffnePruefquelle(new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100);
        var captured = lease.Inhalt;
        await lease.DisposeAsync();
        Assert.False(captured.CanRead);
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Metadaten);
        Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
    }

    [Fact]
    public async Task FullArchiveResolvesFromVerifiedListedPathWithoutExposingItInMetadata()
    {
        var archive = CreateArchive();
        File.WriteAllBytes(FullPath, archive);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var listed = await reader.Liste(CancellationToken.None);
        var metadata = Assert.Single(listed.Eintraege);
        Assert.Equal(FullId, metadata.Id);
        Assert.DoesNotContain(BackupDir, JsonSerializer.Serialize(metadata));
        await using var lease = await reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100_000);
        Assert.Equal(metadata, lease.Metadaten);
        Assert.Equal(archive, await ReadAll(lease.Inhalt));
    }

    [Fact]
    public async Task UnknownAndUnlistedIdsAreNotGuessedFromDiskNames()
    {
        var guessedPath = Path.Combine(BackupDir, $"andytours_full_{UnknownId:N}.zip");
        File.WriteAllBytes(guessedPath, CreateArchive());
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var unlisted = await Assert.ThrowsAsync<StammdatenException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(UnknownId, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(guessedPath)))), 100_000));
        Assert.Equal(StammdatenFehlerart.NichtGefunden, unlisted.Art);
        Assert.True(File.Exists(guessedPath));
        Assert.Equal(StammdatenFehlerart.NichtGefunden,
            (await Assert.ThrowsAsync<StammdatenException>(() => reader.OeffnePruefquelle(
                new WiederherstellungsvorschauAnfrage(FullId, new string('A', 64)), 100_000))).Art);
    }

    [Fact]
    public async Task InvalidRequestAndInvalidOrTooSmallServerLimitsAreRejectedSafely()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        var invalid = await Assert.ThrowsAsync<StammdatenException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(Guid.Empty, new string('A', 64)), 100));
        Assert.Equal(StammdatenFehlerart.Ungueltig, invalid.Art);
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 0));
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), LegacyBytes.Length - 1));
    }

    [Fact]
    public async Task StaleHashDeletedAndChangedSameLengthSourcesFailWithoutLeakingDetails()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        var stale = await Assert.ThrowsAsync<StammdatenException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, new string('A', 64)), 100));
        Assert.Equal(StammdatenFehlerart.Konflikt, stale.Art);
        Assert.DoesNotContain(LegacyPath, stale.Message);

        File.Delete(LegacyPath);
        var missing = await Assert.ThrowsAsync<StammdatenException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100));
        Assert.Equal(StammdatenFehlerart.NichtGefunden, missing.Art);

        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var changedReader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var changedMetadata = Assert.Single((await changedReader.Liste(CancellationToken.None)).Eintraege);
        var oldWrite = File.GetLastWriteTimeUtc(LegacyPath);
        File.WriteAllBytes(LegacyPath, [55, 44, 33, 22, 11]);
        File.SetLastWriteTimeUtc(LegacyPath, oldWrite);
        var changed = await Assert.ThrowsAsync<StammdatenException>(() => changedReader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(changedMetadata.Id, changedMetadata.InhaltHash), 100));
        Assert.Equal(StammdatenFehlerart.Konflikt, changed.Art);
        Assert.DoesNotContain(LegacyPath, changed.Message);
        using (new FileStream(LegacyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public async Task FailedListingAndCancellationPreservePriorSourceIndex()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        var malformed = Path.Combine(BackupDir, $"andytours_full_{FullId:N}.zip");
        File.WriteAllBytes(malformed, [1, 2, 3]);
        var failed = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => reader.Liste(CancellationToken.None));
        Assert.Null(failed.InnerException);
        using (var lease = await reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100))
            Assert.Equal(LegacyBytes, await ReadAll(lease.Inhalt));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.Liste(cts.Token));
        using (var lease = await reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100))
            Assert.Equal(LegacyBytes, await ReadAll(lease.Inhalt));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(malformed));
    }

    [Fact]
    public async Task CancelledResolveClosesHandleAndReleasesGate()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100, cancelled.Token));
        using (new FileStream(LegacyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Single((await reader.Liste(timeout.Token)).Eintraege);
    }

    [Fact]
    public async Task SymlinkedListedFileIsUnavailableAndTargetRemainsUnchanged()
    {
        if (OperatingSystem.IsWindows()) return;
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        var target = Path.Combine(_root, "outside-target.db");
        File.WriteAllBytes(target, LegacyBytes);
        File.Delete(LegacyPath);
        File.CreateSymbolicLink(LegacyPath, target);
        var ex = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100));
        Assert.Null(ex.InnerException);
        Assert.True(File.GetAttributes(LegacyPath).HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal(LegacyBytes, File.ReadAllBytes(target));
    }

    [Fact]
    public async Task SymlinkedBackupAncestorIsUnavailableAndTargetRemainsUnchanged()
    {
        if (OperatingSystem.IsWindows()) return;
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        var moved = Path.Combine(_root, "moved");
        Directory.Move(BackupDir, moved);
        Directory.CreateSymbolicLink(BackupDir, moved);
        var ex = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100));
        Assert.Null(ex.InnerException);
        Assert.Equal(LegacyBytes, File.ReadAllBytes(Path.Combine(moved, Path.GetFileName(LegacyPath))));
    }

    [Fact]
    public async Task DirectoryReplacementOfListedFileIsUnavailable()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        File.Delete(LegacyPath);
        Directory.CreateDirectory(LegacyPath);
        var ex = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100));
        Assert.Null(ex.InnerException);
        Assert.True(Directory.Exists(LegacyPath));
    }

    [Fact]
    public async Task MissingDirectoryClearsSourceIndex()
    {
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        using var reader = new DateisystemSicherungskatalog(BackupDir);
        var metadata = Assert.Single((await reader.Liste(CancellationToken.None)).Eintraege);
        Directory.Delete(BackupDir, recursive: true);
        Assert.Empty((await reader.Liste(CancellationToken.None)).Eintraege);
        Directory.CreateDirectory(BackupDir);
        File.WriteAllBytes(LegacyPath, LegacyBytes);
        var missing = await Assert.ThrowsAsync<StammdatenException>(() => reader.OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), 100));
        Assert.Equal(StammdatenFehlerart.NichtGefunden, missing.Art);
    }

    private static async Task<byte[]> ReadAll(Stream stream)
    {
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private static byte[] CreateArchive()
    {
        var database = new byte[] { 1, 2, 3 };
        var ride = new byte[] { 4, 5 };
        var payloads = new Dictionary<string, byte[]>
        {
            ["andytours.db"] = database,
            ["2026/ride.fit"] = ride
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in payloads)
            {
                using var entry = zip.CreateEntry(item.Key, CompressionLevel.NoCompression).Open();
                entry.Write(item.Value);
            }
            var entries = payloads.Select(item => new SicherungsmanifestEintrag(
                item.Key,
                item.Key == "andytours.db" ? SicherungsmanifestEintragart.Datenbank : SicherungsmanifestEintragart.Datei,
                item.Value.LongLength,
                Convert.ToHexString(SHA256.HashData(item.Value)))).ToArray();
            var manifest = new Sicherungsmanifest(1,
                new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), entries);
            using var content = zip.CreateEntry("manifest.json", CompressionLevel.NoCompression).Open();
            JsonSerializer.Serialize(content, manifest, JsonOptions);
        }
        return stream.ToArray();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }
}
