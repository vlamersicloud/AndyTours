namespace AndyTours.Tests.Sicherung;

using System.Security;
using System.Text;
using AndyTours.Client.Sicherung;
using Xunit;

public sealed class DateisystemSicherungsjournalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "andy-journal-tests", Guid.NewGuid().ToString("N"));
    private static readonly Uri Backend = new("https://unit.test/api/");
    private static string Key => SicherungsjournalBackend.Kennung(Backend);
    private string Pending => Path.Combine(_root, Key + ".pending");
    private string LockPath => Path.Combine(_root, Key + ".lock");

    [Fact]
    public async Task PersistsNonemptyIdBeforeReturningAndReopenRetainsIt()
    {
        var journal = CreateJournal();
        Guid id;
        using (var lease = await journal.Oeffnen(CancellationToken.None))
        {
            id = lease.VorgangsId;
            Assert.NotEqual(Guid.Empty, id);
            Assert.Equal(id.ToString("N"), Encoding.ASCII.GetString(File.ReadAllBytes(Pending)));
            Assert.Equal(32, new FileInfo(Pending).Length);
            Assert.True(File.Exists(LockPath));
            lease.Dispose();
            lease.Dispose();
            Assert.True(File.Exists(Pending));
            Assert.True(File.Exists(LockPath));
        }

        using var reopened = await CreateJournal().Oeffnen(CancellationToken.None);
        Assert.Equal(id, reopened.VorgangsId);
    }

    [Fact]
    public async Task ExclusiveLockRejectsSecondOpenAndPreservesActivePending()
    {
        var journal = CreateJournal();
        using var lease = await journal.Oeffnen(CancellationToken.None);
        var persisted = File.ReadAllBytes(Pending);
        var ex = await Assert.ThrowsAsync<IOException>(() => journal.Oeffnen(CancellationToken.None));
        Assert.DoesNotContain(_root, ex.Message, StringComparison.Ordinal);
        Assert.Equal(persisted, File.ReadAllBytes(Pending));
        Assert.NotEqual(Guid.Empty, lease.VorgangsId);
    }

    [Fact]
    public async Task RejectsNonemptyForeignLockWithoutChangingIt()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(LockPath, "foreign-lock-data");
        var before = File.ReadAllBytes(LockPath);
        var ex = await Assert.ThrowsAsync<IOException>(() => CreateJournal().Oeffnen(CancellationToken.None));
        Assert.DoesNotContain(_root, ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(LockPath));
    }

    [Fact]
    public async Task DifferentBackendUsesIndependentPendingOperation()
    {
        using var first = await CreateJournal().Oeffnen(CancellationToken.None);
        var firstBytes = File.ReadAllBytes(Pending);
        var otherKey = SicherungsjournalBackend.Kennung(new Uri("https://other.test/api/"));
        using var second = await new DateisystemSicherungsjournal(_root, new Uri("https://other.test/api/")).Oeffnen(CancellationToken.None);
        Assert.NotEqual(first.VorgangsId, second.VorgangsId);
        Assert.Equal(firstBytes, File.ReadAllBytes(Pending));
        Assert.Equal(second.VorgangsId.ToString("N"), Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(_root, otherKey + ".pending"))));
    }

    [Fact]
    public async Task MatchingConfirmationClearsOnlyPendingAndNextOpenCreatesNewId()
    {
        var journal = CreateJournal();
        Guid firstId;
        using (var lease = await journal.Oeffnen(CancellationToken.None))
        {
            firstId = lease.VorgangsId;
            await lease.Bestaetigen(firstId, CancellationToken.None);
            Assert.False(File.Exists(Pending));
            Assert.True(File.Exists(LockPath));
            await lease.Bestaetigen(firstId, CancellationToken.None);
        }

        using var next = await journal.Oeffnen(CancellationToken.None);
        Assert.NotEqual(firstId, next.VorgangsId);
    }

    [Fact]
    public async Task WrongOrChangedPendingIdIsNeverDeleted()
    {
        var journal = CreateJournal();
        using var lease = await journal.Oeffnen(CancellationToken.None);
        var before = File.ReadAllBytes(Pending);
        await Assert.ThrowsAsync<IOException>(() => lease.Bestaetigen(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(before, File.ReadAllBytes(Pending));

        var different = Guid.NewGuid();
        File.WriteAllText(Pending, different.ToString("N"));
        var altered = File.ReadAllBytes(Pending);
        await Assert.ThrowsAsync<IOException>(() => lease.Bestaetigen(lease.VorgangsId, CancellationToken.None));
        Assert.Equal(altered, File.ReadAllBytes(Pending));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0000000000000000000000000000000")]
    [InlineData("000000000000000000000000000000000")]
    [InlineData("gg000000000000000000000000000000")]
    [InlineData("00000000000000000000000000000000")]
    public async Task CorruptPendingFailsClosedWithoutReplacingBytes(string raw)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Pending, raw);
        var before = File.ReadAllBytes(Pending);
        var ex = await Assert.ThrowsAsync<IOException>(() => CreateJournal().Oeffnen(CancellationToken.None));
        Assert.DoesNotContain(_root, ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(Pending));
    }

    [Fact]
    public async Task PreCancellationDoesNotCreateRootAndConfirmationCancellationRetainsId()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var missing = Path.Combine(_root, "cancelled");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateJournal(missing).Oeffnen(cts.Token));
        Assert.False(Directory.Exists(missing));

        using var lease = await CreateJournal().Oeffnen(CancellationToken.None);
        var before = File.ReadAllBytes(Pending);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lease.Bestaetigen(lease.VorgangsId, cts.Token));
        Assert.Equal(before, File.ReadAllBytes(Pending));
    }

    [Fact]
    public async Task DisposedLeaseGuardsAccessAndKeepsPending()
    {
        using var lease = await CreateJournal().Oeffnen(CancellationToken.None);
        var id = lease.VorgangsId;
        lease.Dispose();
        lease.Dispose();
        Assert.True(File.Exists(Pending));
        Assert.Throws<ObjectDisposedException>(() => _ = lease.VorgangsId);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => lease.Bestaetigen(id, CancellationToken.None));
    }

    [Fact]
    public async Task PreservesUnrelatedAndStaleTempFiles()
    {
        Directory.CreateDirectory(_root);
        var unrelated = Path.Combine(_root, "unrelated.txt");
        var staleTemp = Path.Combine(_root, Key + ".stale.tmp");
        File.WriteAllText(unrelated, "keep");
        File.WriteAllText(staleTemp, "stale");
        using var lease = await CreateJournal().Oeffnen(CancellationToken.None);
        Assert.Equal("keep", File.ReadAllText(unrelated));
        Assert.Equal("stale", File.ReadAllText(staleTemp));
    }

    [Fact]
    public async Task CreatesPrivateUnixDirectoryAndFilesWithoutChangingExistingRootPermissions()
    {
        if (OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_root);
        var oldMode = File.GetUnixFileMode(_root);
        using var lease = await CreateJournal().Oeffnen(CancellationToken.None);
        Assert.Equal(oldMode, File.GetUnixFileMode(_root));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Pending));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(LockPath));

        var privateRoot = Path.Combine(_root, "new-private");
        using var privateLease = await CreateJournal(privateRoot).Oeffnen(CancellationToken.None);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(privateRoot));
    }

    [Fact]
    public async Task RejectsSymlinkedJournalRootOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_root);
        var real = Path.Combine(_root, "real");
        Directory.CreateDirectory(real);
        var alias = Path.Combine(_root, "alias");
        Directory.CreateSymbolicLink(alias, real);
        await Assert.ThrowsAsync<IOException>(() => CreateJournal(alias).Oeffnen(CancellationToken.None));
        Assert.Empty(Directory.GetFileSystemEntries(real));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsSymlinkedPendingOrLockWithoutChangingTarget(bool pendingLink)
    {
        if (OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_root);
        var target = Path.Combine(_root, pendingLink ? "pending-target" : "lock-target");
        var targetContent = pendingLink ? Guid.NewGuid().ToString("N") : string.Empty;
        File.WriteAllText(target, targetContent);
        var before = File.ReadAllBytes(target);
        var link = pendingLink ? Pending : LockPath;
        File.CreateSymbolicLink(link, target);

        await Assert.ThrowsAsync<IOException>(() => CreateJournal().Oeffnen(CancellationToken.None));
        Assert.True(File.Exists(link));
        Assert.Equal(before, File.ReadAllBytes(target));
    }

    private DateisystemSicherungsjournal CreateJournal(string? root = null) =>
        new(root ?? _root, Backend);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
