using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>HTTP-Client für die versionierte Aktivitätsauswertung.</summary>
public sealed class AktivitaetsauswertungHttpClient(HttpClient http) : IAktivitaetsauswertung
{
    /// <inheritdoc />
    public async Task<AktivitaetsauswertungAntwort> Laden(
        AktivitaetsauswertungAnfrage anfrage, CancellationToken ct = default)
    {
        var query = $"?von={Encode(anfrage.Von.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}" +
                    $"&bis={Encode(anfrage.Bis.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}" +
                    $"&raster={Encode(anfrage.Raster.ToString())}";
        if (anfrage.AktivitaetId.HasValue)
            query += $"&aktivitaetId={anfrage.AktivitaetId.Value.ToString(CultureInfo.InvariantCulture)}";
        if (anfrage.KategorieId.HasValue)
            query += $"&kategorieId={anfrage.KategorieId.Value.ToString(CultureInfo.InvariantCulture)}";

        using var response = await http.GetAsync("api/v1/auswertung/aktivitaeten" + query, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new UngueltigeTourException("Das Backend hat die Auswertungsanfrage abgelehnt.");
        if (!response.IsSuccessStatusCode)
            throw new BackendApiException(response.StatusCode);
        return await response.Content.ReadFromJsonAsync<AktivitaetsauswertungAntwort>(cancellationToken: ct)
            ?? throw new BackendApiException(response.StatusCode);
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
