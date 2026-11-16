using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>HTTP adapter for FIT uploads; the supplied HttpClient remains caller-owned.</summary>
public sealed class FitUploadsHttpClient(HttpClient http) : IFitUploads
{
    private const string Root = "api/v1/fit-uploads";

    public async Task<FitUploadAntwort> Hochladen(Guid uploadId, string dateiname, Stream inhalt, CancellationToken cancellationToken)
    {
        if (uploadId == Guid.Empty || inhalt is null || !inhalt.CanRead) throw Invalid();
        var normalized = FitUploadRegeln.Dateiname(dateiname);
        using var content = new StreamContent(new NichtBesitzenderStream(inhalt));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = normalized };
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Root}/{uploadId:D}") { Content = content };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await Check(response).ConfigureAwait(false);
        return await Read<FitUploadAntwort>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FitUploadAntwort?> Laden(Guid uploadId, CancellationToken cancellationToken)
    {
        if (uploadId == Guid.Empty) throw Invalid();
        using var response = await http.GetAsync($"{Root}/{uploadId:D}", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await Check(response).ConfigureAwait(false);
        return await Read<FitUploadAntwort>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FitUploadListeAntwort> Liste(int start, int anzahl, CancellationToken cancellationToken)
    {
        if (start < 0 || anzahl is < 1 or > 200) throw Invalid();
        var path = $"{Root}/?start={start.ToString(CultureInfo.InvariantCulture)}&anzahl={anzahl.ToString(CultureInfo.InvariantCulture)}";
        using var response = await http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await Check(response).ConfigureAwait(false);
        return await Read<FitUploadListeAntwort>(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<FitUploadPruefungAntwort> Pruefen(FitUploadPruefanfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        return Post<FitUploadPruefanfrage, FitUploadPruefungAntwort>("pruefung", anfrage, cancellationToken);
    }

    public Task<FitUploadUebernahmeAntwort> Uebernehmen(FitUploadUebernahmeAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        return Post<FitUploadUebernahmeAnfrage, FitUploadUebernahmeAntwort>("uebernehmen", anfrage, cancellationToken);
    }

    private async Task<TResponse> Post<TRequest, TResponse>(string action, TRequest payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/{action}") { Content = JsonContent.Create(payload) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await Check(response).ConfigureAwait(false);
        return await Read<TResponse>(response, ct).ConfigureAwait(false);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false)
        ?? throw new BackendApiException(response.StatusCode);

    private static Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge) throw new FitUploadZuGrossException();
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Upload-Anfrage ungültig.");
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "FIT-Upload wurde nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "FIT-Upload-Konflikt.");
        throw new BackendApiException(response.StatusCode);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "FIT-Upload-Anfrage ungültig.");

    private sealed class NichtBesitzenderStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() => await base.DisposeAsync().ConfigureAwait(false);
    }
}
