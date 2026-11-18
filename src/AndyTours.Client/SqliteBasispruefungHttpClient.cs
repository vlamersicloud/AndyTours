namespace AndyTours.Client;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Uses but never owns the caller's authenticated HttpClient.</summary>
public sealed class SqliteBasispruefungHttpClient : ISqliteBasispruefung
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { RespectRequiredConstructorParameters = true };
    private readonly HttpClient _http;
    public SqliteBasispruefungHttpClient(HttpClient http) => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<SqliteBasispruefungAntwort> Pruefen(SqliteBasispruefungAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.SicherungsId == Guid.Empty || !IsSha256(anfrage.ErwarteterInhaltHash))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Prüfanfrage ungültig.");
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/sicherungen/sqlite-basispruefung")
            { Content = JsonContent.Create(anfrage, options: JsonOptions) };
        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Prüfanfrage ungültig.");
        if (response.StatusCode == HttpStatusCode.NotFound) throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Sicherung nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict) throw new StammdatenException(StammdatenFehlerart.Konflikt, "Sicherungsstand geändert.");
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable) throw new HttpRequestException("SQLite-Prüfung nicht verfügbar.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("SQLite-Prüfung fehlgeschlagen.", null, response.StatusCode);
        try
        {
            var antwort = await response.Content.ReadFromJsonAsync<SqliteBasispruefungAntwort>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (antwort is null || antwort.SicherungsId != anfrage.SicherungsId || antwort.Hinweise is null || antwort.Hinweise.Count > 16 ||
                antwort.Hinweise.Any(h => h is null || h.Length > 256) || !IstGueltig(antwort)) throw InvalidResponse();
            cancellationToken.ThrowIfCancellationRequested();
            return antwort with { Hinweise = Array.AsReadOnly(antwort.Hinweise.ToArray()) };
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (JsonException) { throw InvalidResponse(); }
        catch (NotSupportedException) { throw InvalidResponse(); }
    }

    private static bool IstGueltig(SqliteBasispruefungAntwort a) => a.Status switch
    {
        "ok" => a.SchemaVersion is >= 1 and <= 5 && a.CoreTablesChecked == 8 && a.QuickCheck,
        "unsupported_schema" => a.SchemaVersion is 0 or > 5 && a.CoreTablesChecked is >= 0 and <= 8 && !a.QuickCheck,
        "invalid_input" or "readonly_unavailable" or "core_schema_invalid" or "integrity_failed" or "check_failed" =>
            a.SchemaVersion is null or >= 0 && a.CoreTablesChecked is >= 0 and <= 8 && !a.QuickCheck,
        _ => false
    };
    private static bool IsSha256(string? value)
    {
        if (value is null || value.Length != 64) return false;
        Span<byte> bytes = stackalloc byte[32];
        return value.All(Uri.IsHexDigit);
    }
    private static HttpRequestException InvalidResponse() => new("SQLite-Prüfantwort ungültig.");
}
