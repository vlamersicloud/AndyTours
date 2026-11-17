namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using AndyTours.Application.Koordination;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

public sealed class DatenbestandsMiddlewareTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().BuildServiceProvider();
    private readonly List<MemoryStream> _bodies = [];
    [Fact]
    public async Task Normal_and_exclusive_metadata_select_matching_lease_and_release_after_next()
    {
        var gate = new FakeGate();
        var nextCalls = 0;
        var middleware = new DatenbestandsMiddleware(_ =>
        {
            nextCalls++;
            Assert.Equal(nextCalls - 1, gate.Releases);
            return Task.CompletedTask;
        });
        var normal = Context();
        await middleware.InvokeAsync(normal, gate);
        Assert.Equal(1, gate.NormalCalls);
        Assert.Equal(0, gate.ExclusiveCalls);
        Assert.Equal(1, gate.Releases);
        Assert.Equal(1, nextCalls);

        var exclusive = Context();
        exclusive.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ExklusiverDatenbestand()), "unit"));
        await middleware.InvokeAsync(exclusive, gate);
        Assert.Equal(1, gate.ExclusiveCalls);
        Assert.Equal(2, gate.Releases);
        Assert.Equal(2, nextCalls);
    }

    [Fact]
    public async Task Lease_is_released_when_downstream_throws()
    {
        var gate = new FakeGate();
        var middleware = new DatenbestandsMiddleware(_ => throw new InvalidOperationException("downstream"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(Context(), gate));
        Assert.Equal(1, gate.Releases);
    }

    [Fact]
    public async Task Health_bypasses_lease_and_normal_request_passes_request_cancellation()
    {
        var gate = new FakeGate();
        var middleware = new DatenbestandsMiddleware(_ => Task.CompletedTask);
        var health = Context("/api/v1/health");
        await middleware.InvokeAsync(health, gate);
        Assert.Equal(0, gate.NormalCalls);
        Assert.Equal(0, gate.ExclusiveCalls);

        using var cts = new CancellationTokenSource();
        var context = Context("/api/v1/work");
        context.RequestAborted = cts.Token;
        await middleware.InvokeAsync(context, gate);
        Assert.Equal(cts.Token, gate.LastToken);
    }

    [Fact]
    public async Task Busy_acquisition_returns_fixed_503_and_does_not_call_next()
    {
        var gate = new FakeGate { Busy = true };
        var nextCalled = false;
        var middleware = new DatenbestandsMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = Context();
        await middleware.InvokeAsync(context, gate);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Datenbestand ist vorübergehend belegt.", json.RootElement.GetProperty("title").GetString());
        Assert.False(nextCalled);
        Assert.Equal(0, gate.Releases);
    }

    [Fact]
    public async Task Cancellation_during_acquisition_propagates_instead_of_becoming_busy()
    {
        var gate = new FakeGate { Cancel = true };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DatenbestandsMiddleware(_ => Task.CompletedTask).InvokeAsync(Context(), gate));
        Assert.Equal(0, gate.Releases);
    }

    private DefaultHttpContext Context(string path = "/api/v1/work")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var body = new MemoryStream();
        _bodies.Add(body);
        context.Response.Body = body;
        context.RequestServices = _services;
        return context;
    }

    public void Dispose()
    {
        foreach (var body in _bodies) body.Dispose();
        _services.Dispose();
    }

    private sealed class FakeGate : IDatenbestandskoordination
    {
        public bool Busy { get; init; }
        public bool Cancel { get; init; }
        public int NormalCalls { get; private set; }
        public int ExclusiveCalls { get; private set; }
        public int Releases { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public ValueTask<IDisposable> Betreten(CancellationToken cancellationToken)
        {
            NormalCalls++;
            LastToken = cancellationToken;
            return Acquire(cancellationToken);
        }

        public ValueTask<IDisposable> ExklusivBetreten(CancellationToken cancellationToken)
        {
            ExclusiveCalls++;
            LastToken = cancellationToken;
            return Acquire(cancellationToken);
        }

        private ValueTask<IDisposable> Acquire(CancellationToken cancellationToken)
        {
            if (Cancel) throw new OperationCanceledException(cancellationToken);
            if (Busy) throw new DatenbestandBelegtException();
            return ValueTask.FromResult<IDisposable>(new FakeLease(() => Releases++));
        }

        private sealed class FakeLease(Action release) : IDisposable
        {
            private int _disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) release();
            }
        }
    }
}
