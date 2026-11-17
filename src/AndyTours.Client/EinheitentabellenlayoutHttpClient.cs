namespace AndyTours.Client;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Tabellenlayout;
using AndyTours.Contracts.V1;

public sealed class EinheitentabellenlayoutHttpClient(HttpClient http) : IEinheitentabellenlayout
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { RespectRequiredConstructorParameters = true };

    public Task<EinheitentabellenlayoutAntwort> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Send(new HttpRequestMessage(HttpMethod.Get, "api/v1/einstellungen/einheitentabelle/"), cancellationToken);
    }

    public Task<EinheitentabellenlayoutAntwort> Speichern(EinheitentabellenlayoutAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EinheitentabellenlayoutRegeln.Pruefe(anfrage);
        var snapshot = new EinheitentabellenlayoutAnfrage(anfrage.Version,
            Array.AsReadOnly(anfrage.Anordnung.Select(x => new Einheitentabellenspalte(x.Schluessel, x.Breite)).ToArray()),
            Array.AsReadOnly(anfrage.Ausgeblendet.ToArray()));
        return Send(new HttpRequestMessage(HttpMethod.Put, "api/v1/einstellungen/einheitentabelle/") { Content = JsonContent.Create(snapshot, options: JsonOptions) }, cancellationToken);
    }

    private async Task<EinheitentabellenlayoutAntwort> Send(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.BadRequest) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Tabellenlayout ungültig.");
            if (response.StatusCode == HttpStatusCode.Conflict) throw new StammdatenException(StammdatenFehlerart.Konflikt, "Tabellenlayout wurde zwischenzeitlich geändert.");
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("Tabellenlayout konnte nicht verarbeitet werden.", null, response.StatusCode);
            try
            {
                var result = await response.Content.ReadFromJsonAsync<EinheitentabellenlayoutAntwort>(JsonOptions, ct).ConfigureAwait(false);
                if (result is null) throw Invalid();
                try { EinheitentabellenlayoutRegeln.Pruefe(new EinheitentabellenlayoutAnfrage(result.Version, result.Anordnung, result.Ausgeblendet)); }
                catch (Exception e) when (e is StammdatenException or NullReferenceException) { throw Invalid(); }
                return new EinheitentabellenlayoutAntwort(result.Version,
                    Array.AsReadOnly(result.Anordnung.Select(x => new Einheitentabellenspalte(x.Schluessel, x.Breite)).ToArray()),
                    Array.AsReadOnly(result.Ausgeblendet.ToArray()));
            }
            catch (System.Text.Json.JsonException) { throw Invalid(); }
            catch (NotSupportedException) { throw Invalid(); }
        }
    }

    private static HttpRequestException Invalid() => new("Tabellenlayoutantwort ungültig.");
}
