using System.Net;
using System.Net.Http.Json;
using System.Text;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class PlanungsDateienHttpClient(HttpClient http) : IPlanungsDateien
{
    public async Task<PlanungsStand> Lesen(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is null || !stream.CanRead || stream.CanSeek && stream.Length > PlanungsDateiRegeln.MaxDateiBytes) throw Invalid();
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/planung/gpx/lesen")
        {
            Content = new NonOwningStreamContent(stream)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/gpx+xml");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest) throw Invalid();
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge) throw new PlanungsDateiZuGrossException();
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Planungsdatei konnte nicht gelesen werden.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<PlanungsStand>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("Planungsantwort ungültig.");
    }

    public async Task<PlanungsGpxExportAntwort> Schreiben(PlanungsGpxExportAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null || anfrage.Planung is null) throw Invalid();
        string name = PlanungsDateiRegeln.NormalisiereName(anfrage.Name);
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/planung/gpx/schreiben")
        { Content = JsonContent.Create(anfrage with { Name = name }) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest) throw Invalid();
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge) throw new PlanungsDateiZuGrossException();
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Planungsdatei konnte nicht geschrieben werden.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<PlanungsGpxExportAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("Planungsantwort ungültig.");
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungsdatei ungültig.");

    private sealed class NonOwningStreamContent(Stream stream) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) => stream.CopyToAsync(target);
        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context, CancellationToken cancellationToken) => stream.CopyToAsync(target, cancellationToken);
        protected override bool TryComputeLength(out long length) { if (stream.CanSeek) { length = stream.Length - stream.Position; return true; } length = 0; return false; }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); }
    }
}
