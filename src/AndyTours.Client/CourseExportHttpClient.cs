using System.Net;
using System.Net.Http.Json;
using System.Text;
using AndyTours.Application.Export;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class CourseExportHttpClient(HttpClient http) : ICourseExport
{
    private const string Root = "api/v1/course-export";

    public async Task<CourseExportEinstellungenAntwort> LadenEinstellungen(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"{Root}/einstellungen", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<CourseExportEinstellungenAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    public async Task<CourseExportAntwort> Erstellen(CourseExportAnfrage anfrage, CancellationToken cancellationToken)
    {
        Validate(anfrage);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/") { Content = JsonContent.Create(anfrage) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<CourseExportAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    private static void Validate(CourseExportAnfrage anfrage)
    {
        if (anfrage is null || string.IsNullOrWhiteSpace(anfrage.Name) || !Enum.IsDefined(anfrage.Profil) ||
            anfrage.Verlauf is null || anfrage.Verlauf.Count is < 2 or > 2000000) throw Invalid();
        try
        {
            var name = anfrage.Name.Trim().Normalize(NormalizationForm.FormC);
            if (name.Length == 0 || name.Length > 200 || name.Any(char.IsControl)) throw Invalid();
        }
        catch (ArgumentException) { throw Invalid(); }
        if (anfrage.Verlauf.Any(p => p is null)) throw Invalid();
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType or HttpStatusCode.RequestEntityTooLarge)
            throw Invalid();
        throw new BackendApiException(response.StatusCode);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Course-Export-Anfrage ungültig.");
}