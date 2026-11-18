namespace AndyTours.Application.Sicherung;

using AndyTours.Contracts.V1;

/// <summary>Opens only a previously catalogued backup after validating its opaque ID and expected full-file hash.</summary>
public interface ISicherungsdownload
{
    Task<SicherungsdownloadQuelle> Oeffnen(SicherungsdownloadAnfrage anfrage, long serverMaxBytes, CancellationToken cancellationToken);
}

/// <summary>Owns a verified source stream until the HTTP response has completed or aborted.</summary>
public sealed class SicherungsdownloadQuelle : IAsyncDisposable
{
    private readonly Func<ValueTask> _dispose;
    private int _disposed;
    public SicherungsdownloadQuelle(Stream stream, SicherungAntwort metadaten, Func<ValueTask> dispose)
    {
        Inhalt = stream ?? throw new ArgumentNullException(nameof(stream));
        Metadaten = metadaten ?? throw new ArgumentNullException(nameof(metadaten));
        _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
    }
    public Stream Inhalt { get; }
    public SicherungAntwort Metadaten { get; }
    public ValueTask DisposeAsync() => Interlocked.Exchange(ref _disposed, 1) == 0 ? _dispose() : ValueTask.CompletedTask;
}
