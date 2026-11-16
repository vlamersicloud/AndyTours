using AndyTours.Application.Auswertung;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

public static class PulsEndpunkte
{
    public static IEndpointRouteBuilder MapPuls(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auswertung/puls").WithTags("Pulsauswertung");
        group.MapGet("/referenz", async (HttpContext context, IPulsAuswertung service, CancellationToken ct) =>
        {
            var result = await service.LadenReferenz(ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        })
        .Produces<PulsReferenzAntwort>().ProducesProblem(400);

        group.MapPost("/referenz/aktualisieren",
            async (PulsReferenzAktualisierenAnfrage request, HttpContext context, IPulsAuswertung service, CancellationToken ct) =>
            {
                var result = await service.AktualisierenReferenz(request, ct);
                context.Response.Headers.ETag = $"\"{result.Version}\"";
                return Results.Ok(result);
            })
        .Produces<PulsReferenzAntwort>().ProducesProblem(400).ProducesProblem(409);

        group.MapPost("/", async (PulsAuswertungsAnfrage request, IPulsAuswertung service, CancellationToken ct) =>
            Results.Ok(await service.Auswerten(request, ct)))
        .Produces<PulsAuswertungAntwort>().ProducesProblem(400).ProducesProblem(409);

        return app;
    }
}