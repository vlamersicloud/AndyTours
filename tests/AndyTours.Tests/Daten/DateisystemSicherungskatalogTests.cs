namespace AndyTours.Tests.Daten;

using System.Security.Cryptography;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Xunit;

public sealed class DateisystemSicherungskatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "andytours-catalog-" + Guid.NewGuid().ToString("N"));
    private string BackupDir => Path.Combine(_root, "Sicherungen");

    [Fact]
    public async Task Lists_only_strict_legacy_files_with_opaque_stable_ids_and_exact_metadata()
    {
        Directory.CreateDirectory(BackupDir);
        var bytes = new byte[] { 1, 3, 5, 7 };
        var newer = Path.Combine(BackupDir, "andytours_2024-02-29_12-34-56.db");
        var older = Path.Combine(BackupDir, "andytours_2024-01-01_00-00-00.db");
        await File.WriteAllBytesAsync(newer, bytes);
        await File.WriteAllBytesAsync(older, bytes);
        await File.WriteAllBytesAsync(Path.Combine(BackupDir, "andytours_bad.db"), [9]);
        await File.WriteAllBytesAsync(Path.Combine(BackupDir, "andytours_2024-03-01_00-00-00.db.tmp"), [8]);
        await File.WriteAllBytesAsync(Path.Combine(BackupDir, "other.zip"), [6]);
        var before = Snapshot(BackupDir);

        using var reader = new DateisystemSicherungskatalog(BackupDir);
        var first = await reader.Liste(default);
        Assert.Equal(2, first.Eintraege.Count);
        var newest = first.Eintraege[0];
        Assert.Equal(Sicherungsart.LegacySqlite, newest.Art);
        Assert.Equal((long)bytes.Length, newest.Byteanzahl);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), newest.InhaltHash);
        Assert.Null(newest.ErstelltUtc);
        Assert.Equal("2024-02-29_12-34-56", newest.LegacyZeitstempel);
        Assert.NotEqual(Guid.Empty, newest.Id);
        Assert.NotEqual(newest.Id, first.Eintraege[1].Id);
        var second = await reader.Liste(default);
        Assert.Equal(first.Eintraege.Select(x => x.Id), second.Eintraege.Select(x => x.Id));
        Assert.Equal(before, Snapshot(BackupDir));
    }

    [Fact]
    public async Task Changed_or_removed_and_recreated_file_gets_new_volatile_id()
    {
        Directory.CreateDirectory(BackupDir);
        var path = Path.Combine(BackupDir, "andytours_2024-02-29_12-34-56.db");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        using var reader = new DateisystemSicherungskatalog(BackupDir);
        var id = Assert.Single((await reader.Liste(default)).Eintraege).Id;
        await File.WriteAllBytesAsync(path, [4, 5, 6, 7]);
        var changed = Assert.Single((await reader.Liste(default)).Eintraege).Id;
        Assert.NotEqual(id, changed);
        File.Delete(path);
        Assert.Empty((await reader.Liste(default)).Eintraege);
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        Assert.NotEqual(id, Assert.Single((await reader.Liste(default)).Eintraege).Id);
    }

    [Fact]
    public async Task Missing_directory_is_empty_without_creation_and_relative_path_is_rejected()
    {
        using var reader = new DateisystemSicherungskatalog(BackupDir);
        Assert.Empty((await reader.Liste(default)).Eintraege);
        Assert.False(Directory.Exists(BackupDir));
        Assert.Throws<ArgumentException>(() => new DateisystemSicherungskatalog("relative/backups"));
    }

    [Fact]
    public async Task Opens_only_selected_id_and_hash_on_readonly_source_and_preserves_source()
    {
        Directory.CreateDirectory(BackupDir);
        var bytes = new byte[] { 2, 4, 6, 8 };
        var path = Path.Combine(BackupDir, "andytours_2024-02-29_12-34-56.db");
        await File.WriteAllBytesAsync(path, bytes);
        var before = Snapshot(BackupDir);
        using var reader = new DateisystemSicherungskatalog(BackupDir);
        var entry = Assert.Single((await reader.Liste(default)).Eintraege);

        await using (var source = await reader.Oeffnen(
            new SicherungsdownloadAnfrage(entry.Id, entry.InhaltHash), 100, CancellationToken.None))
        {
            Assert.Equal(entry.Id, source.Metadaten.Id);
            using var received = new MemoryStream();
            await source.Inhalt.CopyToAsync(received);
            Assert.Equal(bytes, received.ToArray());
        }

        Assert.Equal(before, Snapshot(BackupDir));
        await Assert.ThrowsAsync<StammdatenException>(() => reader.Oeffnen(
            new SicherungsdownloadAnfrage(Guid.NewGuid(), entry.InhaltHash), 100, CancellationToken.None));
        await Assert.ThrowsAsync<StammdatenException>(() => reader.Oeffnen(
            new SicherungsdownloadAnfrage(entry.Id, new string('A', 64)), 100, CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_and_disposal_do_not_write_catalog_files()
    {
        Directory.CreateDirectory(BackupDir);
        await File.WriteAllBytesAsync(Path.Combine(BackupDir, "andytours_2024-02-29_12-34-56.db"), [2, 4, 6]);
        var before = Snapshot(BackupDir);
        var reader = new DateisystemSicherungskatalog(BackupDir);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.Liste(cts.Token));
        Assert.Equal(before, Snapshot(BackupDir));
        reader.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => reader.Liste(default));
    }

    [Fact]
    public async Task Rejects_symlinked_backup_file_and_parent_directory_on_linux()
    {
        if (!OperatingSystem.IsLinux()) return;
        Directory.CreateDirectory(BackupDir);
        var target = Path.Combine(_root, "target.db");
        await File.WriteAllBytesAsync(target, [1, 2, 3]);
        var link = Path.Combine(BackupDir, "andytours_2024-02-29_12-34-56.db");
        File.CreateSymbolicLink(link, target);
        using var reader = new DateisystemSicherungskatalog(BackupDir);
        var error = await Assert.ThrowsAsync<StammdatenException>(() => reader.Liste(default));
        Assert.DoesNotContain(target, error.Message, StringComparison.Ordinal);

        var realDir = Path.Combine(_root, "real");
        Directory.CreateDirectory(realDir);
        var realBackupDir = Path.Combine(realDir, "Sicherungen");
        Directory.CreateDirectory(realBackupDir);
        await File.WriteAllBytesAsync(Path.Combine(realBackupDir, "andytours_2024-02-29_12-34-56.db"), [7]);
        var alias = Path.Combine(_root, "alias");
        Directory.CreateSymbolicLink(alias, realDir);
        using var parentReader = new DateisystemSicherungskatalog(Path.Combine(alias, "Sicherungen"));
        var parentError = await Assert.ThrowsAsync<StammdatenException>(() => parentReader.Liste(default));
        Assert.DoesNotContain(alias, parentError.Message, StringComparison.Ordinal);
    }

    private static string[] Snapshot(string directory) =>
        Directory.Exists(directory)
            ? Directory.GetFiles(directory).Order(StringComparer.Ordinal)
                .Select(path => Path.GetFileName(path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))
                .ToArray()
            : [];

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }
}
