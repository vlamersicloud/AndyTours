namespace AndyTours.Tests.Sicherung;

using AndyTours.App.ViewModels;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using AndyTours.Client.Sicherung;
using Xunit;

public sealed class ApiSicherungsarchivViewModelTests
{
    private static readonly Guid OperationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid[] ExpectedRetryIds = [OperationId, OperationId];
    private static readonly SicherungsarchivErstellungsantwort Success = new(
        new SicherungAntwort(OperationId, Sicherungsart.Vollarchiv,
            new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), 123,
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"), false);

    [Fact]
    public async Task PersistsBeforePostAndConfirmsOnlyAfterValidResponse()
    {
        var journal = new FakeJournal(OperationId);
        var creator = new FakeCreator((request, _) =>
        {
            Assert.Equal(OperationId, request.VorgangsId);
            Assert.Equal(1, journal.OpenCount);
            Assert.Equal(0, journal.Leases[0].ConfirmCount);
            return Task.FromResult(Success);
        });
        using var vm = new ApiSicherungsarchivViewModel(creator, journal);
        await vm.ErstellenAsync();
        Assert.Equal(1, creator.CallCount);
        Assert.Equal(1, journal.Leases[0].ConfirmCount);
        Assert.Equal(OperationId, journal.Leases[0].ConfirmedId);
        Assert.False(journal.Leases[0].ConfirmToken.CanBeCanceled);
        Assert.Null(vm.VorgangsId);
        Assert.Equal(Success.Sicherung, vm.Ergebnis);
        Assert.False(vm.IstBeschaeftigt);
        Assert.True(journal.Leases[0].Disposed);
        Assert.True(creator.LastToken.CanBeCanceled);
    }

    [Fact]
    public async Task BusyCommandDoesNotStartASecondOperation()
    {
        var journal = new FakeJournal(OperationId);
        var pending = new TaskCompletionSource<SicherungsarchivErstellungsantwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var creator = new FakeCreator((_, _) => pending.Task);
        using var vm = new ApiSicherungsarchivViewModel(creator, journal);
        var run = vm.ErstellenAsync();
        try
        {
            await creator.Started.Task;
            Assert.True(vm.IstBeschaeftigt);
            await vm.ErstellenAsync();
            Assert.Equal(1, journal.OpenCount);
            Assert.Equal(1, creator.CallCount);
        }
        finally
        {
            pending.TrySetResult(Success);
            await run;
        }
        Assert.Equal(1, journal.Leases[0].ConfirmCount);
    }

    [Fact]
    public async Task PreCancelledOperationDoesNotOpenJournalOrPost()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var journal = new FakeJournal(OperationId);
        var creator = new FakeCreator((_, _) => Task.FromResult(Success));
        using var vm = new ApiSicherungsarchivViewModel(creator, journal);
        await vm.ErstellenAsync(cts.Token);
        Assert.Equal(0, journal.OpenCount);
        Assert.Equal(0, creator.CallCount);
    }

    [Fact]
    public async Task RetryUsesSamePersistentIdAfterTransportFailureAnd503()
    {
        var journal = new FakeJournal(OperationId);
        var ids = new List<Guid>();
        var creator = new FakeCreator((request, _) =>
        {
            ids.Add(request.VorgangsId);
            if (ids.Count == 1) throw new HttpRequestException("network");
            throw new HttpRequestException("unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
        });
        using var vm = new ApiSicherungsarchivViewModel(creator, journal);
        await vm.ErstellenAsync();
        Assert.Equal(OperationId, vm.VorgangsId);
        Assert.Equal(0, journal.Leases[0].ConfirmCount);
        await vm.ErstellenAsync();
        Assert.Equal(ExpectedRetryIds, ids);
        Assert.Equal(OperationId, vm.VorgangsId);
        Assert.Equal(0, journal.Leases[1].ConfirmCount);
    }

    [Fact]
    public async Task InvalidMetadataAndConflictRetainPendingIdWithoutConfirmation()
    {
        foreach (var response in new SicherungsarchivErstellungsantwort?[]
        {
            Success with { Sicherung = Success.Sicherung with { Id = Guid.NewGuid() } },
            Success with { Sicherung = Success.Sicherung with { InhaltHash = "bad" } },
            null
        })
        {
            var journal = new FakeJournal(OperationId);
            using var vm = new ApiSicherungsarchivViewModel(
                new FakeCreator((_, _) => Task.FromResult(response!)), journal);
            await vm.ErstellenAsync();
            Assert.Equal(OperationId, vm.VorgangsId);
            Assert.Equal(0, journal.Leases[0].ConfirmCount);
        }

        var conflictJournal = new FakeJournal(OperationId);
        using var conflictVm = new ApiSicherungsarchivViewModel(
            new FakeCreator((_, _) => throw new AndyTours.Application.Stammdaten.StammdatenException(
                AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt, "conflict")), conflictJournal);
        await conflictVm.ErstellenAsync();
        Assert.Equal(OperationId, conflictVm.VorgangsId);
        Assert.Equal(0, conflictJournal.Leases[0].ConfirmCount);
    }

    [Fact]
    public async Task ConfirmationFailureThenRetryUsesSameIdAndFreshLease()
    {
        var journal = new FakeJournal(OperationId) { ConfirmFailure = new IOException("private path") };
        var ids = new List<Guid>();
        var creator = new FakeCreator((request, _) =>
        {
            ids.Add(request.VorgangsId);
            return Task.FromResult(Success);
        });
        using var vm = new ApiSicherungsarchivViewModel(creator, journal);
        await vm.ErstellenAsync();
        Assert.Equal(Success.Sicherung, vm.Ergebnis);
        Assert.Equal(OperationId, vm.VorgangsId);
        Assert.Equal(1, journal.Leases[0].ConfirmCount);
        Assert.DoesNotContain("private path", vm.Status, StringComparison.Ordinal);

        journal.ConfirmFailure = null;
        await vm.ErstellenAsync();
        Assert.Equal(ExpectedRetryIds, ids);
        Assert.Equal(2, journal.Leases.Count);
        Assert.NotSame(journal.Leases[0], journal.Leases[1]);
        Assert.True(journal.Leases[0].Disposed);
        Assert.True(journal.Leases[1].Disposed);
        Assert.Equal(1, journal.Leases[1].ConfirmCount);
        Assert.Null(vm.VorgangsId);
    }

    [Fact]
    public async Task DisposalDuringRequestStillConfirmsValidatedSuccessWithoutUiUpdates()
    {
        var journal = new FakeJournal(OperationId);
        var pending = new TaskCompletionSource<SicherungsarchivErstellungsantwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var creator = new FakeCreator((_, _) => pending.Task);
        var vm = new ApiSicherungsarchivViewModel(creator, journal);
        var changedAfterDispose = false;
        var disposed = false;
        vm.PropertyChanged += (_, _) => { if (disposed) changedAfterDispose = true; };
        var run = vm.ErstellenAsync();
        try
        {
            await creator.Started.Task;
            vm.Dispose();
            disposed = true;
        }
        finally
        {
            pending.TrySetResult(Success);
            await run;
            vm.Dispose();
        }
        Assert.Equal(1, journal.Leases[0].ConfirmCount);
        Assert.True(journal.Leases[0].Disposed);
        Assert.False(changedAfterDispose);
    }

    [Fact]
    public async Task DisposalWhileJournalOpenWaitsForLeaseThenDisposesWithoutPost()
    {
        var journal = new FakeJournal(OperationId) { DeferOpen = true };
        var creator = new FakeCreator((_, _) => Task.FromResult(Success));
        var vm = new ApiSicherungsarchivViewModel(creator, journal);
        var changedAfterDispose = false;
        var disposed = false;
        vm.PropertyChanged += (_, _) => { if (disposed) changedAfterDispose = true; };
        var run = vm.ErstellenAsync();
        try
        {
            await journal.OpenStarted.Task;
            vm.Dispose();
            disposed = true;
        }
        finally
        {
            journal.CompleteOpen();
            await run;
            vm.Dispose();
        }
        Assert.Equal(0, creator.CallCount);
        Assert.Equal(1, journal.OpenCount);
        Assert.True(journal.Leases[0].Disposed);
        Assert.False(changedAfterDispose);
    }

    [Fact]
    public async Task EmptyJournalIdAndOpenFailureNeverCallCreator()
    {
        foreach (var failOpen in new[] { false, true })
        {
            var journal = new FakeJournal(failOpen ? OperationId : Guid.Empty)
            {
                OpenFailure = failOpen ? new IOException("private path") : null
            };
            var creator = new FakeCreator((_, _) => Task.FromResult(Success));
            using var vm = new ApiSicherungsarchivViewModel(creator, journal);
            await vm.ErstellenAsync();
            Assert.Equal(0, creator.CallCount);
            Assert.Equal(0, journal.Leases.Sum(lease => lease.ConfirmCount));
            Assert.DoesNotContain("private path", vm.Status, StringComparison.Ordinal);
        }
    }

    private sealed class FakeCreator(Func<SicherungsarchivErstellungsanfrage, CancellationToken, Task<SicherungsarchivErstellungsantwort>> run)
        : ISicherungsarchivErstellung
    {
        public int CallCount { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SicherungsarchivErstellungsantwort> Erstellen(SicherungsarchivErstellungsanfrage anfrage, CancellationToken cancellationToken)
        {
            CallCount++;
            LastToken = cancellationToken;
            Started.TrySetResult();
            return run(anfrage, cancellationToken);
        }
    }

    private sealed class FakeJournal(Guid id) : ISicherungsjournal
    {
        private readonly TaskCompletionSource<FakeLease> _openResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Guid? _pendingId = id;

        public int OpenCount { get; private set; }
        public Exception? OpenFailure { get; init; }
        public Exception? ConfirmFailure { get; set; }
        public bool DeferOpen { get; init; }
        public TaskCompletionSource OpenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<FakeLease> Leases { get; } = [];

        public Task<ISicherungsjournalSperre> Oeffnen(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            OpenStarted.TrySetResult();
            if (OpenFailure is { } failure) return Task.FromException<ISicherungsjournalSperre>(failure);
            if (DeferOpen) return AwaitOpen();
            return Task.FromResult<ISicherungsjournalSperre>(CreateLease());
        }

        private async Task<ISicherungsjournalSperre> AwaitOpen() =>
            await _openResult.Task.ConfigureAwait(false);

        public void CompleteOpen() => _openResult.TrySetResult(CreateLease());

        private FakeLease CreateLease()
        {
            _pendingId ??= Guid.NewGuid();
            var lease = new FakeLease(this, _pendingId.Value);
            Leases.Add(lease);
            return lease;
        }

        public Task CompleteConfirm(Guid id)
        {
            if (_pendingId != id)
                return Task.FromException(new IOException("operation id mismatch"));
            if (ConfirmFailure is { } failure) return Task.FromException(failure);
            _pendingId = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLease(FakeJournal owner, Guid id) : ISicherungsjournalSperre
    {
        public int ConfirmCount { get; private set; }
        public Guid? ConfirmedId { get; private set; }
        public CancellationToken ConfirmToken { get; private set; }
        public bool Disposed { get; private set; }

        public Task Bestaetigen(Guid vorgangsId, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            ConfirmCount++;
            ConfirmedId = vorgangsId;
            ConfirmToken = cancellationToken;
            return owner.CompleteConfirm(vorgangsId);
        }

        public Guid VorgangsId
        {
            get
            {
                ObjectDisposedException.ThrowIf(Disposed, this);
                return id;
            }
        }

        public void Dispose() => Disposed = true;
    }
}
