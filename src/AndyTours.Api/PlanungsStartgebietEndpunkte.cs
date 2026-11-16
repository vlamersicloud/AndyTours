using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

public static class PlanungsStartgebietEndpunkte
{
    public static IEndpointRouteBuilder MapPlanungsStartgebiet(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/planung/startgebiet", async (IPlanungsStartgebiet service, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            PlanungsStartgebietAntwort result = await service.Laden(ct);
            return Results.Ok(result);
        }).WithTags("Planung").Produces<PlanungsStartgebietAntwort>();
        return app;
    }
}
