namespace AndyTours.Tests.Sicherung;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class SicherungsarchivPruefungTests
{
    private static readonly SicherungsmanifestGrenzen Grenzen = new(10, 1024, 4096, 200);
    private static readonly IReadOnlyList<string> Exclusions = Array.Empty<string>();
    private const string ErrorText = "Sicherungsarchiv ungültig.";

    [Fact]
    public async Task ValidArchive_VerifiesManifestAndPayloads_LeavesCallerStreamOpen()
    {
        using var archive = CreateArchive(new Dictionary<string, byte[]> { ["andytours.db"] = [1, 2, 3], ["2026/ride.fit"] = [4, 5] });
        var manifest = await Inspect(archive);
        Assert.Equal(2, manifest.Eintraege.Count);
        Assert.True(archive.CanRead);
        Assert.True(archive.CanSeek);
    }

    [Theory]
    [InlineData("missing-manifest")][InlineData("missing-payload")][InlineData("extra-entry")]
    [InlineData("duplicate-casefold")][InlineData("wrong-payload-case")][InlineData("hash")]
    [InlineData("length")][InlineData("symlink")][InlineData("root-directory")]
    public async Task InvalidArchiveShapes_AreRejectedWithFixedError(string kind)
    {
        var expectedPayloads = new Dictionary<string, byte[]> { ["andytours.db"] = [1, 2, 3] };
        var payloads = new Dictionary<string, byte[]>(expectedPayloads);
        var addManifest = true;
        string? extraName = null;
        int? extraAttributes = null;
        byte[]? dbOverride = null;
        switch (kind)
        {
            case "missing-manifest": addManifest = false; break;
            case "missing-payload": payloads.Clear(); break;
            case "extra-entry": extraName = "unlisted.bin"; break;
            case "duplicate-casefold": extraName = "ANDYTOURS.DB"; break;
            case "wrong-payload-case": payloads.Clear(); payloads["Andytours.db"] = [1, 2, 3]; break;
            case "hash": dbOverride = [9, 9, 9]; break;
            case "length": dbOverride = [1, 2, 3, 4]; break;
            case "symlink": extraAttributes = unchecked((int)0xA1FF0000); break;
            case "root-directory": extraName = "folder/"; break;
        }
        using var archive = CreateArchive(payloads, addManifest, extraName, extraAttributes, dbOverride, expectedPayloads);
        await AssertInvalid(archive);
    }

    [Theory]
    [InlineData("malformed")][InlineData("duplicate")][InlineData("unknown")]
    [InlineData("pascal-case")][InlineData("string-number")]
    public async Task StrictManifestJson_IsEnforced(string kind)
    {
        using var archive = CreateArchive(new Dictionary<string, byte[]> { ["andytours.db"] = [1, 2, 3] });
        var original = archive.ToArray();
        var canonical = ReadManifestJson(original);
        var replacementText = kind switch
        {
            "malformed" => "{",
            "duplicate" => canonical.Replace("\"formatversion\":1", "\"formatversion\":1,\"formatversion\":1", StringComparison.Ordinal),
            "unknown" => canonical.Insert(1, "\"unexpected\":0,"),
            "pascal-case" => canonical.Replace("\"formatversion\"", "\"Formatversion\"", StringComparison.Ordinal),
            "string-number" => canonical.Replace("\"formatversion\":1", "\"formatversion\":\"1\"", StringComparison.Ordinal),
            _ => throw new InvalidOperationException()
        };
        var replacement = Encoding.UTF8.GetBytes(replacementText);
        using var stream = CreateArchiveFromManifestBytes(original, replacement);
        await AssertInvalid(stream);
    }

    [Fact]
    public async Task ManifestAndArchiveLimitsAreChecked_AndCancellationReadsNothing()
    {
        using var archive = CreateArchive(new Dictionary<string, byte[]> { ["andytours.db"] = [1, 2, 3] });
        await AssertInvalid(archive, maxManifestBytes: 1);
        using var tooSmallArchive = CreateArchive(new Dictionary<string, byte[]> { ["andytours.db"] = [1, 2, 3] });
        await AssertInvalid(tooSmallArchive, maxArchiveBytes: tooSmallArchive.Length - 1);
        using var fileLimitArchive = CreateArchive(new Dictionary<string, byte[]> { ["andytours.db"] = [1, 2, 3] });
        var fileLimitError = await Assert.ThrowsAsync<StammdatenException>(() =>
            SicherungsarchivPruefung.Pruefe(fileLimitArchive, new SicherungsmanifestGrenzen(10, 2, 4096, 200), Exclusions, 1_000_000, 16_384, 16_384));
        Assert.Equal(ErrorText, fileLimitError.Message);
        Assert.True(fileLimitArchive.CanRead);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var tracked = new CountingStream(CreateArchiveBytes(new Dictionary<string, byte[]> { ["andytours.db"] = [1, 2, 3] }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SicherungsarchivPruefung.Pruefe(tracked, Grenzen, Exclusions, 1_000_000, 16_384, 16_384, cts.Token));
        Assert.Equal(0, tracked.BytesRead);
        Assert.True(tracked.CanRead);
    }

    private static MemoryStream CreateArchive(Dictionary<string, byte[]> payloads, bool addManifest = true,
        string? extraName = null, int? extraAttributes = null, byte[]? dbOverride = null,
        Dictionary<string, byte[]>? declaredPayloads = null)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in payloads)
            {
                var bytes = item.Key == "andytours.db" && dbOverride is not null ? dbOverride : item.Value;
                var entry = zip.CreateEntry(item.Key, CompressionLevel.NoCompression);
                using var content = entry.Open(); content.Write(bytes);
                if (item.Key == "andytours.db" && extraAttributes.HasValue) entry.ExternalAttributes = extraAttributes.Value;
            }
            if (extraName is not null)
            {
                var extra = zip.CreateEntry(extraName, CompressionLevel.NoCompression);
                if (extraAttributes.HasValue) extra.ExternalAttributes = extraAttributes.Value;
            }
            if (addManifest)
            {
                var manifestPayloads = declaredPayloads ?? payloads;
                var records = manifestPayloads.Select(pair =>
                {
                    var data = pair.Value;
                    var kind = pair.Key == "andytours.db" ? SicherungsmanifestEintragart.Datenbank : SicherungsmanifestEintragart.Datei;
                    return new SicherungsmanifestEintrag(pair.Key, kind, data.LongLength, Convert.ToHexString(SHA256.HashData(data)));
                }).ToArray();
                var manifest = new Sicherungsmanifest(1, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), records);
                var entry = zip.CreateEntry("manifest.json", CompressionLevel.NoCompression);
                using var content = entry.Open();
                JsonSerializer.Serialize(content, manifest, JsonOptions);
            }
        }
        stream.Position = 0; return stream;
    }

    private static MemoryStream CreateArchiveFromManifestBytes(byte[] original, byte[] replacement)
    {
        // Rebuild a fresh archive so each test owns an independent stream; malformed manifest is added alongside valid payload.
        using var source = new MemoryStream(original);
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var payload = new Dictionary<string, byte[]>();
        foreach (var entry in zip.Entries.Where(e => e.FullName != "manifest.json"))
        {
            using var input = entry.Open(); using var output = new MemoryStream(); input.CopyTo(output); payload[entry.FullName] = output.ToArray();
        }
        var result = new MemoryStream();
        using (var created = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var pair in payload) { using var output = created.CreateEntry(pair.Key).Open(); output.Write(pair.Value); }
            using var manifest = created.CreateEntry("manifest.json").Open(); manifest.Write(replacement);
        }
        result.Position = 0; return result;
    }

    private static byte[] CreateArchiveBytes(Dictionary<string, byte[]> payloads) { using var archive = CreateArchive(payloads); return archive.ToArray(); }

    private static string ReadManifestJson(byte[] archiveBytes)
    {
        using var source = new MemoryStream(archiveBytes);
        using var zip = new ZipArchive(source, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static Task<Sicherungsmanifest> Inspect(Stream stream, long maxArchiveBytes = 1_000_000, long maxManifestBytes = 16_384)
        => SicherungsarchivPruefung.Pruefe(stream, Grenzen, Exclusions, maxArchiveBytes, maxManifestBytes, 16_384);

    private static async Task AssertInvalid(Stream stream, long maxArchiveBytes = 1_000_000, long maxManifestBytes = 16_384)
    {
        var error = await Assert.ThrowsAsync<StammdatenException>(() => Inspect(stream, maxArchiveBytes, maxManifestBytes));
        Assert.Equal(ErrorText, error.Message);
        Assert.True(stream.CanRead); // caller owns it on failure too
    }

    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        public long BytesRead { get; private set; }
        public override int Read(byte[] buffer, int offset, int count) { var n = base.Read(buffer, offset, count); BytesRead += n; return n; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { var n = await base.ReadAsync(buffer, cancellationToken); BytesRead += n; return n; }
    }
}
