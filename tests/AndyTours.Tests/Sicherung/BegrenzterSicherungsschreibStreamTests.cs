namespace AndyTours.Tests.Sicherung;

using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class BegrenzterSicherungsschreibStreamTests
{
    private const string ErrorText = "Sicherungsarchiv überschreitet die konfigurierte Größe.";

    [Fact]
    public void BoundsArraySpanWritesAndAllowsZipStyleBackpatch()
    {
        using var destination = new MemoryStream();
        using var bounded = new BegrenzterSicherungsschreibStream(destination, 4);
        bounded.Write(new byte[] { 1, 2 }, 0, 2);
        bounded.Write(new byte[] { 3, 4 }.AsSpan());
        Assert.Throws<IOException>(() => bounded.WriteByte(5));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, destination.ToArray());
        bounded.Seek(-2, SeekOrigin.Current);
        bounded.WriteByte(9);
        Assert.Equal(new byte[] { 1, 2, 9, 4 }, destination.ToArray());
    }

    [Fact]
    public void BoundsPositionSeekAndSetLengthIncludingCheckedOverflow()
    {
        using var destination = new MemoryStream();
        using var bounded = new BegrenzterSicherungsschreibStream(destination, 4);
        bounded.SetLength(4);
        Assert.Throws<IOException>(() => bounded.SetLength(5));
        Assert.Throws<IOException>(() => bounded.Seek(5, SeekOrigin.Begin));
        bounded.Position = 4;
        Assert.Throws<IOException>(() => bounded.Seek(long.MaxValue, SeekOrigin.Current));
        Assert.Throws<IOException>(() => bounded.Position = -1);
        Assert.Throws<IOException>(() => bounded.Position = 5);
        bounded.SetLength(2);
        Assert.Equal(2, bounded.Length);
    }

    [Fact]
    public async Task AsyncWritesRespectCapAndCancellationWithoutPartialOverLimitWrite()
    {
        using var destination = new MemoryStream();
        using var bounded = new BegrenzterSicherungsschreibStream(destination, 4);
        Func<byte[], int, int, CancellationToken, Task> arrayWrite = bounded.WriteAsync;
        await bounded.WriteAsync(new byte[] { 1, 2 }.AsMemory());
        await arrayWrite(new byte[] { 3, 4 }, 0, 2, CancellationToken.None);
        var before = destination.ToArray();
        var error = await Assert.ThrowsAsync<IOException>(() => arrayWrite(new byte[] { 5 }, 0, 1, CancellationToken.None));
        Assert.Equal(ErrorText, error.Message);
        Assert.Equal(before, destination.ToArray());
        bounded.Position = 0;
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bounded.WriteAsync(new byte[] { 8 }.AsMemory(), cts.Token).AsTask());
        Assert.Equal(before, destination.ToArray());
        await bounded.FlushAsync(CancellationToken.None);
    }

    [Fact]
    public void DisposalLeavesDestinationOpenAndOperationsFailAfterward()
    {
        using var destination = new MemoryStream();
        var bounded = new BegrenzterSicherungsschreibStream(destination, 4);
        bounded.WriteByte(1);
        bounded.Dispose(); bounded.Dispose();
        Assert.True(destination.CanWrite);
        Assert.Equal(new byte[] { 1 }, destination.ToArray());
        Assert.Throws<ObjectDisposedException>(() => bounded.WriteByte(2));
        Assert.Throws<ObjectDisposedException>(() => _ = bounded.Length);
        Assert.False(bounded.CanWrite);
    }

    [Fact]
    public void RejectsInvalidTargetsAndExistingLengthOverCapWithoutTakingOwnership()
    {
        using var full = new MemoryStream(new byte[5]);
        Assert.Throws<IOException>(() => new BegrenzterSicherungsschreibStream(full, 4));
        Assert.True(full.CanWrite);
        using var readonlyStream = new MemoryStream(new byte[1], writable: false);
        Assert.Throws<IOException>(() => new BegrenzterSicherungsschreibStream(readonlyStream, 4));
        Assert.True(readonlyStream.CanRead);
        using var nonSeekable = new NonSeekableStream();
        Assert.Throws<IOException>(() => new BegrenzterSicherungsschreibStream(nonSeekable, 4));
        Assert.True(nonSeekable.CanWrite);
        using var destination = new MemoryStream();
        Assert.Throws<IOException>(() => new BegrenzterSicherungsschreibStream(destination, 0));
        Assert.True(destination.CanWrite);
    }

    [Fact]
    public void ReadOperationsAreUnsupported()
    {
        using var destination = new MemoryStream();
        using var bounded = new BegrenzterSicherungsschreibStream(destination, 4);
        Assert.False(bounded.CanRead);
        Assert.Throws<NotSupportedException>(() => bounded.ReadByte());
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Length => throw new NotSupportedException();
    }
}
