namespace AndyTours.Tests.Sicherung;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Xunit;

public sealed class DateisystemSicherungskatalogVollarchivTests : IDisposable
{
    private static readonly SicherungsarchivGrenzen ArchiveLimits =
        new(new SicherungsmanifestGrenzen(10, 4096, 8192, 200), 1_000_000, 32_768, 32_768);
    private static readonly VollarchivKatalogGrenzen FullLimits = new(ArchiveLimits, 8, 32_768);
    private static readonly string[] ExpectedLegacyOrder = ["2024-02-29_12-34-56", "2024-01-01_00-00-00"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _root = Path.Combine(Path.GetTempPath(), "andytours-unified-catalog-" + Guid.NewGuid().ToString("N"));
    private string BackupDir => Path.Combine(_root, "Sicherungen");

    public DateisystemSicherungskatalogVollarchivTests() => Directory.CreateDirectory(BackupDir);

    [Fact]
    public async Task MixedListKeepsLegacyFirstAndStableWhenFullArchiveIsAdded()
    {
        var legacyA = WriteLegacy("andytours_2024-02-29_12-34-56.db", [1, 2, 3]);
        var legacyB = WriteLegacy("andytours_2024-01-01_00-00-00.db", [4, 5]);
        var legacyABytes = File.ReadAllBytes(legacyA);
        var legacyBBytes = File.ReadAllBytes(legacyB);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);

        var first = await reader.Liste(CancellationToken.None);
        var idsBefore = first.Eintraege.ToDictionary(entry => entry.LegacyZeitstempel!, entry => entry.Id);
        Assert.Equal(2, first.Eintraege.Count);
        Assert.All(first.Eintraege, entry =>
        {
            Assert.Equal(Sicherungsart.LegacySqlite, entry.Art);
            Assert.Null(entry.ErstelltUtc);
        });

        var fullId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var archiveBytes = CreateArchive();
        File.WriteAllBytes(FullPath(fullId), archiveBytes);
        var after = await reader.Liste(CancellationToken.None);
        Assert.Equal(3, after.Eintraege.Count);
        Assert.Equal(ExpectedLegacyOrder, after.Eintraege.Take(2).Select(entry => entry.LegacyZeitstempel));
        var full = after.Eintraege[2];
        Assert.Equal(fullId, full.Id);
        Assert.Equal(Sicherungsart.Vollarchiv, full.Art);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), full.ErstelltUtc);
        Assert.Equal(archiveBytes.LongLength, full.Byteanzahl);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(archiveBytes)), full.InhaltHash);
        Assert.Equal(idsBefore[Timestamp(legacyA)], after.Eintraege[0].Id);
        Assert.Equal(idsBefore[Timestamp(legacyB)], after.Eintraege[1].Id);
        Assert.Equal(after.Eintraege.Count, after.Eintraege.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(legacyABytes, File.ReadAllBytes(legacyA));
        Assert.Equal(legacyBBytes, File.ReadAllBytes(legacyB));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(archiveBytes)),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FullPath(fullId)))));
    }

    [Fact]
    public async Task FullIdCollisionRegeneratesOnlyCollidingLegacyIdAndThenStabilizes()
    {
        WriteLegacy("andytours_2024-02-29_12-34-56.db", [1, 2, 3]);
        WriteLegacy("andytours_2024-01-01_00-00-00.db", [4, 5]);
        using var reader = new DateisystemSicherungskatalog(BackupDir, FullLimits);

        var initial = await reader.Liste(CancellationToken.None);
        var initialIds = initial.Eintraege.ToDictionary(entry => entry.LegacyZeitstempel!, entry => entry.Id);
        var collisionId = initialIds["2024-02-29_12-34-56"];
        File.WriteAllBytes(FullPath(collisionId), CreateArchive());

        var collisionResult = await reader.Liste(CancellationToken.None);
        Assert.Equal(3, collisionResult.Eintraege.Count);
        Assert.Equal(collisionId, collisionResult.Eintraege[2].Id);
        var regenerated = collisionResult.Eintraege[0].Id;
        Assert.NotEqual(Guid.Empty, regenerated);
        Assert.NotEqual(collisionId, regenerated);
        Assert.Equal(initialIds["2024-01-01_00-00-00"], collisionResult.Eintraege[1].Id);
        Assert.Equal(collisionResult.Eintraege.Count, collisionResult.Eintraege.Select(entry => entry.Id).Distinct().Count());

        var repeated = await reader.Liste(CancellationToken.None);
        Assert.Equal(collisionResult.Eintraege.Select(entry => entry.Id), repeated.Eintraege.Select(entry => entry.Id));
    }

    [Fact]
    public async Task MissingLimitsAndBadArchiveDoNotCommitPartialLegacyIdentityCache()
    {
        WriteLegacy("andytours_2024-02-29_12-34-56.db", [1, 2, 3]);
        WriteLegacy("andytours_2024-01-01_00-00-00.db", [4, 5]);
        using var reader = new DateisystemSicherungskatalog(BackupDir);
        var initial = await reader.Liste(CancellationToken.None);
        var priorIds = initial.Eintraege.Select(entry => entry.Id).ToArray();

        var fullId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var fullPath = FullPath(fullId);
        File.WriteAllBytes(fullPath, CreateArchive());
        var unavailable = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => reader.Liste(CancellationToken.None));
        Assert.Equal("Vollarchiv-Katalog ist derzeit nicht verfügbar.", unavailable.Message);
        Assert.Null(unavailable.InnerException);
        File.Delete(fullPath);

        var restoredLegacyOnly = await reader.Liste(CancellationToken.None);
        Assert.Equal(priorIds, restoredLegacyOnly.Eintraege.Select(entry => entry.Id));

        using var boundedReader = new DateisystemSicherungskatalog(BackupDir, FullLimits);
        var boundedInitial = await boundedReader.Liste(CancellationToken.None);
        var boundedPriorIds = boundedInitial.Eintraege.Select(entry => entry.Id).ToArray();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => boundedReader.Liste(cts.Token));

        var malformedPath = FullPath(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        File.WriteAllBytes(malformedPath, [1, 2, 3]);
        var malformed = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => boundedReader.Liste(CancellationToken.None));
        Assert.Null(malformed.InnerException);
        File.Delete(malformedPath);

        var afterFailure = await boundedReader.Liste(CancellationToken.None);
        Assert.Equal(boundedPriorIds, afterFailure.Eintraege.Select(entry => entry.Id));
    }

    private string WriteLegacy(string filename, byte[] bytes)
    {
        var path = Path.Combine(BackupDir, filename);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string Timestamp(string path) =>
        Path.GetFileNameWithoutExtension(path)["andytours_".Length..];

    private string FullPath(Guid id) =>
        Path.Combine(BackupDir, $"andytours_full_{id:N}.zip");

    private static byte[] CreateArchive()
    {
        var database = new byte[] { 1, 2, 3 };
        var ride = new byte[] { 4 };
        var payloads = new Dictionary<string, byte[]>
        {
            ["andytours.db"] = database,
            ["2026/ride.fit"] = ride
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in payloads)
            {
                using var entry = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
                entry.Write(bytes);
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
