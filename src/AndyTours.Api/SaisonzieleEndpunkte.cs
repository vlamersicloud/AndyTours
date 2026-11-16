using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

/// <summary>HTTP-Endpunkte für den versionierten Saisonzielebestand.</summary>
public static class SaisonzieleEndpunkte
{
    /// <summary>Registriert Laden und Speichern unter /api/v1/saisonziele.</summary>
    public static IEndpointRouteBuilder MapSaisonziele(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/saisonziele").WithTags("Saisonziele");
        group.MapGet("/", async (ISaisonzieleVerwaltung service, HttpContext context, CancellationToken ct) =>
        {
            var result = await service.Laden(ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<SaisonzieleAntwort>().ProducesProblem(400).ProducesProblem(404);
        group.MapPut("/", async (SaisonzieleSpeichernAnfrage request, ISaisonzieleVerwaltung service, HttpContext context, CancellationToken ct) =>
        {
            var result = await service.Speichern(request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<SaisonzieleAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }
}
