using System.Net;
using System.Security.Cryptography;
using System.Text;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Sicherung;

public sealed class ApiSicherungsdownloadClientTests
{
    private static readonly byte[] Payload = [1, 3, 5, 7, 9];
    private static readonly byte[] CanceledPayload = [4, 2];
    private static readonly byte[] LengthReference = [8, 6, 4, 2];
    private static readonly byte[] TooShort = [8, 6, 4];
    private static readonly byte[] TooLong = [8, 6, 4, 3, 1];
    [Fact]
    public async Task StreamsAndVerifiesExactBytesWithoutOwningHttpClientOrDestination()
    {
        var entry = Entry(Payload);
        var entryId = entry.Id;
        var entryHash = entry.InhaltHash;
        var handler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, Payload)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var client = new SicherungsdownloadHttpClient(http);
        using var destination = new MemoryStream();

        await client.Download(entry, destination, CancellationToken.None);

        Assert.Equal(Payload, destination.ToArray());
        Assert.False(handler.Disposed);
        Assert.Equal("/api/v1/sicherungen/download", handler.Path);
        Assert.Equal(HttpMethod.Post, handler.Method);
        using (var requestJson = System.Text.Json.JsonDocument.Parse(handler.Body!))
        {
            Assert.Equal(entryId, requestJson.RootElement.GetProperty("sicherungsId").GetGuid());
            Assert.Equal(entryHash, requestJson.RootElement.GetProperty("erwarteterInhaltHash").GetString());
        }
        using var probe = await http.GetAsync("https://localhost/health");
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task PreCancelledDownloadDoesNotSend()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("Unexpected request."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var destination = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SicherungsdownloadHttpClient(http).Download(Entry(CanceledPayload), destination, cts.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CancellationDuringResponseBodyReadStopsStreamingWithoutRetry()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler((_, _) =>
        {
            var content = new StreamContent(new BlockingReadStream(started));
            content.Headers.ContentLength = Payload.LongLength;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        using var cts = new CancellationTokenSource();
        using var destination = new MemoryStream();
        Task? pending = null;
        try
        {
            pending = new SicherungsdownloadHttpClient(http).Download(Entry(Payload), destination, cts.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending!);
            Assert.Equal(1, handler.Calls);
            Assert.Equal(0, destination.Length);
        }
        finally
        {
            cts.Cancel();
            if (pending is not null)
            {
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception) { }
            }
        }
    }

    [Fact]
    public async Task RejectsWrongLengthAndHashAndLeavesCallerDestinationOwned()
    {
        var entry = Entry(LengthReference);
        foreach (var response in new[]
        {
            Response(HttpStatusCode.OK, TooShort, LengthReference.LongLength),
            Response(HttpStatusCode.OK, TooLong, LengthReference.LongLength)
        })
        {
            var handler = new Handler((_, _) => Task.FromResult(response));
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
            using var destination = new MemoryStream();
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new SicherungsdownloadHttpClient(http).Download(entry, destination, CancellationToken.None));
            Assert.True(destination.CanRead);
        }

        var wrongHash = entry with { InhaltHash = new string('A', 64) };
        var hashHandler = new Handler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, LengthReference)));
        using var hashHttp = new HttpClient(hashHandler) { BaseAddress = new Uri("https://localhost/") };
        using var hashDestination = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SicherungsdownloadHttpClient(hashHttp).Download(wrongHash, hashDestination, CancellationToken.None));
        Assert.True(hashDestination.CanWrite);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(413)]
    [InlineData(503)]
    public async Task NonSuccessResponseBodyIsNotRead(int statusCode)
    {
        var content = new TrackingContent(Encoding.UTF8.GetBytes("error"));
        var handler = new Handler((_, _) => Task.FromResult(
            new HttpResponseMessage((HttpStatusCode)statusCode) { Content = content }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        using var target = new MemoryStream();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new SicherungsdownloadHttpClient(http).Download(Entry(CanceledPayload), target, CancellationToken.None));
        Assert.Equal(0, content.Reads);
        Assert.True(content.Disposed);
    }

    private static SicherungAntwort Entry(byte[] bytes) =>
        new(Guid.NewGuid(), Sicherungsart.LegacySqlite, null, bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes))) { LegacyZeitstempel = "2026-11-18_05-00-00" };

    private static HttpResponseMessage Response(HttpStatusCode status, byte[] bytes, long? declaredLength = null)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes) };
        if (declaredLength is not null) response.Content.Headers.ContentLength = declaredLength;
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Disposed { get; private set; }
        public string? Path { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Path = request.RequestUri?.AbsolutePath;
            Method = request.Method;
            if (request.Content is not null) Body = await request.Content.ReadAsStringAsync(cancellationToken);
            return await send(request, cancellationToken);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class BlockingReadStream(TaskCompletionSource<bool> started) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TrackingContent(byte[] bytes) : HttpContent
    {
        public int Reads { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            Reads++;
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }
        protected override bool TryComputeLength(out long length) { length = bytes.Length; return true; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }
}
