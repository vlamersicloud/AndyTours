using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Client;

public sealed class TextlistenNutzungHttpClientTests
{
    private const string Version = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Stamp = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task NutzungRequestUsesEncodedNameAndChecksTypedResponse()
    {
        Uri? requested = null;
        HttpResponseMessage? response = null;
        TrackingContent? content = null;
        var probeCalls = 0;
        var handler = new DelegateHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/usage-probe")
            {
                probeCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
            requested = request.RequestUri;
            response = JsonResponse(new TextlistenNutzungAntwort(Textlistenart.Taetigkeit, Version, "Kette & Öl", 0, 2, 1, Stamp));
            content = Assert.IsType<TrackingContent>(response.Content);
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new TextlistenVerwaltungHttpClient(http);

        var result = await client.NutzungLesen(Textlistenart.Taetigkeit, Version, "Kette & Öl");
        Assert.Equal(2, result.Wartungen);
        Assert.Equal("/api/v1/textlisten/Taetigkeit/nutzung", requested?.AbsolutePath);
        Assert.Contains("name=Kette%20%26%20%C3%96l", requested?.Query ?? string.Empty);
        Assert.True(content!.WasDisposed);
        using var probe = await http.GetAsync("/usage-probe");
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        Assert.Equal(1, probeCalls);
        Assert.False(handler.Disposed);
    }

    [Fact]
    public async Task InFlightCancellationStopsAndDisposesResponse()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TrackingContent? content = null;
        using var http = new HttpClient(new DelegateHandler(async (_, ct) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
            var response = JsonResponse(new TextlistenNutzungAntwort(Textlistenart.Taetigkeit, Version, "Kette", 0, 0, 0, Stamp));
            content = Assert.IsType<TrackingContent>(response.Content);
            return response;
        })) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new TextlistenVerwaltungHttpClient(http);
        using var source = new CancellationTokenSource();
        Task? pending = null;
        try
        {
            pending = client.NutzungLesen(Textlistenart.Taetigkeit, Version, "Kette", source.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            source.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally
        {
            release.TrySetResult();
            if (pending is not null)
            {
                try { await pending.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (OperationCanceledException) { }
            }
        }
    }

    [Fact]
    public async Task UsageErrorBodyIsNotReadAndResponseIsDisposed()
    {
        var body = new TrackingContent(Encoding.UTF8.GetBytes("private backend details"));
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        var response = new HttpResponseMessage(HttpStatusCode.Conflict) { Content = body };
        using var http = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(response)))
            { BaseAddress = new Uri("https://example.invalid/") };
        var client = new TextlistenVerwaltungHttpClient(http);
        var error = await Assert.ThrowsAsync<AndyTours.Application.Stammdaten.StammdatenException>(() =>
            client.NutzungLesen(Textlistenart.Taetigkeit, Version, "Kette"));
        Assert.Equal(AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt, error.Art);
        Assert.True(body.WasDisposed);
    }

    [Fact]
    public async Task MalformedUsageEchoFailsClosed()
    {
        using var http = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(JsonResponse(new TextlistenNutzungAntwort(Textlistenart.Trainingsbereich,
                Version, "other", 0, 0, 0, Stamp))))) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new TextlistenVerwaltungHttpClient(http);
        await Assert.ThrowsAsync<BackendApiException>(() =>
            client.NutzungLesen(Textlistenart.Taetigkeit, Version, "Kette"));
    }

    [Fact]
    public async Task PreCancelledRequestNeverReachesHandlerAndHttpClientRemainsCallerOwned()
    {
        var calls = 0;
        var handler = new DelegateHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(JsonResponse(new TextlistenNutzungAntwort(Textlistenart.Taetigkeit,
                Version, "Kette", 0, 0, 0, Stamp)));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new TextlistenVerwaltungHttpClient(http);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.NutzungLesen(Textlistenart.Taetigkeit, Version, "Kette", source.Token));
        Assert.Equal(0, calls);
        Assert.False(handler.Disposed);
    }

    [Fact]
    public async Task MissingCountsAndNullJsonAreRejected()
    {
        foreach (var body in new[]
        {
            "{\"Art\":1,\"ListenVersion\":\"" + Version + "\",\"Name\":\"Kette\",\"Wartungen\":0,\"Alarme\":0,\"VerwendungsVersion\":\"" + Stamp + "\"}",
            "null",
            "{"
        })
        {
            using var http = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(RawResponse(body))))
                { BaseAddress = new Uri("https://example.invalid/") };
            var client = new TextlistenVerwaltungHttpClient(http);
            await Assert.ThrowsAsync<BackendApiException>(() =>
                client.NutzungLesen(Textlistenart.Taetigkeit, Version, "Kette"));
        }
    }

    [Fact]
    public async Task NegativeAndImpossibleCountsOrInvalidVersionsFailClosed()
    {
        var invalidBodies = new[]
        {
            "{\"Art\":1,\"ListenVersion\":\"" + Version + "\",\"Name\":\"Kette\",\"Touren\":0,\"Wartungen\":-1,\"Alarme\":0,\"VerwendungsVersion\":\"" + Stamp + "\"}",
            "{\"Art\":0,\"ListenVersion\":\"" + Version + "\",\"Name\":\"Bereich\",\"Touren\":0,\"Wartungen\":1,\"Alarme\":0,\"VerwendungsVersion\":\"" + Stamp + "\"}",
            "{\"Art\":1,\"ListenVersion\":\"bad\",\"Name\":\"Kette\",\"Touren\":0,\"Wartungen\":0,\"Alarme\":0,\"VerwendungsVersion\":\"" + Stamp + "\"}",
            "{\"Art\":1,\"ListenVersion\":\"" + Version + "\",\"Name\":\"Kette\",\"Touren\":0,\"Wartungen\":0,\"Alarme\":0,\"VerwendungsVersion\":\"bad\"}"
        };
        foreach (var body in invalidBodies)
        {
            using var http = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(RawResponse(body))))
                { BaseAddress = new Uri("https://example.invalid/") };
            var client = new TextlistenVerwaltungHttpClient(http);
            await Assert.ThrowsAsync<BackendApiException>(() =>
                client.NutzungLesen(body.Contains("\"Bereich\"", StringComparison.Ordinal)
                    ? Textlistenart.Trainingsbereich : Textlistenart.Taetigkeit,
                    Version, body.Contains("\"Bereich\"", StringComparison.Ordinal) ? "Bereich" : "Kette"));
        }
    }

    [Fact]
    public async Task MalformedInputIsRejectedBeforeHttpSend()
    {
        var calls = 0;
        using var http = new HttpClient(new DelegateHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(JsonResponse(new TextlistenNutzungAntwort(Textlistenart.Taetigkeit,
                Version, "Kette", 0, 0, 0, Stamp)));
        })) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new TextlistenVerwaltungHttpClient(http);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.NutzungLesen(Textlistenart.Taetigkeit, "bad", "Kette"));
        Assert.Equal(0, calls);
    }

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        RawResponse(JsonSerializer.Serialize(value, WebJson));

    private static HttpResponseMessage RawResponse(string json)
    {
        var content = new TrackingContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class TrackingContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
