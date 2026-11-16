using System.Net;
using System.Text;
using AndyTours.Application.Export;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Routing;

namespace AndyTours.Tests.Import;

public sealed class PlanungsRoutingRobustheitTests
{
    [Fact]
    public async Task Error_response_body_is_never_read_and_route_falls_back()
    {
        var handler = new ErrorHandler();
        var service = Service(handler);
        var result = await service.Berechnen(Request(), CancellationToken.None);

        Assert.True(result.IstLuftlinie);
        Assert.Equal("routing_nicht_erreichbar", result.Warncode);
        Assert.Equal(1, handler.Sends);
        Assert.False(handler.Body.WasRead);
        Assert.True(handler.Disposed);
    }

    [Fact]
    public async Task Cancellation_during_first_send_propagates_without_retry_and_disposes_client()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancelHandler(cts);
        var service = Service(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Berechnen(Request(), cts.Token));

        Assert.Equal(1, handler.Sends);
        Assert.True(handler.Disposed);
    }

    [Theory]
    [InlineData("{\"features\":[{\"geometry\":{\"coordinates\":[[11.5,48.1],[11.6,48.2]]},\"properties\":{\"voicehints\":[[0,-2147483648,0,0,0]]}}]}")]
    [InlineData("{\"features\":[{\"geometry\":{\"coordinates\":[[200,95],[201,96]]},\"properties\":{}}]}")]
    public async Task Invalid_provider_geometry_or_turn_falls_back_without_emitting_bad_values(string body)
    {
        var handler = new JsonHandler(body);
        var answer = await Service(handler).Berechnen(Request(), CancellationToken.None);
        Assert.True(answer.IstLuftlinie);
        Assert.Equal("routing_nicht_erreichbar", answer.Warncode);
        Assert.Equal(2, answer.Punkte.Count);
        Assert.All(answer.Punkte, p => Assert.InRange(p.Position.Breite, -90, 90));
    }

    private static PlanungsRoutingDienst Service(HttpMessageHandler handler) =>
        new(new Source(), () => new HttpClient(handler, disposeHandler: true));
    private static PlanungsRoutingAnfrage Request() =>
        new(CourseFahrprofil.Trekking, new(new(48.1, 11.5), null), new(new(48.2, 11.6), null));

    private sealed class Source : ICourseExportServerdaten
    {
        public Task<CourseExportServerkontext> Laden(CancellationToken cancellationToken) =>
            Task.FromResult(new CourseExportServerkontext(Fahrprofil.Trekking, Routingeinstellungen.Standard));
    }

    private abstract class BaseHandler : HttpMessageHandler
    {
        public int Sends { get; protected set; }
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class ErrorHandler : BaseHandler
    {
        public TrackingContent Body { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = Body });
        }
    }

    private sealed class JsonHandler(string json) : BaseHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/geo+json") });
        }
    }

    private sealed class CancelHandler(CancellationTokenSource cts) : BaseHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException();
        }
    }

    private sealed class TrackingContent : HttpContent
    {
        public bool WasRead { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasRead = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes("provider error")).AsTask();
        }
        protected override bool TryComputeLength(out long length) { length = -1; return false; }
    }
}
