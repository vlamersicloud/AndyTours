using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Export;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class PlanungsCourseExportHttpClientTests
{
    [Fact]
    public async Task Typed_course_export_serializes_planning_and_keeps_error_bodies_unread()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var dto = new CourseExportAntwort(bytes, "Café.fit", null);
        var content = new TrackingContent(JsonSerializer.Serialize(dto));
        var handler = new RecordingHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/planung/course-export", request.RequestUri!.AbsolutePath);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("Café", json.RootElement.GetProperty("name").GetString());
            Assert.Equal(0, json.RootElement.GetProperty("planung").GetProperty("profil").GetInt32());
            Assert.Equal(2, json.RootElement.GetProperty("planung").GetProperty("wegpunkte").GetArrayLength());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new PlanungsCourseExportHttpClient(http);
        var plan = new PlanungsStand(CourseFahrprofil.Rennrad, new[] { new PlanungsPosition(1, 2), new PlanungsPosition(3, 4) }, new PlanungsTeilstueck?[] { null });
        var result = await client.Erstellen(new("Cafe\u0301", plan), default);
        Assert.Equal(dto, result);
        Assert.True(content.WasDisposed);
        Assert.Equal(1, handler.Count);

        var error = new ErrorContent();
        var errors = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge) { Content = error }));
        using var http2 = new HttpClient(errors) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAsync<PlanungsDateiZuGrossException>(() => new PlanungsCourseExportHttpClient(http2).Erstellen(new("Tour", plan), default));
        Assert.False(error.WasRead); Assert.True(error.WasDisposed); Assert.Equal(1, errors.Count);
        await Assert.ThrowsAsync<StammdatenException>(() => client.Erstellen(new("../bad", plan), default));
        Assert.Equal(1, handler.Count);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    { public int Count { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Count++; return callback(request, cancellationToken); } }
    private sealed class TrackingContent(string json) : StringContent(json, Encoding.UTF8, "application/json")
    { public bool WasDisposed { get; private set; } protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); } }
    private sealed class ErrorContent : HttpContent
    {
        public bool WasRead { get; private set; } public bool WasDisposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) { WasRead = true; throw new InvalidOperationException(); }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) { WasRead = true; throw new InvalidOperationException(); }
        protected override Task<Stream> CreateContentReadStreamAsync() { WasRead = true; throw new InvalidOperationException(); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}
