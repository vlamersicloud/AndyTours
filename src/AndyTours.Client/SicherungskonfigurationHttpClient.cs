namespace AndyTours.Client;

using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

public sealed class SicherungskonfigurationHttpClient(HttpClient http) : ISicherungskonfiguration
{
    public Task<SicherungskonfigurationAntwort> Laden(CancellationToken cancellationToken) =>
        Send(new HttpRequestMessage(HttpMethod.Get, "api/v1/einstellungen/sicherung/"), cancellationToken);

    public Task<SicherungskonfigurationAntwort> Speichern(SicherungskonfigurationAnfrage anfrage, CancellationToken cancellationToken)
    {
        SicherungskonfigurationRegeln.Pruefe(anfrage);
        return Send(new HttpRequestMessage(HttpMethod.Put, "api/v1/einstellungen/sicherung/")
        {
            Content = JsonContent.Create(anfrage)
        }, cancellationToken);
    }

    private async Task<SicherungskonfigurationAntwort> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Sicherungskonfiguration ungültig.");
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Sicherungskonfiguration wurde zwischenzeitlich geändert.");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Sicherungskonfiguration konnte nicht verarbeitet werden.", null, response.StatusCode);

            SicherungskonfigurationAntwort result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<SicherungskonfigurationAntwort>(
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
                    { RespectRequiredConstructorParameters = true }, cancellationToken).ConfigureAwait(false)
                    ?? throw InvalidResponse();
            }
            catch (System.Text.Json.JsonException) { throw InvalidResponse(); }
            catch (NotSupportedException) { throw InvalidResponse(); }
            if (result.Version is not { Length: 64 } || !result.Version.All(Uri.IsHexDigit) ||
                result.Intervall is < 1 or > 99 || result.Hoechstzahl is < 1 or > 99 ||
                result.StartsSeitSicherung < 0)
                throw InvalidResponse();
            return result;
        }
    }

    private static HttpRequestException InvalidResponse() =>
        new("Sicherungskonfigurationantwort ungültig.");
}
