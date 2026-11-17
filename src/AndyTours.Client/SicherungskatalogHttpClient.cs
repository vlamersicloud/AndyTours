namespace AndyTours.Client;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;

public sealed class SicherungskatalogHttpClient(HttpClient http) : ISicherungskatalog
{
    public async Task<SicherungListeAntwort> Liste(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/sicherungen/");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new HttpRequestException("Sicherungskataloganfrage ungültig.", null, response.StatusCode);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException("Vollarchiv-Katalog ist derzeit nicht verfügbar.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Sicherungskatalog konnte nicht geladen werden.", null, response.StatusCode);

        SicherungListeAntwort result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<SicherungListeAntwort>(
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
                { RespectRequiredConstructorParameters = true }, cancellationToken).ConfigureAwait(false)
                ?? throw InvalidResponse();
        }
        catch (System.Text.Json.JsonException) { throw InvalidResponse(); }
        catch (NotSupportedException) { throw InvalidResponse(); }

        if (result.Eintraege is null) throw InvalidResponse();
        var ids = new HashSet<Guid>();
        foreach (var entry in result.Eintraege)
        {
            if (entry is null || entry.Id == Guid.Empty || !ids.Add(entry.Id) ||
                !Enum.IsDefined(entry.Art) || entry.Byteanzahl < 0 ||
                entry.InhaltHash is not { Length: 64 } || !entry.InhaltHash.All(Uri.IsHexDigit))
                throw InvalidResponse();

            if (entry.Art == Sicherungsart.LegacySqlite)
            {
                if (entry.ErstelltUtc is not null ||
                    entry.LegacyZeitstempel is not { } legacy ||
                    !DateTime.TryParseExact(legacy, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _))
                    throw InvalidResponse();
            }
            else if (entry.ErstelltUtc is not { Offset: var offset } || offset != TimeSpan.Zero || entry.LegacyZeitstempel is not null)
                throw InvalidResponse();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new SicherungListeAntwort(new ReadOnlyCollection<SicherungAntwort>(result.Eintraege.ToArray()));
    }

    private static HttpRequestException InvalidResponse() =>
        new("Sicherungskatalogantwort ungültig.");
}
