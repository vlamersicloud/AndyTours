using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

public static class OverlayOrdnerEndpunkte
{
    public static IEndpointRouteBuilder MapOverlayOrdner(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/overlays/ordner").WithTags("Overlays");

        group.MapPost("/", async (
            OverlayOrdnerAnlegenAnfrage request, HttpContext context,
            IOverlayVerwaltung service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await service.OrdnerAnlegen(request, ct);
            context.Response.Headers.ETag = $"\"{result.Bestand.Version}\"";
            return Results.Ok(result);
        })
        .Produces<OverlayAnlegenAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        group.MapPut("/{id:long}", async (
            long id, OverlayOrdnerAendernAnfrage request, HttpContext context,
            IOverlayVerwaltung service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await service.OrdnerAendern(id, request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        })
        .Produces<OverlayBestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        group.MapDelete("/{id:long}", async (
            long id, HttpContext context, IOverlayVerwaltung service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.Headers.TryGetValue("If-Match", out var values) ||
                values.Count != 1 || values[0] is not { } tag || !IstVersion(tag))
                return Results.Problem(statusCode: 400, title: "Ungültige Overlay-Version.");

            var result = await service.OrdnerLoeschen(id, tag[1..^1], ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        })
        .Produces<OverlayBestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        return app;
    }

    private static bool IstVersion(string tag)
    {
        if (tag.Length != 66 || tag[0] != '"' || tag[^1] != '"') return false;
        foreach (char c in tag.AsSpan(1, 64))
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
