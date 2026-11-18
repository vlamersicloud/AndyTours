namespace AndyTours.Tests.Sicherung;

using AndyTours.Application.Koordination;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class BackendStartsicherungsKoordinatorTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(2, 3, 4)]
    [InlineData(3, 3, 0)]
    public async Task NotDueOrEmptyCountsOnceAndNeverCreatesArchive(int starts, int interval, int units)
    {
        var counter = new FakeCounter(new(interval, starts, units));
        var archive = new FakeArchive(new FakeWriter());
        var coordination = new FakeCoordination();
        var result = await Coordinator(coordination, counter, archive).Ausfuehren(CancellationToken.None);

        Assert.Equal(BackendStartsicherungsStatus.NichtFaellig, result.Status);
        Assert.Equal(1, coordination.Acquisitions);
        Assert.Equal(1, coordination.Releases);
        Assert.Equal(1, counter.Increments);
        Assert.Equal(0, counter.Resets);
        Assert.Equal(0, archive.Resolutions);
        Assert.NotNull(archive.Writer);
        Assert.Equal(0, archive.Writer!.Calls);
    }

    [Fact]
    public async Task MissingArchiveConfigurationDoesNotResetDueCounter()
    {
        var counter = new FakeCounter(new(1, 1, 2));
        var archive = new FakeArchive(null);
        var result = await Coordinator(new FakeCoordination(), counter, archive).Ausfuehren(CancellationToken.None);

        Assert.Equal(BackendStartsicherungsStatus.NichtVerfuegbar, result.Status);
        Assert.Equal(1, counter.Increments);
        Assert.Equal(0, counter.Resets);
        Assert.Equal(1, archive.Resolutions);
    }

    [Fact]
    public async Task ArchiveFailureKeepsIncrementedCounterAndDoesNotRetry()
    {
        var counter = new FakeCounter(new(1, 3, 1));
        var writer = new FakeWriter(_ => throw new InvalidOperationException("private path"));
        var archive = new FakeArchive(writer);
        var result = await Coordinator(new FakeCoordination(), counter, archive).Ausfuehren(CancellationToken.None);

        Assert.Equal(BackendStartsicherungsStatus.Fehlgeschlagen, result.Status);
        Assert.Equal(1, archive.Resolutions);
        Assert.Equal(1, writer.Calls);
        Assert.Equal(0, counter.Resets);
        Assert.Equal(1, counter.Increments);
        Assert.DoesNotContain("private path", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetsOnlyAfterValidatedSuccessfulArchiveAndUsesOneFreshOperationId()
    {
        var counter = new FakeCounter(new(1, int.MaxValue, 2));
        SicherungsarchivErstellungsanfrage? observed = null;
        var writer = new FakeWriter(request =>
        {
            observed = request;
            return new SicherungsarchivErstellungsantwort(
                new SicherungAntwort(request.VorgangsId, Sicherungsart.Vollarchiv,
                    DateTimeOffset.UtcNow, 12, Hash), false);
        });
        var coordination = new FakeCoordination();
        var result = await Coordinator(coordination, counter, new FakeArchive(writer))
            .Ausfuehren(CancellationToken.None);

        Assert.Equal(BackendStartsicherungsStatus.Gesichert, result.Status);
        Assert.NotNull(observed);
        Assert.NotEqual(Guid.Empty, observed!.VorgangsId);
        Assert.Equal(observed, writer.LastRequest);
        Assert.Equal(int.MaxValue, counter.ResetExpected);
        Assert.Equal(1, counter.Resets);
        Assert.Equal(1, writer.Calls);
        Assert.Equal(1, coordination.Acquisitions);
        Assert.Equal(1, coordination.Releases);
    }

    [Fact]
    public async Task FailedCounterCompareAndSwapNeverRetriesOrClaimsSuccess()
    {
        var counter = new FakeCounter(new(1, 1, 1)) { ResetResult = false };
        var writer = new FakeWriter(request => new SicherungsarchivErstellungsantwort(
            new SicherungAntwort(request.VorgangsId, Sicherungsart.Vollarchiv,
                DateTimeOffset.UtcNow, 12, Hash), false));
        var result = await Coordinator(new FakeCoordination(), counter, new FakeArchive(writer))
            .Ausfuehren(CancellationToken.None);

        Assert.Equal(BackendStartsicherungsStatus.Fehlgeschlagen, result.Status);
        Assert.Equal(1, counter.Resets);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task InvalidArchiveShapesDoNotResetCounter()
    {
        var invalidShapes = new Func<Guid, SicherungAntwort>[]
        {
            id => new SicherungAntwort(id, Sicherungsart.Vollarchiv, null, 12, Hash),
            id => new SicherungAntwort(id, Sicherungsart.Vollarchiv,
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)), 12, Hash),
            id => new SicherungAntwort(id, Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 12, Hash)
                { LegacyZeitstempel = "2026-01-01_12-00-00" },
            id => new SicherungAntwort(id, Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 12, "not-a-hash"),
            id => new SicherungAntwort(id, Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 0, Hash)
        };

        foreach (var shape in invalidShapes)
        {
            var counter = new FakeCounter(new(1, 1, 1));
            var writer = new FakeWriter(request => new SicherungsarchivErstellungsantwort(shape(request.VorgangsId), false));
            var result = await Coordinator(new FakeCoordination(), counter, new FakeArchive(writer))
                .Ausfuehren(CancellationToken.None);
            Assert.Equal(BackendStartsicherungsStatus.Fehlgeschlagen, result.Status);
            Assert.Equal(0, counter.Resets);
            Assert.Equal(1, writer.Calls);
        }

        var alreadyPublishedCounter = new FakeCounter(new(1, 1, 1));
        var alreadyPublished = new FakeWriter(request => new SicherungsarchivErstellungsantwort(
            new SicherungAntwort(request.VorgangsId, Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 12, Hash), true));
        var alreadyResult = await Coordinator(new FakeCoordination(), alreadyPublishedCounter, new FakeArchive(alreadyPublished))
            .Ausfuehren(CancellationToken.None);
        Assert.Equal(BackendStartsicherungsStatus.Fehlgeschlagen, alreadyResult.Status);
        Assert.Equal(0, alreadyPublishedCounter.Resets);
        Assert.Equal(1, alreadyPublished.Calls);

        var mismatchedCounter = new FakeCounter(new(1, 1, 1));
        var mismatched = new FakeWriter(_ => new SicherungsarchivErstellungsantwort(
            new SicherungAntwort(Guid.NewGuid(), Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 12, Hash), false));
        var mismatchedResult = await Coordinator(new FakeCoordination(), mismatchedCounter, new FakeArchive(mismatched))
            .Ausfuehren(CancellationToken.None);
        Assert.Equal(BackendStartsicherungsStatus.Fehlgeschlagen, mismatchedResult.Status);
        Assert.Equal(0, mismatchedCounter.Resets);
        Assert.Equal(1, mismatched.Calls);
    }

    [Fact]
    public async Task HostCancellationAfterWriterIgnoresTokenNeverResetsCounter()
    {
        using var cancellation = new CancellationTokenSource();
        var counter = new FakeCounter(new(1, 1, 1));
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new FakeWriter(async (request, _) =>
        {
            await release.Task;
            return new SicherungsarchivErstellungsantwort(
                new SicherungAntwort(request.VorgangsId, Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 12, Hash), false);
        });
        var pending = Coordinator(new FakeCoordination(), counter, new FakeArchive(writer))
            .Ausfuehren(cancellation.Token);
        try
        {
            await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            release.TrySetResult(true);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(0, counter.Resets);
        }
        finally
        {
            cancellation.Cancel();
            release.TrySetResult(true);
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { }
        }
    }

    [Fact]
    public async Task PreCancelledHostDoesNotAcquireLeaseOrTouchPorts()
    {
        var counter = new FakeCounter(new(1, 1, 1));
        var archive = new FakeArchive(new FakeWriter());
        var coordination = new FakeCoordination();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Coordinator(coordination, counter, archive).Ausfuehren(cancellation.Token));

        Assert.Equal(0, coordination.Acquisitions);
        Assert.Equal(0, counter.Increments);
        Assert.Equal(0, archive.Resolutions);
    }

    [Fact]
    public async Task HostCancellationDuringArchivePropagatesAndReleasesExclusiveLease()
    {
        using var cancellation = new CancellationTokenSource();
        var counter = new FakeCounter(new(1, 1, 1));
        var writer = new FakeWriter(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });
        var coordination = new FakeCoordination();
        var pending = Coordinator(coordination, counter, new FakeArchive(writer)).Ausfuehren(cancellation.Token);
        try
        {
            await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(0, counter.Resets);
            Assert.Equal(1, coordination.Releases);
        }
        finally
        {
            cancellation.Cancel();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { }
        }
    }

    private static BackendStartsicherungsKoordinator Coordinator(
        FakeCoordination coordination, FakeCounter counter, FakeArchive archive) =>
        new(coordination, counter, archive);

    private sealed class FakeCoordination : IDatenbestandskoordination
    {
        public int Acquisitions { get; private set; }
        public int Releases { get; private set; }
        public ValueTask<IDisposable> Betreten(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ordinary lease must not be nested.");
        public ValueTask<IDisposable> ExklusivBetreten(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Acquisitions++;
            return ValueTask.FromResult<IDisposable>(new Lease(() => Releases++));
        }
        private sealed class Lease(Action release) : IDisposable
        {
            private Action? _release = release;
            public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
        }
    }

    private sealed class FakeCounter(BackendStartsicherungsStand state) : IBackendStartsicherungsZaehler
    {
        public int Increments { get; private set; }
        public int Resets { get; private set; }
        public int ResetExpected { get; private set; } = -1;
        public bool ResetResult { get; set; } = true;
        public Task<BackendStartsicherungsStand> ZaehlenUndErhoehen(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Increments++;
            return Task.FromResult(state);
        }
        public Task<bool> Zuruecksetzen(int erwarteterStand, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Resets++;
            ResetExpected = erwarteterStand;
            return Task.FromResult(ResetResult);
        }
    }

    private sealed class FakeArchive(FakeWriter? writer) : IBackendStartsicherungsArchiv
    {
        public int Resolutions { get; private set; }
        public FakeWriter? Writer => writer;
        public ISicherungsarchivErstellung? Ermittle(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Resolutions++;
            return writer;
        }
    }

    private sealed class FakeWriter(
        Func<SicherungsarchivErstellungsanfrage, SicherungsarchivErstellungsantwort>? reply = null,
        Func<SicherungsarchivErstellungsanfrage, CancellationToken, Task<SicherungsarchivErstellungsantwort>>? asyncReply = null)
        : ISicherungsarchivErstellung
    {
        public int Calls { get; private set; }
        public SicherungsarchivErstellungsanfrage? LastRequest { get; private set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SicherungsarchivErstellungsantwort> Erstellen(SicherungsarchivErstellungsanfrage anfrage, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = anfrage;
            Started.TrySetResult(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (asyncReply is not null) return asyncReply(anfrage, cancellationToken);
            return Task.FromResult(reply?.Invoke(anfrage) ?? new SicherungsarchivErstellungsantwort(
                new SicherungAntwort(anfrage.VorgangsId, Sicherungsart.Vollarchiv,
                    DateTimeOffset.UtcNow, 12, Hash), false));
        }
        public FakeWriter(Func<SicherungsarchivErstellungsanfrage, CancellationToken, Task<SicherungsarchivErstellungsantwort>> asyncReply)
            : this(null, asyncReply) { }
    }
}
