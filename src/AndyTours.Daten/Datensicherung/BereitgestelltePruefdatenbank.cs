namespace AndyTours.Daten.Datensicherung;

using AndyTours.Contracts.V1;

/// <summary>
/// Owns only a private temporary inspection workspace after successful coordinator transfer.
/// This data-only server result is not an HTTP DTO, SQLite check, restore proof, or active-database owner.
/// </summary>
public sealed class BereitgestelltePruefdatenbank : IDisposable
{
    private readonly WiederherstellungsPruefarbeitsbereich _arbeitsbereich;
    private readonly string _datenbankPfad;
    private readonly SicherungAntwort _quelle;
    private readonly Sicherungsmanifest _manifest;
    private bool _disposed;

    internal BereitgestelltePruefdatenbank(
        WiederherstellungsPruefarbeitsbereich arbeitsbereich,
        SicherungAntwort quelle,
        Sicherungsmanifest manifest)
    {
        ArgumentNullException.ThrowIfNull(arbeitsbereich);
        ArgumentNullException.ThrowIfNull(quelle);
        ArgumentNullException.ThrowIfNull(manifest);
        var databasePath = arbeitsbereich.DatenbankPfad;
        _arbeitsbereich = arbeitsbereich;
        _datenbankPfad = databasePath;
        _quelle = quelle;
        _manifest = manifest;
    }

    public string DatenbankPfad
    {
        get
        {
            ThrowIfDisposed();
            return _datenbankPfad;
        }
    }

    public SicherungAntwort Quelle
    {
        get
        {
            ThrowIfDisposed();
            return _quelle;
        }
    }

    public Sicherungsmanifest Manifest
    {
        get
        {
            ThrowIfDisposed();
            return _manifest;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _arbeitsbereich.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
