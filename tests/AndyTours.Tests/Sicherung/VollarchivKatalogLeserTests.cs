namespace AndyTours.Tests.Sicherung;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class VollarchivKatalogLeserTests : IDisposable
{
    private static readonly SicherungsarchivGrenzen ArchivGrenzen =
        new(new SicherungsmanifestGrenzen(10, 4096, 8192, 200), 1_000_000, 32_768, 32_768);
    private static readonly VollarchivKatalogGrenzen StandardLimits = new(ArchivGrenzen, 4, 8192);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly byte[] Datenbank = [1, 2, 3];
    private static readonly byte[] Fahrt = [4];
    private static readonly IReadOnlyDictionary<string, byte[]> Nutzdaten = new Dictionary<string, byte[]>
    {
        ["andytours.db"] = Datenbank,
        ["2026/ride.fit"] = Fahrt
    };

    private readonly string _basis = Path.Combine(Path.GetTempPath(), "AndyTours-fullcatalog-" + Guid.NewGuid().ToString("N"));

    public VollarchivKatalogLeserTests()
    {
        Directory.CreateDirectory(_basis);
    }

    [Fact]
    public async Task MissingFolderAndFolderWithoutExactCandidatesRemainEmptyWithoutLimits()
    {
        var missing = Path.Combine(_basis, "missing");
        Assert.Empty(await VollarchivKatalogLeser.Liste(missing, null, CancellationToken.None));

        var folder = Path.Combine(_basis, "backups");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "unrelated.bin"), "leave me");
        File.WriteAllBytes(Path.Combine(folder, "andytours_full_bad.zip"), [9]);
        File.WriteAllBytes(Path.Combine(folder, "andytours_full_" + Guid.Empty.ToString("N") + ".zip"), [8]);
        var uppercaseId = Guid.Parse("abcdefab-cdef-abcd-efab-cdefabcdefab");
        File.WriteAllBytes(Path.Combine(folder, "andytours_full_" + uppercaseId.ToString("N").ToUpperInvariant() + ".zip"), [7]);
        File.WriteAllBytes(Path.Combine(folder, "andytours_full_" + Guid.NewGuid().ToString("D") + ".zip"), [6]);
        File.WriteAllBytes(Path.Combine(folder, "andytours_full_" + Guid.NewGuid().ToString("N") + ".ZIP"), [5]);

        Assert.Empty(await VollarchivKatalogLeser.Liste(folder, null, CancellationToken.None));
        Assert.Equal("leave me", File.ReadAllText(Path.Combine(folder, "unrelated.bin")));
    }

    [Fact]
    public async Task ValidArchiveReturnsStableMetadataAndHashWithoutChangingFiles()
    {
        var folder = CreateFolder("valid");
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var path = ArchivePath(folder, id);
        var bytes = CreateArchive(Nutzdaten);
        File.WriteAllBytes(path, bytes);
        var before = File.ReadAllBytes(path);
        var unrelated = Path.Combine(folder, "notes.txt");
        File.WriteAllText(unrelated, "preserve");

        var first = Assert.Single(await VollarchivKatalogLeser.Liste(folder, StandardLimits, CancellationToken.None));
        var second = Assert.Single(await VollarchivKatalogLeser.Liste(folder, StandardLimits, CancellationToken.None));
        Assert.Equal(id, first.Id);
        Assert.Equal(Sicherungsart.Vollarchiv, first.Art);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), first.ErstelltUtc);
        Assert.Equal(bytes.LongLength, first.Byteanzahl);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), first.InhaltHash);
        Assert.Null(first.LegacyZeitstempel);
        Assert.Equal(first, second);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal("preserve", File.ReadAllText(unrelated));
    }

    [Fact]
    public async Task MissingOrInvalidLimitsAndMalformedArchiveFailClosedWithoutPartialResults()
    {
        var folder = CreateFolder("invalid-limits");
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var path = ArchivePath(folder, id);
        var valid = CreateArchive(Nutzdaten);
        File.WriteAllBytes(path, valid);

        var missing = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, null, CancellationToken.None));
        Assert.Equal("Vollarchiv-Katalog ist derzeit nicht verfügbar.", missing.Message);
        Assert.Null(missing.InnerException);
        var invalidLimits = StandardLimits with { MaxVollarchive = 0 };
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, invalidLimits, CancellationToken.None));

        var malformed = Path.Combine(folder, "andytours_full_33333333333333333333333333333333.zip");
        File.WriteAllBytes(malformed, [1, 2, 3, 4]);
        var unchangedValid = File.ReadAllBytes(path);
        var malformedError = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, StandardLimits, CancellationToken.None));
        Assert.Equal("Vollarchiv-Katalog ist derzeit nicht verfügbar.", malformedError.Message);
        Assert.Null(malformedError.InnerException);
        Assert.Equal(unchangedValid, File.ReadAllBytes(path));
        File.Delete(malformed);

        var mismatchActual = Nutzdaten.ToDictionary(pair => pair.Key, pair => pair.Value);
        mismatchActual["2026/ride.fit"] = [9];
        var mismatch = Path.Combine(folder, "andytours_full_99999999999999999999999999999999.zip");
        File.WriteAllBytes(mismatch, CreateArchive(mismatchActual, Nutzdaten));
        var hashError = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, StandardLimits, CancellationToken.None));
        Assert.Equal("Vollarchiv-Katalog ist derzeit nicht verfügbar.", hashError.Message);
        Assert.Null(hashError.InnerException);
        Assert.Equal(unchangedValid, File.ReadAllBytes(path));
        Assert.Equal(valid, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task CountAndAggregateExpandedPayloadBudgetsAreEnforcedAcrossArchives()
    {
        var folder = CreateFolder("budgets");
        File.WriteAllBytes(ArchivePath(folder, Guid.Parse("44444444-4444-4444-4444-444444444444")), CreateArchive(Nutzdaten));
        File.WriteAllBytes(ArchivePath(folder, Guid.Parse("55555555-5555-5555-5555-555555555555")), CreateArchive(Nutzdaten));

        var tooFew = StandardLimits with { MaxVollarchive = 1, MaxNutzdatenBytes = 8192 };
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, tooFew, CancellationToken.None));

        var aggregateTooSmall = StandardLimits with { MaxVollarchive = 2, MaxNutzdatenBytes = 7 };
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, aggregateTooSmall, CancellationToken.None));

        var exactBudget = StandardLimits with { MaxVollarchive = 2, MaxNutzdatenBytes = 8 };
        Assert.Equal(2, (await VollarchivKatalogLeser.Liste(folder, exactBudget, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task ExactNamedDirectoryAndFileOrRootSymlinksAreRejected()
    {
        var folder = CreateFolder("unsafe");
        var directoryId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        Directory.CreateDirectory(ArchivePath(folder, directoryId));
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, StandardLimits, CancellationToken.None));

        if (OperatingSystem.IsWindows()) return;

        var targetFolder = CreateFolder("root-target");
        var rootAlias = Path.Combine(_basis, "root-link");
        Directory.CreateSymbolicLink(rootAlias, targetFolder);
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(rootAlias, StandardLimits, CancellationToken.None));

        var linkFolder = CreateFolder("file-link");
        var target = Path.Combine(_basis, "target.zip");
        var targetBytes = CreateArchive(Nutzdaten);
        File.WriteAllBytes(target, targetBytes);
        var fileId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        File.CreateSymbolicLink(ArchivePath(linkFolder, fileId), target);
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(linkFolder, StandardLimits, CancellationToken.None));
        Assert.True(File.Exists(target));
        Assert.Equal(targetBytes, File.ReadAllBytes(target));
    }

    [Fact]
    public async Task CancellationAndArchiveSizeLimitAreEnforced()
    {
        var folder = CreateFolder("limits");
        var id = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var bytes = CreateArchive(Nutzdaten);
        File.WriteAllBytes(ArchivePath(folder, id), bytes);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => VollarchivKatalogLeser.Liste(folder, StandardLimits, cts.Token));

        var archiveCap = bytes.LongLength - 1;
        var tooSmall = StandardLimits with
        {
            Archiv = StandardLimits.Archiv with
            {
                MaxArchivBytes = archiveCap,
                MaxManifestBytes = Math.Min(StandardLimits.Archiv.MaxManifestBytes, archiveCap),
                MaxZentralverzeichnisBytes = Math.Min(StandardLimits.Archiv.MaxZentralverzeichnisBytes, archiveCap)
            }
        };
        var error = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(
            () => VollarchivKatalogLeser.Liste(folder, tooSmall, CancellationToken.None));
        Assert.Equal("Vollarchiv-Katalog ist derzeit nicht verfügbar.", error.Message);
        Assert.Null(error.InnerException);
    }

    private string CreateFolder(string name)
    {
        var path = Path.Combine(_basis, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ArchivePath(string folder, Guid id) =>
        Path.Combine(folder, $"andytours_full_{id:N}.zip");

    private static byte[] CreateArchive(
        IReadOnlyDictionary<string, byte[]> payloads,
        IReadOnlyDictionary<string, byte[]>? declaredPayloads = null)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in payloads)
            {
                using var entry = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
                entry.Write(bytes);
            }

            var records = (declaredPayloads ?? payloads).Select(item =>
            {
                var kind = item.Key == "andytours.db"
                    ? SicherungsmanifestEintragart.Datenbank
                    : SicherungsmanifestEintragart.Datei;
                return new SicherungsmanifestEintrag(item.Key, kind, item.Value.LongLength,
                    Convert.ToHexString(SHA256.HashData(item.Value)));
            }).ToArray();
            var manifest = new Sicherungsmanifest(1,
                new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), records);
            using var manifestStream = zip.CreateEntry("manifest.json", CompressionLevel.NoCompression).Open();
            JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
        }

        return stream.ToArray();
    }

    public void Dispose()
    {
        try { Directory.Delete(_basis, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }
}
