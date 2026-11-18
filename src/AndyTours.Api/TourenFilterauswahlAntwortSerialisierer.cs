using System.IO;
using System.Text.Json;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

/// <summary>Serialisiert eine vollständige Antwort ausschließlich innerhalb des konfigurierten Bytebudgets.</summary>
internal static class TourenFilterauswahlAntwortSerialisierer
{
    internal static bool TrySerialisieren(
        TourenFilterauswahlAntwort antwort, JsonSerializerOptions optionen, int maxAntwortBytes,
        out byte[] payload, CancellationToken ct = default)
    {
        payload = [];
        if (antwort is null || optionen is null || maxAntwortBytes <= 0)
            return false;

        ct.ThrowIfCancellationRequested();
        using var body = new BegrenzterStream(maxAntwortBytes, callerCancellation: ct);
        try
        {
            JsonSerializer.Serialize(body, antwort, optionen);
            ct.ThrowIfCancellationRequested();
            payload = body.ToArray();
            return true;
        }
        catch (BudgetUeberschrittenException)
        {
            return false;
        }
    }

    private sealed class BudgetUeberschrittenException : Exception { }

    private sealed class BegrenzterStream(int maximum, CancellationToken callerCancellation) : Stream
    {
        private readonly MemoryStream _buffer = new();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.Length;
        public override long Position { get => _buffer.Position; set => throw new NotSupportedException(); }
        public byte[] ToArray() => _buffer.ToArray();
        public override void Flush()
        {
            callerCancellation.ThrowIfCancellationRequested();
            _buffer.Flush();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            callerCancellation.ThrowIfCancellationRequested();
            Pruefe(count);
            _buffer.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            callerCancellation.ThrowIfCancellationRequested();
            Pruefe(buffer.Length);
            _buffer.Write(buffer);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            callerCancellation.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
            Pruefe(buffer.Length);
            return _buffer.WriteAsync(buffer, cancellationToken);
        }
        private void Pruefe(int count)
        {
            callerCancellation.ThrowIfCancellationRequested();
            if (count < 0 || count > maximum - _buffer.Length)
                throw new BudgetUeberschrittenException();
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _buffer.Dispose();
            base.Dispose(disposing);
        }
    }
}
