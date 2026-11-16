using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Export;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class PlanungsCourseExportHttpClient(HttpClient http) : IPlanungsCourseExport
{
    public async Task<CourseExportAntwort> Erstellen(PlanungsGpxExportAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null || anfrage.Planung is null) throw Invalid();
        string name = PlanungsDateiRegeln.NormalisiereName(anfrage.Name);
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/planung/course-export")
        { Content = JsonContent.Create(anfrage with { Name = name }) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest) throw Invalid();
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge) throw new PlanungsDateiZuGrossException();
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Planungs-Course konnte nicht exportiert werden.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<CourseExportAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("Planungs-Course-Antwort ungültig.");
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungs-Course-Anfrage ungültig.");
}
