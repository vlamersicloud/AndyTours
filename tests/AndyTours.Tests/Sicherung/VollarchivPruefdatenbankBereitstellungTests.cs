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

public sealed class VollarchivPruefdatenbankBereitstellungTests : IDisposable
{
    private static readonly SicherungsarchivGrenzen Limits =
        new(new SicherungsmanifestGrenzen(8, 4096, 8192, 200), 1_000_000, 32_768, 32_768);
    private static readonly VollarchivKatalogGrenzen CatalogLimits = new(Limits, 8, 32_768);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly byte[] DbBytes = [7, 8, 9, 10];
    private static readonly byte[] ActiveBytes = [90, 91, 92];
    private static readonly string[] ExpectedStageNames = ["andytours.db"];
    private static readonly Guid FullId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UnknownId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "andytours-restore-coordinator-" + Guid.NewGuid().ToString("N"));
    private string Backup => Path.Combine(_root, "backup");
    private string Check => Path.Combine(_root, "check");
    private string Active => Path.Combine(_root, "active");
    private string FullPath => Path.Combine(Backup, $"andytours_full_{FullId:N}.zip");

    public VollarchivPruefdatenbankBereitstellungTests()
    {
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Check);
        Directory.CreateDirectory(Active);
    }

    [Fact]
    public async Task ProvidesOnlyDatabasePayloadAndOwnsWorkspaceUntilDispose()
    {
        var archive = CreateArchive();
        File.WriteAllBytes(FullPath, archive);
        var activePath = Path.Combine(Active, "active.db");
        File.WriteAllBytes(activePath, ActiveBytes);
        using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
        var metadata = Assert.Single((await catalog.Liste(CancellationToken.None)).Eintraege);
        var sourceBytes = File.ReadAllBytes(FullPath);
        var coordinator = new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, Limits, Array.Empty<string>());
        var result = await coordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash.ToLowerInvariant()));
        using (result)
        {
            Assert.Equal(metadata, result.Quelle);
            Assert.Equal(1, result.Manifest.Formatversion);
            Assert.Equal(2, result.Manifest.Eintraege.Count);
            Assert.Equal(metadata.ErstelltUtc, result.Manifest.ErstelltUtc);
            var staged = File.ReadAllBytes(result.DatenbankPfad);
            Assert.Equal(DbBytes, staged);
            Assert.NotEqual(archive, staged);
            Assert.Equal(sourceBytes, File.ReadAllBytes(FullPath));
            Assert.Equal(ActiveBytes, File.ReadAllBytes(activePath));
            Assert.Equal(ExpectedStageNames, Directory.GetFileSystemEntries(Path.GetDirectoryName(result.DatenbankPfad)!)
                .Select(Path.GetFileName));
            using (new FileStream(FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            var dbPath = result.DatenbankPfad;
            result.Dispose();
            result.Dispose();
            Assert.False(File.Exists(dbPath));
            Assert.False(Directory.Exists(Path.GetDirectoryName(dbPath)!));
            Assert.Empty(Directory.GetFileSystemEntries(Check));
            Assert.Throws<ObjectDisposedException>(() => _ = result.DatenbankPfad);
            Assert.Throws<ObjectDisposedException>(() => _ = result.Quelle);
            Assert.Throws<ObjectDisposedException>(() => _ = result.Manifest);
        }
        Assert.Equal(sourceBytes, File.ReadAllBytes(FullPath));
        Assert.Equal(ActiveBytes, File.ReadAllBytes(activePath));
    }

    [Fact]
    public async Task LegacyIsExplicitlyUnsupportedAndDoesNotCreateWorkspace()
    {
        var legacyPath = Path.Combine(Backup, "andytours_2026-11-17_12-00-00.db");
        File.WriteAllBytes(legacyPath, DbBytes);
        using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
        var metadata = Assert.Single((await catalog.Liste(CancellationToken.None)).Eintraege);
        var before = File.ReadAllBytes(legacyPath);
        var coordinator = new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, Limits, Array.Empty<string>());
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => coordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash)));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.Empty(Directory.GetFileSystemEntries(Check));
        Assert.Equal(before, File.ReadAllBytes(legacyPath));
    }

    [Fact]
    public async Task StaleHashUnknownIdAndCancellationCreateNoWorkspace()
    {
        var archive = CreateArchive();
        File.WriteAllBytes(FullPath, archive);
        using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
        var metadata = Assert.Single((await catalog.Liste(CancellationToken.None)).Eintraege);
        var coordinator = new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, Limits, Array.Empty<string>());
        var staleHash = (metadata.InhaltHash[0] == 'A' ? 'B' : 'A') + metadata.InhaltHash[1..];
        var stale = await Assert.ThrowsAsync<StammdatenException>(() => coordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, staleHash)));
        Assert.Equal(StammdatenFehlerart.Konflikt, stale.Art);
        var unknown = await Assert.ThrowsAsync<StammdatenException>(() => coordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(UnknownId, new string('A', 64))));
        Assert.Equal(StammdatenFehlerart.NichtGefunden, unknown.Art);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash), cancelled.Token));
        Assert.Empty(Directory.GetFileSystemEntries(Check));
        Assert.Equal(archive, File.ReadAllBytes(FullPath));
    }

    [Fact]
    public async Task CopyFailureCleansWorkspaceAndReleasesSource()
    {
        var archive = CreateArchive();
        File.WriteAllBytes(FullPath, archive);
        var activePath = Path.Combine(Active, "active.db");
        File.WriteAllBytes(activePath, ActiveBytes);
        using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
        var metadata = Assert.Single((await catalog.Liste(CancellationToken.None)).Eintraege);
        var baseline = new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, Limits, Array.Empty<string>());
        using (var prepared = await baseline.Bereitstellen(new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash)))
            Assert.Equal(DbBytes, File.ReadAllBytes(prepared.DatenbankPfad));
        Assert.Empty(Directory.GetFileSystemEntries(Check));
        var coordinator = new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, Limits, ["2026"]);
        var ex = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => coordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash)));
        Assert.Null(ex.InnerException);
        Assert.Empty(Directory.GetFileSystemEntries(Check));
        Assert.Equal(archive, File.ReadAllBytes(FullPath));
        Assert.Equal(ActiveBytes, File.ReadAllBytes(activePath));
        using (new FileStream(FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public async Task MissingOrOverlappingServerRootsFailWithoutArtifacts()
    {
        var archive = CreateArchive();
        File.WriteAllBytes(FullPath, archive);
        var activePath = Path.Combine(Active, "active.db");
        File.WriteAllBytes(activePath, ActiveBytes);
        var activeBefore = File.ReadAllBytes(activePath);
        using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
        var metadata = Assert.Single((await catalog.Liste(CancellationToken.None)).Eintraege);
        var missingRoot = Path.Combine(_root, "missing-check");
        var missingCoordinator = new VollarchivPruefdatenbankBereitstellung(
            catalog, missingRoot, Active, Limits, Array.Empty<string>());
        Assert.False(Directory.Exists(missingRoot));
        var missing = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => missingCoordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash)));
        Assert.Null(missing.InnerException);
        Assert.False(Directory.Exists(missingRoot));
        Assert.Empty(Directory.GetFileSystemEntries(Check));

        var activeNamesBefore = Directory.GetFileSystemEntries(Active).Select(Path.GetFileName).Order().ToArray();
        var overlapCoordinator = new VollarchivPruefdatenbankBereitstellung(
            catalog, Active, Active, Limits, Array.Empty<string>());
        var overlap = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() => overlapCoordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash)));
        Assert.Null(overlap.InnerException);
        Assert.Empty(Directory.GetFileSystemEntries(Check));
        Assert.Equal(activeBefore, File.ReadAllBytes(activePath));
        Assert.Equal(activeNamesBefore, Directory.GetFileSystemEntries(Active).Select(Path.GetFileName).Order());
        Assert.Equal(archive, File.ReadAllBytes(FullPath));
        using (new FileStream(FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public async Task ConstructorSnapshotsExclusionsAsImmutablePolicy()
    {
        var archive = CreateArchive();
        File.WriteAllBytes(FullPath, archive);
        using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
        var metadata = Assert.Single((await catalog.Liste(CancellationToken.None)).Eintraege);
        var exclusions = new List<string>();
        var coordinator = new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, Limits, exclusions);
        exclusions.Add("2026");
        using var result = await coordinator.Bereitstellen(
            new WiederherstellungsvorschauAnfrage(metadata.Id, metadata.InhaltHash));
        Assert.Equal(DbBytes, File.ReadAllBytes(result.DatenbankPfad));
        result.Dispose();
        Assert.Empty(Directory.GetFileSystemEntries(Check));
    }

    [Fact]
    public void ConstructorValidationIsPureAndUsesSafeUnavailableErrors()
    {
        File.WriteAllBytes(FullPath, [1, 2, 3]);
        using var catalog = new DateisystemSicherungskatalog(Backup, CatalogLimits);
        var missingRoot = Path.Combine(_root, "not-created");
        _ = new VollarchivPruefdatenbankBereitstellung(catalog, missingRoot, Active, Limits, Array.Empty<string>());
        Assert.False(Directory.Exists(missingRoot));

        var badPath = Assert.Throws<SicherungskatalogNichtVerfuegbarException>(() =>
            new VollarchivPruefdatenbankBereitstellung(catalog, "relative", Active, Limits, Array.Empty<string>()));
        Assert.Null(badPath.InnerException);
        var badLimits = Limits with { MaxArchivBytes = 0 };
        var badLimitError = Assert.Throws<SicherungskatalogNichtVerfuegbarException>(() =>
            new VollarchivPruefdatenbankBereitstellung(catalog, Check, Active, badLimits, Array.Empty<string>()));
        Assert.Null(badLimitError.InnerException);
        Assert.True(Directory.Exists(Check));
        Assert.Empty(Directory.GetFileSystemEntries(Check));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(FullPath));
    }

    private static byte[] CreateArchive()
    {
        var ride = new byte[] { 1, 2 };
        var payloads = new Dictionary<string, byte[]>
        {
            ["andytours.db"] = DbBytes,
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
            using var manifestStream = zip.CreateEntry("manifest.json", CompressionLevel.NoCompression).Open();
            JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
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
