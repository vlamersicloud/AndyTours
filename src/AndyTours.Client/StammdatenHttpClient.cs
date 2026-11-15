using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>HTTP-Implementierung des Stammdaten-Leseports.</summary>
public sealed class StammdatenHttpClient(HttpClient http) : IStammdatenLeser
{
    /// <inheritdoc />
    public async Task<StammdatenAntwort> Laden(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/v1/stammdaten", ct);
        if (!response.IsSuccessStatusCode)
            throw new BackendApiException(response.StatusCode);

        return await response.Content.ReadFromJsonAsync<StammdatenAntwort>(cancellationToken: ct)
            ?? throw new BackendApiException(response.StatusCode);
    }
}
