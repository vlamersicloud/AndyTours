using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class PlanungsProfilHttpClientTests
{
    [Fact]
    public async Task Profile_get_put_conflict_and_invalid_version_are_bounded()
    {
        const string version = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var responseBody = new TrackingContent("{\"version\":\"" + version + "\",\"profil\":1}");
        var errorBody = new ErrorContent();
        var handler = new FakeHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/api/v1/planung/profil", request.RequestUri!.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = responseBody };
            }
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/v1/planung/profil", request.RequestUri!.AbsolutePath);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(version, json.RootElement.GetProperty("version").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("profil").GetInt32());
            return new HttpResponseMessage(HttpStatusCode.Conflict) { Content = errorBody };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new PlanungsProfilHttpClient(http);
        var loaded = await client.Laden(default);
        Assert.Equal(CourseFahrprofil.Trekking, loaded.Profil);
        Assert.True(responseBody.WasDisposed);
        var conflict = await Assert.ThrowsAsync<StammdatenException>(() => client.Speichern(new(version, CourseFahrprofil.Mountainbike), default));
        Assert.Equal(StammdatenFehlerart.Konflikt, conflict.Art);
        Assert.False(errorBody.WasRead);
        Assert.True(errorBody.WasDisposed);
        await Assert.ThrowsAsync<StammdatenException>(() => client.Speichern(new("bad", CourseFahrprofil.Rennrad), default));
        Assert.Equal(2, handler.Count);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Count++; return callback(request, cancellationToken); }
    }
    private sealed class TrackingContent(string value) : StringContent(value, Encoding.UTF8, "application/json")
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
    private sealed class ErrorContent : HttpContent
    {
        public bool WasRead { get; private set; }
        public bool WasDisposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) { WasRead = true; throw new InvalidOperationException(); }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) { WasRead = true; throw new InvalidOperationException(); }
        protected override Task<Stream> CreateContentReadStreamAsync() { WasRead = true; throw new InvalidOperationException(); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}
