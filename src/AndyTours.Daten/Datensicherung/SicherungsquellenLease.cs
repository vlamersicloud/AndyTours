namespace AndyTours.Daten.Datensicherung;

using AndyTours.Contracts.V1;

/// <summary>
/// Owns a verified read-only backup stream and its metadata. The caller must hold the required outer
/// dataset lease and independently exclude external writers. This is not SQLite or restore validation.
/// </summary>
public sealed class SicherungsquellenLease : IDisposable, IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly SicherungAntwort _metadaten;
    private bool _disposed;

    internal SicherungsquellenLease(FileStream ownedStream, SicherungAntwort metadaten)
    {
        ArgumentNullException.ThrowIfNull(ownedStream);
        ArgumentNullException.ThrowIfNull(metadaten);
        _stream = ownedStream;
        _metadaten = metadaten;
    }

    public Stream Inhalt
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _stream;
        }
    }

    public SicherungAntwort Metadaten
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _metadaten;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _stream.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
