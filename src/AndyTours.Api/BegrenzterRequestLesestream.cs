using Microsoft.AspNetCore.Http;

namespace AndyTours.Api;

/// <summary>Byte-counting request stream that never owns or disposes its source.</summary>
internal sealed class BegrenzterRequestLesestream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _read;

    public BegrenzterRequestLesestream(Stream inner, long maxBytes)
    {
        if (inner is null || !inner.CanRead) throw new ArgumentException("Lesbarer Eingabestream erforderlich.", nameof(inner));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        _inner = inner;
        _maxBytes = maxBytes;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => Check(_inner.Read(buffer, offset, count));
    public override int Read(Span<byte> buffer) => Check(_inner.Read(buffer));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Check(await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Check(await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false));
    private int Check(int count)
    {
        _read += count;
        if (_read > _maxBytes) throw new BadHttpRequestException("Anfrage zu groß.", StatusCodes.Status413PayloadTooLarge);
        return count;
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { base.Dispose(disposing); }
    public override ValueTask DisposeAsync() => base.DisposeAsync();
}