using System.Net.Http.Json;
using AndyTours.Application.Verbindungen;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typisierter, ausschließlich über versionierte HTTP-Ressourcen kommunizierender Client.</summary>
public sealed class BackendHttpVerbindung(HttpClient http) : IBackendVerbindung
{
    /// <inheritdoc />
    public async Task<bool> IstErreichbarAsync(CancellationToken abbruch = default)
    {
        try
        {
            VerbindungAntwort? antwort = await http.GetFromJsonAsync<VerbindungAntwort>("api/v1/health", abbruch);
            return antwort?.Status == "bereit";
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
