namespace AndyTours.Client;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

public sealed class SicherungsarchivErstellungHttpClient(HttpClient http) : ISicherungsarchivErstellung
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task<SicherungsarchivErstellungsantwort> Erstellen(
        SicherungsarchivErstellungsanfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.VorgangsId == Guid.Empty)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Sicherungsanfrage ungültig.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/sicherungen/")
        {
            Content = JsonContent.Create(anfrage, options: JsonOptions)
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Sicherungsanfrage ungültig.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Sicherungsvorgang steht im Konflikt.");
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException("Vollarchiv-Erstellung ist nicht verfügbar.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Vollarchiv-Erstellung fehlgeschlagen.", null, response.StatusCode);

        SicherungsarchivErstellungsantwort result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<SicherungsarchivErstellungsantwort>(JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? throw InvalidResponse();
        }
        catch (JsonException) { throw InvalidResponse(); }
        catch (NotSupportedException) { throw InvalidResponse(); }

        var backup = result.Sicherung;
        if (backup is null || backup.Id != anfrage.VorgangsId ||
            backup.Art != Sicherungsart.Vollarchiv ||
            backup.ErstelltUtc is not { Offset: var offset } || offset != TimeSpan.Zero ||
            backup.LegacyZeitstempel is not null || backup.Byteanzahl <= 0 ||
            backup.InhaltHash is not { Length: 64 } || !backup.InhaltHash.All(Uri.IsHexDigit))
            throw InvalidResponse();

        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static HttpRequestException InvalidResponse() =>
        new("Vollarchiv-Antwort ungültig.");
}
