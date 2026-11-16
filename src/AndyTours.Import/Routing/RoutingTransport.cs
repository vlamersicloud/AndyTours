using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
namespace AndyTours.Import.Routing;

public static class RoutingTransport
{
    private const int MaxAntwortBytes = 8 * 1024 * 1024;

    public static async Task<Teilstueck?> Ermittle(HttpClient http, Routinganfrage anfrage, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(anfrage.Inhalt is null ? HttpMethod.Get : HttpMethod.Post, anfrage.Adresse);
        if (anfrage.Inhalt is not null) request.Content = new StringContent(anfrage.Inhalt, Encoding.UTF8, "application/json");
        foreach (var pair in anfrage.Kopfzeilen) request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxAntwortBytes) return null;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var bounded = new MemoryStream(); var buffer = new byte[81920]; int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (bounded.Length + read > MaxAntwortBytes) return null;
                await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            var json = Encoding.UTF8.GetString(bounded.GetBuffer(), 0, (int)bounded.Length);
            return anfrage.Dienst == Routingdienst.Openrouteservice ? LeseOpenrouteserviceAntwort.Ausfuehren(json) : LeseBRouterAntwort.Ausfuehren(json);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or FormatException or InvalidOperationException or ArgumentException) { return null; }
    }
}
