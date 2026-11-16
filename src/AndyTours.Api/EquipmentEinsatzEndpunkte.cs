using AndyTours.Application.Auswertung;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

/// <summary>Read-only HTTP-Endpunkt für die Equipment-Einsatz-Auskunft.</summary>
public static class EquipmentEinsatzEndpunkte
{
    public static IEndpointRouteBuilder MapEquipmentEinsatz(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/equipment-einsatz", async (IEquipmentEinsatz service, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            EquipmentEinsatzAntwort result = await service.Laden(ct);
            return Results.Ok(result);
        }).WithTags("Equipment").Produces<EquipmentEinsatzAntwort>().ProducesProblem(400);
        return app;
    }
}
