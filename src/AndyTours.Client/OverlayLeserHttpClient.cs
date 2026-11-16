using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typed read-only overlay client using a caller-owned HttpClient.</summary>
public sealed class OverlayLeserHttpClient(HttpClient http) : IOverlayLeser
{
    private const string Root = "api/v1/overlays";

    public async Task<OverlayBestandAntwort> Laden(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(Root, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<OverlayBestandAntwort>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    public async Task<OverlayInhaltAntwort> Inhalt(
        long overlayId, string erwarteteVersion, CancellationToken cancellationToken)
    {
        if (overlayId <= 0 || erwarteteVersion is not { Length: 64 } || !erwarteteVersion.All(Uri.IsHexDigit))
            throw Invalid();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Root}/{overlayId}/inhalt");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{erwarteteVersion}\"");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<OverlayInhaltAntwort>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.BadRequest) throw Invalid();
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Overlay wurde geändert; Bestand bitte neu laden.");
        throw new BackendApiException(response.StatusCode);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Overlay-Anfrage ungültig.");
}
