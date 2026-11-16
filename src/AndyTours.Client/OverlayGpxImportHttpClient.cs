using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Streams one caller-owned GPX body to the overlay API.</summary>
public sealed class OverlayGpxImportHttpClient(HttpClient http) : IOverlayGpxImport
{
    private const string Root = "api/v1/overlays/gpx";

    public async Task<OverlayAnlegenAntwort> Importieren(
        OverlayGpxImportAnfrage anfrage, Stream inhalt, CancellationToken cancellationToken)
    {
        anfrage = OverlayGpxRegeln.Normalisieren(anfrage);
        if (inhalt is null || !inhalt.CanRead) throw Invalid();
        cancellationToken.ThrowIfCancellationRequested();

        var query = anfrage.OrdnerId is { } folder
            ? $"?ordnerId={folder.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        using var content = new StreamContent(new NichtBesitzenderStream(inhalt));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/gpx+xml");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = anfrage.Dateiname
        };
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Root}/{anfrage.AnfrageId:D}{query}")
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{anfrage.Version}\"");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<OverlayAnlegenAntwort>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new BackendApiException(response.StatusCode);
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.BadRequest) throw Invalid();
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay-Ordner nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Overlay-Bestand wurde geändert; bitte neu laden.");
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge) throw new OverlayGpxZuGrossException();
        throw new BackendApiException(response.StatusCode);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Overlay-GPX-Anfrage ungültig.");

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
        public override ValueTask DisposeAsync() => base.DisposeAsync();
    }
}
