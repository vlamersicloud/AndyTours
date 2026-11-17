namespace AndyTours.Api;

using System.Threading.RateLimiting;
using AndyTours.Application.Koordination;

public sealed class Datenbestandskoordination : IDatenbestandskoordination, IDisposable
{
    private readonly int _kapazitaet;
    private readonly ConcurrencyLimiter _limiter;
    private bool _disposed;

    public Datenbestandskoordination(int kapazitaet, int warteschlangenplaetze)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kapazitaet);
        ArgumentOutOfRangeException.ThrowIfLessThan(warteschlangenplaetze, kapazitaet);
        _kapazitaet = kapazitaet;
        _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = kapazitaet,
            QueueLimit = warteschlangenplaetze,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    public ValueTask<IDisposable> Betreten(CancellationToken cancellationToken) =>
        ErwerbeAsync(1, cancellationToken);

    public ValueTask<IDisposable> ExklusivBetreten(CancellationToken cancellationToken) =>
        ErwerbeAsync(_kapazitaet, cancellationToken);

    private async ValueTask<IDisposable> ErwerbeAsync(int permits, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var lease = await _limiter.AcquireAsync(permits, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            lease.Dispose();
            throw new DatenbestandBelegtException();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new Lease(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _limiter.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class Lease(RateLimitLease lease) : IDisposable
    {
        private RateLimitLease? _lease = lease;
        public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();
    }
}
