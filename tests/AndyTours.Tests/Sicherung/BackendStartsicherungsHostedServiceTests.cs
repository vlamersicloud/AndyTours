namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using AndyTours.Application.Koordination;
using AndyTours.Application.Sicherung;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class BackendStartsicherungsHostedServiceTests
{
    [Fact]
    public async Task LifecycleStartingAwaitsCoordinatorWithHostTokenBeforeStartPhase()
    {
        var counter = new Counter();
        var coordinator = new BackendStartsicherungsKoordinator(new Coordination(), counter, new Archive());
        var service = new BackendStartsicherungsHostedService(coordinator, NullLogger<BackendStartsicherungsHostedService>.Instance);

        await service.StartingAsync(CancellationToken.None);
        Assert.Equal(1, counter.Calls);
        await service.StartAsync(CancellationToken.None);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task LifecycleStartingRemainsPendingUntilCoordinatorCompletes()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = new GatedCounter(started, release);
        var service = new BackendStartsicherungsHostedService(
            new BackendStartsicherungsKoordinator(new Coordination(), counter, new Archive()),
            NullLogger<BackendStartsicherungsHostedService>.Instance);

        var starting = service.StartingAsync(CancellationToken.None);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(starting.IsCompleted);
            release.TrySetResult(true);
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, counter.Calls);
        }
        finally
        {
            release.TrySetResult(true);
            try { await starting.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { }
        }
    }

    [Fact]
    public async Task LifecycleStartingPropagatesHostCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var counter = new Counter();
        var service = new BackendStartsicherungsHostedService(
            new BackendStartsicherungsKoordinator(new Coordination(), counter, new Archive()),
            NullLogger<BackendStartsicherungsHostedService>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StartingAsync(cancellation.Token));
        Assert.Equal(0, counter.Calls);
    }

    private sealed class Coordination : IDatenbestandskoordination
    {
        public ValueTask<IDisposable> Betreten(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected non-exclusive lease.");
        public ValueTask<IDisposable> ExklusivBetreten(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IDisposable>(new Lease());
        }
        private sealed class Lease : IDisposable { public void Dispose() { } }
    }

    private sealed class Counter : IBackendStartsicherungsZaehler
    {
        public int Calls { get; private set; }
        public Task<BackendStartsicherungsStand> ZaehlenUndErhoehen(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new BackendStartsicherungsStand(3, 1, 1));
        }
        public Task<bool> Zuruecksetzen(int erwarteterStand, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not due; reset must not run.");
    }

    private sealed class GatedCounter(
        TaskCompletionSource<bool> started,
        TaskCompletionSource<bool> release) : IBackendStartsicherungsZaehler
    {
        public int Calls { get; private set; }
        public async Task<BackendStartsicherungsStand> ZaehlenUndErhoehen(CancellationToken cancellationToken)
        {
            Calls++;
            started.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return new BackendStartsicherungsStand(3, 1, 1);
        }
        public Task<bool> Zuruecksetzen(int erwarteterStand, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not due; reset must not run.");
    }

    private sealed class Archive : IBackendStartsicherungsArchiv
    {
        public ISicherungsarchivErstellung? Ermittle(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not due; archive must not be resolved.");
    }
}
