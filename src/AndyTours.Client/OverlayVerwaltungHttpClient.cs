using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typed client for aggregate-versioned overlay-folder mutations.</summary>
public sealed partial class OverlayVerwaltungHttpClient(HttpClient http)
{
    private const string FolderRoot = "api/v1/overlays/ordner/";

    public Task<OverlayAnlegenAntwort> OrdnerAnlegen(OverlayOrdnerAnlegenAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        ValidiereVersion(anfrage.Version);
        if (anfrage.AnfrageId == Guid.Empty || anfrage.ElternId is <= 0) throw Invalid();
        ValidiereName(anfrage.Name);
        return Send<OverlayAnlegenAntwort>(HttpMethod.Post, FolderRoot, anfrage, null, cancellationToken);
    }

    public Task<OverlayBestandAntwort> OrdnerAendern(long ordnerId, OverlayOrdnerAendernAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        ValidiereId(ordnerId);
        ArgumentNullException.ThrowIfNull(anfrage);
        ValidiereVersion(anfrage.Version);
        if (anfrage.ElternId is <= 0) throw Invalid();
        ValidiereName(anfrage.Name);
        return Send<OverlayBestandAntwort>(HttpMethod.Put, FolderRoot + Id(ordnerId), anfrage, null, cancellationToken);
    }

    public Task<OverlayBestandAntwort> OrdnerLoeschen(long ordnerId, string version, CancellationToken cancellationToken = default)
    {
        ValidiereId(ordnerId);
        ValidiereVersion(version);
        return Send<OverlayBestandAntwort>(HttpMethod.Delete, FolderRoot + Id(ordnerId), null, version, cancellationToken);
    }

    private async Task<T> Send<T>(HttpMethod method, string path, object? body, string? ifMatch, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        Check(response);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false)
            ?? throw new BackendApiException(response.StatusCode);
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.BadRequest) throw Invalid();
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Overlay nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Overlay wurde geändert; Bestand bitte neu laden.");
        throw new BackendApiException(response.StatusCode);
    }

    private static void ValidiereId(long id) { if (id <= 0) throw Invalid(); }
    private static void ValidiereVersion(string version)
    {
        if (version is not { Length: 64 } || !version.All(Uri.IsHexDigit)) throw Invalid();
    }
    private static void ValidiereName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw Invalid();
        string normalisiert;
        try { normalisiert = name.Trim().Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { throw Invalid(); }
        if (normalisiert.Length == 0 || normalisiert.Any(char.IsControl)) throw Invalid();
    }
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Overlay-Anfrage ungültig.");
}
