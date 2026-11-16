using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class PulsAuswertungHttpClient(HttpClient http) : IPulsAuswertung
{
    private const string Root = "api/v1/auswertung/puls";

    public async Task<PulsReferenzAntwort> LadenReferenz(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"{Root}/referenz",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<PulsReferenzAntwort>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    public async Task<PulsReferenzAntwort> AktualisierenReferenz(
        PulsReferenzAktualisierenAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null || anfrage.Version is not { Length: 64 } ||
            !anfrage.Version.All(Uri.IsHexDigit))
            throw Invalid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/referenz/aktualisieren")
        {
            Content = JsonContent.Create(anfrage)
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<PulsReferenzAntwort>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    public async Task<PulsAuswertungAntwort> Auswerten(
        PulsAuswertungsAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null || anfrage.Von > anfrage.Bis || !Enum.IsDefined(anfrage.Raster) ||
            anfrage.AktivitaetId is <= 0 || anfrage.KategorieId is <= 0 ||
            (anfrage.ProfilSport is not null && string.IsNullOrWhiteSpace(anfrage.ProfilSport)))
            throw Invalid();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/")
        {
            Content = JsonContent.Create(anfrage)
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<PulsAuswertungAntwort>(
            cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.BadRequest) throw Invalid();
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt,
                "Pulsreferenz oder Auswertung wurde zwischenzeitlich geändert.");
        throw new BackendApiException(response.StatusCode);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Pulsauswertungsanfrage ungültig.");
}
