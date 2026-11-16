using AndyTours.Application.Garmin;
using AndyTours.Contracts.V1;
namespace AndyTours.Api;
public static class GarminEinstellungenEndpunkte
{
    public static IEndpointRouteBuilder MapGarminEinstellungen(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/v1/garmin/einstellungen").WithTags("Garmin-Einstellungen");
        group.MapGet("", async (IGarminEinstellungen service,HttpContext context,CancellationToken ct) => WithVersion(await service.Laden(ct),context))
            .Produces<GarminEinstellungenAntwort>().ProducesProblem(400).ProducesProblem(409);
        group.MapPut("/schluessel",async (GarminSchluesselAnfrage request,IGarminEinstellungen service,HttpContext context,CancellationToken ct)=>WithVersion(await service.SchluesselSpeichern(request,ct),context))
            .Produces<GarminEinstellungenAntwort>().ProducesProblem(400).ProducesProblem(409);
        group.MapPost("/schluessel/entfernen",async (GarminVersionAnfrage request,IGarminEinstellungen service,HttpContext context,CancellationToken ct)=>WithVersion(await service.SchluesselEntfernen(request,ct),context))
            .Produces<GarminEinstellungenAntwort>().ProducesProblem(400).ProducesProblem(409);
        group.MapPut("/automatik",async (GarminAutomatikAnfrage request,IGarminEinstellungen service,HttpContext context,CancellationToken ct)=>WithVersion(await service.AutomatikSpeichern(request,ct),context))
            .Produces<GarminEinstellungenAntwort>().ProducesProblem(400).ProducesProblem(409);
        return endpoints;
    }
    private static IResult WithVersion(GarminEinstellungenAntwort response,HttpContext context)
    {
        context.Response.Headers.ETag=$"\"{response.Version}\"";
        return Results.Ok(response);
    }
}
