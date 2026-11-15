using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AndyTours.Import.Garmin;

/// <summary>Lesender Zugang zur offiziellen Garmin Personal API; Schlüssel und Token bleiben im Speicher.</summary>
/// <remarks>Eine Instanz gehört zu genau einem sequenziellen Importlauf. HttpClient bleibt Eigentum des Aufrufers.</remarks>
public sealed class GarminPersonalClient(HttpClient http, string apiKey)
{
    private static readonly Uri Basis = new("https://personal.connectapi.garmin.com");
    private string? _token;

    /// <summary>Liest die IDs vollständig; für Downloads den verzögerten Stream verwenden.</summary>
    public async Task<IReadOnlyList<long>> LeseRadaktivitaetenAsync(CancellationToken cancellationToken = default)
    {
        var ergebnis = new List<long>();
        await foreach (long id in LeseRadaktivitaetsIdsAsync(cancellationToken).ConfigureAwait(false))
        {
            ergebnis.Add(id);
        }

        return ergebnis;
    }

    /// <summary>Liest erst bei Bedarf die nächste Seite mit höchstens 20 Aktivitäten.</summary>
    public async IAsyncEnumerable<long> LeseRadaktivitaetsIdsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var gesehen = new HashSet<long>();
        const int seitengroesse = 20;
        for (int start = 0; ; start = checked(start + seitengroesse))
        {
            if (start > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            using var zeitlimit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            zeitlimit.CancelAfter(TimeSpan.FromMinutes(2));
            using HttpResponseMessage antwort = await SendeAsync(
                $"/activitylist-service/activities/search/activities?start={start.ToString(CultureInfo.InvariantCulture)}&limit=20&activityType=cycling",
                zeitlimit.Token).ConfigureAwait(false);
            using JsonDocument json = JsonDocument.Parse(await antwort.Content.ReadAsStringAsync(zeitlimit.Token).ConfigureAwait(false));
            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Garmin hat keine Aktivitätenliste geliefert.");
            }

            // Ganze Seite vor dem ersten yield prüfen; langsame Importauswahl soll keine HTTP-Verbindung offenhalten.
            var ids = new List<long>();
            int anzahl = json.RootElement.GetArrayLength();
            foreach (JsonElement aktivitaet in json.RootElement.EnumerateArray())
            {
                if (!aktivitaet.TryGetProperty("activityId", out JsonElement wert) || !wert.TryGetInt64(out long id) || id <= 0)
                {
                    throw new InvalidDataException("Garmin hat eine ungültige Aktivitäts-ID geliefert.");
                }

                if (gesehen.Add(id))
                {
                    ids.Add(id);
                }
            }

            // Netzwerkressourcen vor einer möglichen längeren Unterbrechung am Importdialog freigeben.
            antwort.Dispose();
            json.Dispose();
            if (anzahl >= seitengroesse && ids.Count == 0)
            {
                throw new InvalidDataException("Garmin liefert wiederholt dieselbe Seite. Bitte später erneut versuchen.");
            }

            foreach (long id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return id;
            }

            if (anzahl < seitengroesse)
            {
                yield break;
            }
        }
    }

    /// <summary>Lädt das Original-ZIP einer Aktivität größenbegrenzt; keine automatische Wiederholung bei Ratenlimit.</summary>
    public async Task LadeArchivAsync(long id, Stream ziel, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        // HttpClient.Timeout gilt bei ResponseHeadersRead nicht für das Lesen des Bodys.
        // Deshalb das vollständige ZIP einschließlich Stream-Übertragung separat begrenzen.
        using var zeitlimit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        zeitlimit.CancelAfter(TimeSpan.FromMinutes(2));
        cancellationToken = zeitlimit.Token;
        using HttpResponseMessage antwort = await SendeAsync(
            $"/download-service/files/activity/{id.ToString(CultureInfo.InvariantCulture)}", cancellationToken).ConfigureAwait(false);
        await using Stream quelle = await antwort.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await KopiereBegrenztAsync(quelle, ziel, 64 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Kopiert auch bei fehlender oder falscher Content-Length nur bis zur erlaubten Größe.</summary>
    internal static async Task KopiereBegrenztAsync(Stream quelle, Stream ziel, long maximum, CancellationToken cancellationToken)
    {
        byte[] puffer = new byte[81920];
        long gesamt = 0;
        int anzahl;
        while ((anzahl = await quelle.ReadAsync(puffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            gesamt += anzahl;
            if (gesamt > maximum)
            {
                throw new InvalidDataException("Die Garmin-Datei überschreitet die erlaubte Größe.");
            }

            await ziel.WriteAsync(puffer.AsMemory(0, anzahl), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Erneuert bei 401 genau einmal den Token, ohne Fehlerantworten mit möglichen Zugangsdaten auszugeben.</summary>
    private async Task<HttpResponseMessage> SendeAsync(string pfad, CancellationToken cancellationToken)
    {
        for (int versuch = 0; ; versuch++)
        {
            if (_token is null)
            {
                await AuthentifiziereAsync(cancellationToken).ConfigureAwait(false);
            }

            using var anfrage = new HttpRequestMessage(HttpMethod.Get, new Uri(Basis, pfad));
            anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            HttpResponseMessage antwort = await http.SendAsync(anfrage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (antwort.StatusCode == HttpStatusCode.Unauthorized && versuch == 0)
            {
                antwort.Dispose();
                _token = null;
                continue;
            }

            if (!antwort.IsSuccessStatusCode)
            {
                HttpStatusCode status = antwort.StatusCode;
                antwort.Dispose();
                throw Fehler(status);
            }

            return antwort;
        }
    }

    /// <summary>Tauscht den Personal API-Key gegen den von Garmin als Text gelieferten Bearer-Token.</summary>
    private async Task AuthentifiziereAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Garmin Personal API-Key fehlt.");
        }

        if (apiKey.Trim().Any(c => !char.IsAsciiLetterOrDigit(c) && !"-._~+/=".Contains(c)))
        {
            throw new InvalidOperationException("Ungültiges API-Key-Format. Nur den Schlüssel ohne Anführungszeichen oder ApiKey-Präfix verwenden.");
        }

        using var anfrage = new HttpRequestMessage(HttpMethod.Post,
            new Uri(Basis, "/personalapitoken-service/personal-api/personal-api-key/token"));
        anfrage.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey.Trim());
        using HttpResponseMessage antwort = await http.SendAsync(anfrage, cancellationToken).ConfigureAwait(false);
        if (!antwort.IsSuccessStatusCode)
        {
            throw Fehler(antwort.StatusCode);
        }

        _token = (await antwort.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(_token) || _token.Any(c => !char.IsAsciiLetterOrDigit(c) && !"-._~+/=".Contains(c)))
        {
            _token = null;
            throw new InvalidDataException("Garmin hat keinen gültigen Zugangstoken geliefert.");
        }
    }

    /// <summary>Liefert sichere, bedienbare Hinweise ohne Response-Body, Schlüssel oder Token.</summary>
    private static HttpRequestException Fehler(HttpStatusCode status) => new(
        status switch
        {
            HttpStatusCode.BadRequest => "Garmin lehnt die Anfrage ab (400). Beim Verbindungsaufbau: API-Key ohne Anführungszeichen oder ApiKey-Präfix verwenden.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Garmin-Zugriff abgelehnt. API-Key, Ablaufdatum und Berechtigungen prüfen.",
            HttpStatusCode.TooManyRequests => "Garmin-Ratenlimit erreicht (429). Bitte später erneut importieren.",
            _ => $"Garmin ist nicht verfügbar (HTTP {(int)status}). Bitte später erneut versuchen.",
        }, null, status);
}
