using AndyTours.Application.Overlays;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

public static class OverlayAenderungEndpunkte
{
    public static IEndpointRouteBuilder MapOverlayAenderungen(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/overlays").WithTags("Overlays");

        group.MapPut("/{id:long}", async (
            long id, OverlayAendernAnfrage request, HttpContext context,
            IOverlayVerwaltung service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await service.OverlayAendern(id, request, ct);
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

            var result = await service.OverlayLoeschen(id, tag[1..^1], ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        })
        .Produces<OverlayBestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        group.MapPut("/sichtbarkeit", async (
            OverlaySichtbarkeitAnfrage request, HttpContext context,
            IOverlayVerwaltung service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await service.Sichtbarkeit(request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        })
        .Produces<OverlayBestandAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        group.MapPut("/farbe", async (
            OverlayGemeinsameFarbeAnfrage request, HttpContext context,
            IOverlayVerwaltung service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await service.GemeinsameFarbe(request, ct);
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
