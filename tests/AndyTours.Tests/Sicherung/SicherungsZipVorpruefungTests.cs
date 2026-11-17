namespace AndyTours.Tests.Sicherung;

using System.Buffers.Binary;
using System.IO.Compression;
using AndyTours.Daten.Datensicherung;
using AndyTours.Application.Stammdaten;
using Xunit;

public sealed class SicherungsZipVorpruefungTests
{
    private const string Fehlertext = "Sicherungsarchiv ungültig.";

    [Fact]
    public async Task ValidZip_ReturnsStructuralFacts_AndLeavesStreamOpen()
    {
        using var stream = CreateZip();
        var result = await SicherungsZipVorpruefung.Pruefe(stream, 4, 1024 * 1024, 4096);
        Assert.Equal(1, result.Eintraege);
        Assert.Equal(stream.Length - 22, result.ZentralverzeichnisOffset + result.ZentralverzeichnisBytes);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
    }

    [Theory]
    [InlineData("signature")][InlineData("comment")][InlineData("disk")][InlineData("count")]
    [InlineData("trailing")][InlineData("central-size")]
    public async Task InvalidEocd_IsRejectedWithoutLeakingDetails(string mutation)
    {
        var bytes = GetZipBytes(); var eocd = bytes.Length - 22;
        switch (mutation)
        {
            case "signature": bytes[eocd] ^= 0x10; break;
            case "comment": U16(bytes, eocd + 20, 1); break;
            case "disk": U16(bytes, eocd + 4, 1); break;
            case "count": U16(bytes, eocd + 8, 2); break;
            case "trailing": Array.Resize(ref bytes, bytes.Length + 1); break;
            case "central-size": U32(bytes, eocd + 12, uint.MaxValue); break;
        }
        await AssertInvalid(new MemoryStream(bytes));
    }

    [Fact]
    public async Task LimitsAreEnforcedBeforeCentralScan()
    {
        var bytes = GetZipBytes(); var eocd = bytes.Length - 22;
        U16(bytes, eocd + 10, 500); U16(bytes, eocd + 8, 500);
        using var tracked = new TrackingStream(bytes);
        await AssertInvalid(tracked, maxEntries: 4);
        Assert.True(tracked.BytesRead <= 22);
        using var valid = new MemoryStream(GetZipBytes());
        await AssertInvalid(valid, maxCentralBytes: 45);
        using var tooLarge = new MemoryStream(GetZipBytes());
        await AssertInvalid(tooLarge, maxArchiveBytes: tooLarge.Length - 1);
    }

    [Theory]
    [InlineData("encrypted")][InlineData("method")][InlineData("variable-overrun")]
    public async Task InvalidCentralRecord_IsRejected(string mutation)
    {
        var bytes = GetZipBytes(); var cd = checked((int)U32(bytes, bytes.Length - 22 + 16));
        switch (mutation)
        {
            case "encrypted": U16(bytes, cd + 8, 1); break;
            case "method": U16(bytes, cd + 10, 99); break;
            case "variable-overrun": U16(bytes, cd + 28, ushort.MaxValue); break;
        }
        await AssertInvalid(new MemoryStream(bytes));
    }

    [Fact]
    public async Task Zip64Fixture_AcceptsValidRecords()
    {
        var bytes = GetZip64Bytes();
        using var valid = new MemoryStream(bytes);
        var result = await SicherungsZipVorpruefung.Pruefe(valid, 4, 1024 * 1024, 4096);
        Assert.Equal(1, result.Eintraege);
    }

    [Theory]
    [InlineData("record-size")][InlineData("locator-disk")][InlineData("locator-offset")]
    [InlineData("nonsentinel-count")][InlineData("central-offset")]
    public async Task InvalidZip64Fields_AreRejected(string mutation)
    {
        var bytes = GetZip64Bytes(); var eocd = bytes.Length - 22;
        var locator = eocd - 20; var record = locator - 56;
        switch (mutation)
        {
            case "record-size": U64(bytes, record + 4, 45); break;
            case "locator-disk": U32(bytes, locator + 4, 2); break;
            case "locator-offset": U64(bytes, locator + 8, ulong.MaxValue); break;
            case "nonsentinel-count": U16(bytes, eocd + 8, 2); U16(bytes, eocd + 10, 2); break;
            case "central-offset": U64(bytes, record + 48, ulong.MaxValue); break;
        }
        await AssertInvalid(new MemoryStream(bytes));
    }

    [Fact]
    public async Task CancellationAndNonSeekableInputAreSafe_AndFailuresLeaveOwnershipWithCaller()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var tracked = new TrackingStream(GetZipBytes());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SicherungsZipVorpruefung.Pruefe(tracked, 4, 1024 * 1024, 4096, cts.Token));
        Assert.Equal(0, tracked.BytesRead);
        var invalidBytes = GetZipBytes(); invalidBytes[invalidBytes.Length - 22] ^= 0x10;
        using var stream = new MemoryStream(invalidBytes);
        await Assert.ThrowsAsync<StammdatenException>(() => SicherungsZipVorpruefung.Pruefe(stream, 4, 1024 * 1024, 4096));
        Assert.True(stream.CanRead);
        using var nonSeekable = new NonSeekableStream(GetZipBytes());
        await AssertInvalid(nonSeekable);
    }

    private static MemoryStream CreateZip()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        using (var payload = archive.CreateEntry("x.txt", CompressionLevel.NoCompression).Open()) payload.Write(new byte[] { 1, 2, 3 });
        stream.Position = 0; return stream;
    }

    private static byte[] GetZipBytes() { using var stream = CreateZip(); return stream.ToArray(); }

    private static byte[] GetZip64Bytes()
    {
        var bytes = GetZipBytes(); var eocd = bytes.Length - 22;
        var cdOffset = U32(bytes, eocd + 16); var cdSize = U32(bytes, eocd + 12);
        using var output = new MemoryStream(); output.Write(bytes, 0, eocd);
        using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x06064b50u); writer.Write(44UL); writer.Write((ushort)45); writer.Write((ushort)45);
            writer.Write(0u); writer.Write(0u); writer.Write(1UL); writer.Write(1UL);
            writer.Write((ulong)cdSize); writer.Write((ulong)cdOffset);
            writer.Write(0x07064b50u); writer.Write(0u); writer.Write((ulong)eocd); writer.Write(1u);
            writer.Write(0x06054b50u); writer.Write((ushort)0); writer.Write((ushort)0);
            writer.Write(ushort.MaxValue); writer.Write(ushort.MaxValue);
            writer.Write(uint.MaxValue); writer.Write(uint.MaxValue); writer.Write((ushort)0);
        }
        return output.ToArray();
    }

    private static async Task AssertInvalid(Stream stream, int maxEntries = 4, long maxArchiveBytes = 1024 * 1024, long maxCentralBytes = 4096)
    {
        using (stream)
        {
            var error = await Assert.ThrowsAsync<StammdatenException>(() => SicherungsZipVorpruefung.Pruefe(stream, maxEntries, maxArchiveBytes, maxCentralBytes));
            Assert.Equal(Fehlertext, error.Message);
        }
    }

    private static ushort U16(byte[] b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o, 2));
    private static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4));
    private static void U16(byte[] b, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o, 2), v);
    private static void U32(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o, 4), v);
    private static void U64(byte[] b, int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(o, 8), v);

    private sealed class TrackingStream : Stream
    {
        private readonly MemoryStream _inner;
        public TrackingStream(byte[] bytes) => _inner = new MemoryStream(bytes);
        public long BytesRead { get; private set; }
        public override bool CanRead => _inner.CanRead; public override bool CanSeek => _inner.CanSeek; public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) { var n = _inner.Read(buffer, offset, count); BytesRead += n; return n; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { var n = await _inner.ReadAsync(buffer, cancellationToken); BytesRead += n; return n; }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => CountAsync(_inner.ReadAsync(buffer, offset, count, cancellationToken));
        private async Task<int> CountAsync(Task<int> task) { var n = await task; BytesRead += n; return n; }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Length => throw new NotSupportedException();
    }
}
