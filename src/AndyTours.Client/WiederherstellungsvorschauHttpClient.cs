namespace AndyTours.Client;

using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>HTTP client for a metadata-only restore preview; it does not apply restores.</summary>
public sealed class WiederherstellungsvorschauHttpClient(HttpClient http) : IWiederherstellungsvorschau
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task<WiederherstellungsvorschauAntwort> Pruefen(
        WiederherstellungsvorschauAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var geprueft = WiederherstellungsvorschauRegeln.PruefeAnfrage(anfrage);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "api/v1/sicherungen/wiederherstellungsvorschau/")
        {
            Content = JsonContent.Create(geprueft, options: JsonOptions)
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Sicherungsanforderung für die Vorschau ist ungültig.");
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Sicherung für die Vorschau wurde nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Sicherung stimmt nicht mit dem erwarteten Inhalt überein.");
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException("Wiederherstellungsvorschau ist nicht verfügbar.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Wiederherstellungsvorschau fehlgeschlagen.", null, response.StatusCode);

        WiederherstellungsvorschauAntwort result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<WiederherstellungsvorschauAntwort>(JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? throw InvalidResponse();
        }
        catch (JsonException) { throw InvalidResponse(); }
        catch (NotSupportedException) { throw InvalidResponse(); }

        if (!WiederherstellungsvorschauRegeln.IstMetadatenAntwortGueltig(result, geprueft)) throw InvalidResponse();
        cancellationToken.ThrowIfCancellationRequested();
        return result with { Hinweise = Array.AsReadOnly(result.Hinweise.ToArray()) };
    }

    private static HttpRequestException InvalidResponse() =>
        new("Wiederherstellungsvorschau-Antwort ungültig.");
}
