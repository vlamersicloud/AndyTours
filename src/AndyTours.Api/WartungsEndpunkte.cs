using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

/// <summary>HTTP-Endpunkte für Wartungen und Kilometeralarme.</summary>
public static class WartungsEndpunkte
{
    /// <summary>Registriert versionierte Wartungs- und Alarmrouten.</summary>
    public static IEndpointRouteBuilder MapWartungen(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/wartungsverwaltung").WithTags("Wartungen und Alarme");
        group.MapGet("/", async (long? aktivitaetId, long? equipmentId, IWartungsverwaltung service, HttpContext context, CancellationToken ct) =>
        {
            var result = await service.Laden(new(aktivitaetId, equipmentId), ct);
            context.Response.Headers.ETag = $"\"{result.Bestand.Version}\"";
            return Results.Ok(result);
        }).Produces<WartungsuebersichtAntwort>().ProducesProblem(400).ProducesProblem(404);
        group.MapPost("/wartungen", async (WartungSpeichernAnfrage request, IWartungsverwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.WartungAnlegen(request, ct), context))
            .Produces<WartungsbestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/wartungen/{id:long}", async (long id, WartungSpeichernAnfrage request, IWartungsverwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.WartungAendern(id, request, ct), context))
            .Produces<WartungsbestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/alarme", async (AlarmSpeichernAnfrage request, IWartungsverwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.AlarmAnlegen(request, ct), context))
            .Produces<WartungsbestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/alarme/{id:long}", async (long id, AlarmSpeichernAnfrage request, IWartungsverwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.AlarmAendern(id, request, ct), context))
            .Produces<WartungsbestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/auswahl/loeschen", async (WartungenAuswahlAnfrage request, IWartungsverwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.Loeschen(request, ct), context))
            .Produces<WartungsbestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/alarme/umschalten", async (WartungenAuswahlAnfrage request, IWartungsverwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.AlarmeUmschalten(request, ct), context))
            .Produces<WartungsbestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }

    private static IResult WithVersion(WartungsbestandAntwort result, HttpContext context)
    {
        context.Response.Headers.ETag = $"\"{result.Version}\"";
        return Results.Ok(result);
    }
}
