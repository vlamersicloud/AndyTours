using AndyTours.Api;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AndyTours.Tests.Sicherung;

public sealed class ApiSicherungsdownloadOwnedResultTests
{
    private static SicherungAntwort Entry(byte[] bytes) =>
        new(Guid.NewGuid(), Sicherungsart.LegacySqlite, null, bytes.LongLength,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));

    [Fact]
    public async Task SuccessWritesExactContentAndDisposesSourceOnce()
    {
        var bytes = new byte[] { 2, 4, 6 };
        var (lease, disposalCount) = Lease(bytes);
        using var response = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = response;

        await new SicherungsdownloadEndpunkt.OwnedSicherungsdownloadResult(lease, Entry(bytes), "safe.db")
            .ExecuteAsync(context);

        Assert.Equal(bytes, response.ToArray());
        Assert.Equal(bytes.LongLength, context.Response.ContentLength);
        Assert.Equal(1, disposalCount());
    }

    [Fact]
    public async Task PreCancelledRequestStillDisposesSourceOnce()
    {
        var bytes = new byte[] { 1 };
        var (lease, disposalCount) = Lease(bytes);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext();
        context.RequestAborted = cancellation.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SicherungsdownloadEndpunkt.OwnedSicherungsdownloadResult(lease, Entry(bytes), "safe.db")
                .ExecuteAsync(context));

        Assert.Equal(1, disposalCount());
    }

    [Fact]
    public async Task HeaderFailureStillDisposesSourceOnce()
    {
        var bytes = new byte[] { 1 };
        var (lease, disposalCount) = Lease(bytes);
        var context = new DefaultHttpContext();
        context.Features.Get<IHttpResponseFeature>()!.Headers = new HeaderDictionary { IsReadOnly = true };

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            new SicherungsdownloadEndpunkt.OwnedSicherungsdownloadResult(lease, Entry(bytes), "safe.db")
                .ExecuteAsync(context));

        Assert.Equal(1, disposalCount());
    }

    [Fact]
    public async Task BodyFailureStillDisposesSourceOnce()
    {
        var bytes = new byte[] { 1, 2 };
        var (lease, disposalCount) = Lease(bytes);
        var context = new DefaultHttpContext();
        using var throwingBody = new ThrowingWriteStream();
        context.Response.Body = throwingBody;

        await Assert.ThrowsAsync<IOException>(() =>
            new SicherungsdownloadEndpunkt.OwnedSicherungsdownloadResult(lease, Entry(bytes), "safe.db")
                .ExecuteAsync(context));

        Assert.Equal(1, disposalCount());
    }

    private static (SicherungsdownloadQuelle Lease, Func<int> DisposalCount) Lease(byte[] bytes)
    {
        var count = 0;
        var stream = new MemoryStream(bytes, writable: false);
        var entry = Entry(bytes);
        return (new SicherungsdownloadQuelle(stream, entry, () =>
        {
            count++;
            stream.Dispose();
            return ValueTask.CompletedTask;
        }), () => count);
    }

    private sealed class ThrowingWriteStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("fake body write failure"));
    }
}
