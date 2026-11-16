using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
namespace AndyTours.Client;
public sealed class GarminEinstellungenHttpClient(HttpClient http) : IGarminEinstellungen
{
    private const string Root = "api/v1/garmin/einstellungen";
    public async Task<GarminEinstellungenAntwort> Laden(CancellationToken ct = default)
    {
        using var response=await http.GetAsync(Root,HttpCompletionOption.ResponseHeadersRead,ct);
        await Check(response);
        return await Read(response,ct);
    }
    public Task<GarminEinstellungenAntwort> SchluesselSpeichern(GarminSchluesselAnfrage anfrage,CancellationToken ct=default)=>Send(HttpMethod.Put,Root+"/schluessel",anfrage,ct);
    public Task<GarminEinstellungenAntwort> SchluesselEntfernen(GarminVersionAnfrage anfrage,CancellationToken ct=default)=>Send(HttpMethod.Post,Root+"/schluessel/entfernen",anfrage,ct);
    public Task<GarminEinstellungenAntwort> AutomatikSpeichern(GarminAutomatikAnfrage anfrage,CancellationToken ct=default)=>Send(HttpMethod.Put,Root+"/automatik",anfrage,ct);
    private async Task<GarminEinstellungenAntwort> Send<T>(HttpMethod method,string path,T body,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(method,path){Content=JsonContent.Create(body)};
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        await Check(response);
        return await Read(response,ct);
    }
    private static async Task<GarminEinstellungenAntwort> Read(HttpResponseMessage response,CancellationToken ct)=>
        await response.Content.ReadFromJsonAsync<GarminEinstellungenAntwort>(cancellationToken:ct) ?? throw new BackendApiException(response.StatusCode);
    private static Task Check(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode)return Task.CompletedTask;
        if(response.StatusCode==HttpStatusCode.BadRequest)throw new StammdatenException(StammdatenFehlerart.Ungueltig,"Garmin-Einstellungen sind ungültig.");
        if(response.StatusCode==HttpStatusCode.Conflict)throw new StammdatenException(StammdatenFehlerart.Konflikt,"Garmin-Einstellungen wurden zwischenzeitlich geändert.");
        throw new BackendApiException(response.StatusCode);
    }
}
