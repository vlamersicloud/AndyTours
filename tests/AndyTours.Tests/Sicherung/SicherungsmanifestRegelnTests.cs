namespace AndyTours.Tests.Sicherung;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class SicherungsmanifestRegelnTests
{
    private const string Hash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private static readonly DateTimeOffset Utc = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly SicherungsmanifestGrenzen Limits = new(10, 100, 100, 200);

    [Fact]
    public void Validates_manifest_and_returns_defensive_readonly_entry_copy()
    {
        var source = new List<SicherungsmanifestEintrag>
        {
            new("andytours.db", SicherungsmanifestEintragart.Datenbank, 10, Hash),
            new("empty.bin", SicherungsmanifestEintragart.Datei, 0, Hash),
            new("folder/data.bin", SicherungsmanifestEintragart.Datei, 2, Hash)
        };
        var manifest = new Sicherungsmanifest(1, Utc, source);
        var result = SicherungsmanifestRegeln.Pruefe(manifest, Limits, Array.Empty<string>());
        source.Clear();
        Assert.Equal(3, result.Eintraege.Count);
        Assert.Empty(manifest.Eintraege);
        Assert.Throws<NotSupportedException>(() => ((IList<SicherungsmanifestEintrag>)result.Eintraege).Clear());
    }

    [Fact]
    public void Accepts_inclusive_caller_limits_and_rejects_excess_counts_size_and_path()
    {
        var exact = new Sicherungsmanifest(1, Utc, new[]
        {
            new SicherungsmanifestEintrag("andytours.db", SicherungsmanifestEintragart.Datenbank, 98, Hash),
            new SicherungsmanifestEintrag("x", SicherungsmanifestEintragart.Datei, 2, Hash)
        });
        Assert.Equal(98, SicherungsmanifestRegeln.Pruefe(exact, new(2, 98, 100, 12), Array.Empty<string>()).Eintraege[0].Byteanzahl);
        AssertInvalid(exact, new(1, 100, 100, 200), Array.Empty<string>());
        AssertInvalid(exact, new(2, 97, 100, 200), Array.Empty<string>());
        AssertInvalid(exact, new(2, 100, 99, 200), Array.Empty<string>());
        AssertInvalid(exact, new(2, 100, 100, 11), Array.Empty<string>());
    }

    [Theory]
    [InlineData("/absolute")]
    [InlineData("C:/drive")]
    [InlineData("a\\b")]
    [InlineData("../escape")]
    [InlineData("a/../b")]
    [InlineData("a//b")]
    [InlineData("a/")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("bad\u0001name")]
    [InlineData("CON")]
    [InlineData("NUL.txt")]
    [InlineData("COM1.bin")]
    [InlineData("COM¹.bin")]
    [InlineData("LPT9")]
    [InlineData("decomposed-e\u0301")]
    public void Rejects_nonportable_or_noncanonical_paths(string path)
    {
        var manifest = Make(new("andytours.db", SicherungsmanifestEintragart.Datenbank, 1, Hash),
            new(path, SicherungsmanifestEintragart.Datei, 0, Hash));
        AssertInvalid(manifest, Limits, Array.Empty<string>());
    }

    [Theory]
    [InlineData("manifest.json")]
    [InlineData("andytours.db-wal")]
    [InlineData("andytours.db-shm")]
    [InlineData(".upload-abcd/file.fit")]
    [InlineData("folder/data.tmp")]
    [InlineData("Sicherungen/old.db")]
    public void Rejects_reserved_names_and_default_backup_folder(string path)
    {
        AssertInvalid(Make(Db(), new(path, SicherungsmanifestEintragart.Datei, 1, Hash)), Limits, Array.Empty<string>());
    }

    [Fact]
    public void Rejects_caller_excluded_prefix_at_segment_boundary()
    {
        AssertInvalid(Make(Db(), new("Staging/file.bin", SicherungsmanifestEintragart.Datei, 1, Hash)),
            Limits, ["staging"]);
        AssertValid(Make(Db(), new("StagingExtra/file.bin", SicherungsmanifestEintragart.Datei, 1, Hash)),
            Limits, ["staging"]);
    }

    [Fact]
    public void Rejects_casefold_duplicates_and_file_directory_collisions_in_either_order()
    {
        var db = Db();
        AssertInvalid(Make(db, new("Data.bin", SicherungsmanifestEintragart.Datei, 1, Hash),
            new("data.BIN", SicherungsmanifestEintragart.Datei, 1, Hash)), Limits, Array.Empty<string>());
        var parent = new SicherungsmanifestEintrag("node", SicherungsmanifestEintragart.Datei, 1, Hash);
        var child = new SicherungsmanifestEintrag("node/child", SicherungsmanifestEintragart.Datei, 1, Hash);
        AssertInvalid(Make(db, parent, child), Limits, Array.Empty<string>());
        AssertInvalid(Make(db, child, parent), Limits, Array.Empty<string>());
    }

    [Fact]
    public void Requires_exactly_one_nonempty_database_entry_and_valid_format_hash_and_utc()
    {
        AssertInvalid(Make(new SicherungsmanifestEintrag("other.db", SicherungsmanifestEintragart.Datenbank, 1, Hash)), Limits, Array.Empty<string>());
        AssertInvalid(Make(new SicherungsmanifestEintrag("andytours.db", SicherungsmanifestEintragart.Datei, 1, Hash)), Limits, Array.Empty<string>());
        AssertInvalid(Make(new SicherungsmanifestEintrag("andytours.db", SicherungsmanifestEintragart.Datenbank, 0, Hash)), Limits, Array.Empty<string>());
        AssertInvalid(new(2, Utc, [Db()]), Limits, Array.Empty<string>());
        AssertInvalid(new(1, Utc.ToOffset(TimeSpan.FromHours(1)), [Db()]), Limits, Array.Empty<string>());
        AssertInvalid(Make(Db() with { Art = (SicherungsmanifestEintragart)99 }), Limits, Array.Empty<string>());
        AssertInvalid(Make(Db() with { InhaltHash = "not-a-hash" }), Limits, Array.Empty<string>());
    }

    [Fact]
    public void Rejects_checked_total_overflow_with_fixed_safe_error()
    {
        var manifest = Make(Db(), new("huge.bin", SicherungsmanifestEintragart.Datei, long.MaxValue, Hash));
        var error = Assert.Throws<StammdatenException>(() =>
            SicherungsmanifestRegeln.Pruefe(manifest, new(3, long.MaxValue, long.MaxValue, 200), Array.Empty<string>()));
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
        Assert.Equal("Sicherungsmanifest ungültig.", error.Message);
        Assert.DoesNotContain("huge.bin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_null_entries_and_nonpositive_limit_values()
    {
        AssertInvalid(new Sicherungsmanifest(1, Utc, new SicherungsmanifestEintrag[] { null!, Db() }),
            Limits, Array.Empty<string>());
        AssertInvalid(Make(Db()), new(0, 10, 10, 10), Array.Empty<string>());
        AssertInvalid(Make(Db()), new(1, 0, 10, 10), Array.Empty<string>());
        AssertInvalid(Make(Db()), new(1, 10, 0, 10), Array.Empty<string>());
        AssertInvalid(Make(Db()), new(1, 10, 10, 0), Array.Empty<string>());
    }

    [Fact]
    public void Cancellation_is_propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            SicherungsmanifestRegeln.Pruefe(Make(Db()), Limits, Array.Empty<string>(), cts.Token));
    }

    private static SicherungsmanifestEintrag Db() =>
        new("andytours.db", SicherungsmanifestEintragart.Datenbank, 1, Hash);

    private static Sicherungsmanifest Make(params SicherungsmanifestEintrag[] entries) => new(1, Utc, entries);
    private static Sicherungsmanifest Make(SicherungsmanifestEintrag first, SicherungsmanifestEintrag second) =>
        new(1, Utc, [first, second]);

    private static void AssertValid(Sicherungsmanifest manifest, SicherungsmanifestGrenzen limits, IReadOnlyList<string> excluded) =>
        Assert.NotNull(SicherungsmanifestRegeln.Pruefe(manifest, limits, excluded));

    private static void AssertInvalid(Sicherungsmanifest manifest, SicherungsmanifestGrenzen limits, IReadOnlyList<string> excluded)
    {
        var error = Assert.Throws<StammdatenException>(() => SicherungsmanifestRegeln.Pruefe(manifest, limits, excluded));
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
        Assert.Equal("Sicherungsmanifest ungültig.", error.Message);
    }
}
