using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>HTTP-Transport für atomare Mehrfachaktionen auf Touren.</summary>
public sealed partial class TourenMehrfachHttpClient(HttpClient http)
{
    private const string Basis = "api/v1/touren/mehrfach/";

    private async Task<TourenMehrfachAntwort> Senden<T>(
        string aktion, T anfrage, Guid anfrageId, IReadOnlyList<TourVersionsreferenz> touren,
        bool loeschen, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Post, Basis + aktion)
        {
            Content = JsonContent.Create(anfrage)
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
            case HttpStatusCode.RequestEntityTooLarge:
                throw new UngueltigeTourException("Mehrfachaktion wurde abgelehnt.");
            case HttpStatusCode.NotFound:
                throw new TourNichtGefundenException(0);
            case HttpStatusCode.Conflict:
            case HttpStatusCode.PreconditionFailed:
                throw new TourVersionskonfliktException();
        }
        if (!response.IsSuccessStatusCode)
            throw new BackendApiException(response.StatusCode);

        TourenMehrfachAntwort? antwort;
        try
        {
            antwort = await response.Content.ReadFromJsonAsync<TourenMehrfachAntwort>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            throw Protokollfehler();
        }

        if (!IstGueltig(antwort, anfrageId, touren, loeschen))
            throw Protokollfehler();
        return antwort!;
    }

    private static bool IstGueltig(TourenMehrfachAntwort? antwort, Guid anfrageId,
        IReadOnlyList<TourVersionsreferenz> touren, bool loeschen)
    {
        if (antwort is null || antwort.AnfrageId != anfrageId ||
            antwort.Eintraege is null || antwort.GeloeschteIds is null)
            return false;

        var erwartete = touren.Select(t => t.Id).Order().ToArray();
        if (loeschen)
            return antwort.Eintraege.Count == 0 &&
                antwort.GeloeschteIds.Order().SequenceEqual(erwartete) &&
                antwort.GeloeschteIds.All(id => id > 0) &&
                antwort.GeloeschteIds.Distinct().Count() == antwort.GeloeschteIds.Count;

        var ids = antwort.Eintraege.Select(e => e?.Id ?? 0).Order().ToArray();
        return antwort.GeloeschteIds.Count == 0 &&
            ids.SequenceEqual(erwartete) && ids.All(id => id > 0) &&
            ids.Distinct().Count() == ids.Length &&
            antwort.Eintraege.All(e => e is not null && e.Werte is not null &&
                e.Version is { Length: 64 } && e.Version.All(Uri.IsHexDigit));
    }

    private static HttpRequestException Protokollfehler() =>
        new("Mehrfachantwort konnte nicht bestätigt werden.");
}
