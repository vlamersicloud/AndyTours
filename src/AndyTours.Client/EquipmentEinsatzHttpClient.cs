using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Read-only HTTP-Client für die Equipment-Einsatz-Auskunft.</summary>
public sealed class EquipmentEinsatzHttpClient(HttpClient http) : IEquipmentEinsatz
{
    public async Task<EquipmentEinsatzAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/equipment-einsatz");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new UngueltigeTourException("Equipment-Einsatz konnte nicht geladen werden.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Equipment-Einsatz konnte nicht geladen werden.", null, response.StatusCode);

        EquipmentEinsatzAntwort? answer;
        try
        {
            answer = await response.Content.ReadFromJsonAsync<EquipmentEinsatzAntwort>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw InvalidResponse();
        }
        if (answer?.Zeilen is null) throw InvalidResponse();
        var rows = new EquipmentEinsatzzeileAntwort[answer.Zeilen.Count];
        int summaries = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = answer.Zeilen[i];
            if (row is null || row.Name is null ||
                (row.VorherKm is double before && !double.IsFinite(before)) ||
                (row.GesamtKm is double total && !double.IsFinite(total)))
                throw InvalidResponse();
            if (row.IstSumme)
            {
                summaries++;
                if (i != rows.Length - 1) throw InvalidResponse();
            }
            rows[i] = row with { };
        }
        if (rows.Length > 0 && summaries != 1 || rows.Length == 0 && summaries != 0)
            throw InvalidResponse();
        return new EquipmentEinsatzAntwort(Array.AsReadOnly(rows));
    }

    private static HttpRequestException InvalidResponse() => new("Equipment-Einsatzantwort konnte nicht bestätigt werden.");
}
