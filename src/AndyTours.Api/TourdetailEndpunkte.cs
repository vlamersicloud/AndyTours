using AndyTours.Application.Auswertung;
using AndyTours.Contracts.V1;
namespace AndyTours.Api;

public static class TourdetailEndpunkte
{
    public static IEndpointRouteBuilder MapTourdetail(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/tourdetails", async (TourdetailAnfrage request, ITourdetailLeser reader, HttpContext context, CancellationToken ct) =>
        {
            var result = await reader.Laden(request, ct);
            context.Response.Headers.ETag = $"{(char)34}{result.Version}{(char)34}";
            return Results.Ok(result);
        }).WithTags("Tourdetails").Produces<TourdetailAntwort>().ProducesProblem(400).ProducesProblem(404);
        return app;
    }
}
