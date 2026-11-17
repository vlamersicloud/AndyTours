namespace AndyTours.Daten.Datensicherung;

/// <summary>Caps a caller-owned seekable stream, including ZIP-style rewrites and truncation. The wrapper never owns the destination; callers must prevent concurrent destination mutation.</summary>
public sealed class BegrenzterSicherungsschreibStream : Stream
{
    private const string Fehlertext = "Sicherungsarchiv überschreitet die konfigurierte Größe.";
    private readonly Stream _ziel;
    private readonly long _maxBytes;
    private bool _disposed;

    public BegrenzterSicherungsschreibStream(Stream ziel, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        if (!ziel.CanWrite || !ziel.CanSeek || maxBytes <= 0 || ziel.Length < 0 || ziel.Length > maxBytes)
            throw Invalid();
        _ziel = ziel;
        _maxBytes = maxBytes;
    }

    public override bool CanRead => false;
    public override bool CanSeek => !_disposed && _ziel.CanSeek;
    public override bool CanWrite => !_disposed && _ziel.CanWrite;
    public override long Length { get { ThrowIfDisposed(); return _ziel.Length; } }
    public override long Position
    {
        get { ThrowIfDisposed(); return _ziel.Position; }
        set { ThrowIfDisposed(); if (value < 0 || value > _maxBytes) throw Invalid(); _ziel.Position = value; }
    }

    public override void Flush() { ThrowIfDisposed(); _ziel.Flush(); }
    public override Task FlushAsync(CancellationToken cancellationToken)
    { ThrowIfDisposed(); return _ziel.FlushAsync(cancellationToken); }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        long target;
        try
        {
            target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_ziel.Position + offset),
                SeekOrigin.End => checked(_ziel.Length + offset),
                _ => throw Invalid()
            };
        }
        catch (OverflowException) { throw Invalid(); }
        if (target < 0 || target > _maxBytes) throw Invalid();
        _ziel.Position = target;
        return target;
    }

    public override void SetLength(long value)
    {
        ThrowIfDisposed();
        if (value < 0 || value > _maxBytes) throw Invalid();
        _ziel.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Invalid buffer range.");
        EnsureWriteWithinLimit(count);
        _ziel.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed(); EnsureWriteWithinLimit(buffer.Length); _ziel.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        ThrowIfDisposed(); EnsureWriteWithinLimit(1); _ziel.WriteByte(value);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed(); EnsureWriteWithinLimit(buffer.Length);
        return _ziel.WriteAsync(buffer, cancellationToken);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Invalid buffer range.");
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override int Read(byte[] buffer, int offset, int count) { ThrowIfDisposed(); throw new NotSupportedException(); }
    public override int Read(Span<byte> buffer) { ThrowIfDisposed(); throw new NotSupportedException(); }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    { ThrowIfDisposed(); throw new NotSupportedException(); }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    { ThrowIfDisposed(); throw new NotSupportedException(); }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed) _disposed = true;
        base.Dispose(disposing);
    }

    private void EnsureWriteWithinLimit(int count)
    {
        if (count < 0) throw Invalid();
        long end;
        try { end = checked(_ziel.Position + count); }
        catch (OverflowException) { throw Invalid(); }
        if (end > _maxBytes) throw Invalid();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private static IOException Invalid() => new(Fehlertext);
}
