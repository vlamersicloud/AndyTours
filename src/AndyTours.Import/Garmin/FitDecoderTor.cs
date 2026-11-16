using System.Threading;

namespace AndyTours.Import.Garmin;

internal static class FitDecoderTor
{
    private static readonly SemaphoreSlim Tor = new(1, 1);

    internal static IDisposable Betreten(CancellationToken cancellationToken)
    {
        Tor.Wait(cancellationToken);
        return new Freigabe();
    }

    private sealed class Freigabe : IDisposable
    {
        private int _freigegeben;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _freigegeben, 1) == 0) Tor.Release();
        }
    }
}
