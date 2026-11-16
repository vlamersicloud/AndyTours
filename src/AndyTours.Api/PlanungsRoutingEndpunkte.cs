using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

public static class PlanungsRoutingEndpunkte
{
    public static IEndpointRouteBuilder MapPlanungsRouting(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/planung/routing", async (PlanungsRoutingAnfrage anfrage, IPlanungsRouting routing,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await routing.Berechnen(anfrage, cancellationToken));
        })
        .WithTags("Planung")
        .Accepts<PlanungsRoutingAnfrage>("application/json")
        .Produces<PlanungsRoutingAntwort>()
        .ProducesProblem(StatusCodes.Status400BadRequest);
        return app;
    }
}
