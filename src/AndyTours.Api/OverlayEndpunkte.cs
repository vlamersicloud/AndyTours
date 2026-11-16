using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using Microsoft.AspNetCore.Mvc;

namespace AndyTours.Api;

public static class OverlayEndpunkte
{
    public static IEndpointRouteBuilder MapOverlays(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/overlays").WithTags("Overlays");

        group.MapGet("/", async (HttpContext context, IOverlayLeser reader, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await reader.Laden(ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        })
        .Produces<Contracts.V1.OverlayBestandAntwort>()
        .ProducesProblem(400);

        group.MapGet("/{id:long}/inhalt", async (
            long id, HttpContext context, IOverlayLeser reader, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.Headers.TryGetValue("If-Match", out var ifMatch) ||
                ifMatch.Count != 1 || ifMatch[0] is not { } header || header.Length != 66 ||
                header[0] != '"' || header[^1] != '"' ||
                !IsVersion(header.AsSpan(1, 64)))
                return Results.Problem(statusCode: 400, title: "Ungültige Overlay-Version.");

            var expected = header[1..^1];
            try
            {
                var result = await reader.Inhalt(id, expected, ct);
                context.Response.Headers.ETag = $"\"{result.Version}\"";
                return Results.Ok(result);
            }
            catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.NichtGefunden)
            {
                return Results.Problem(statusCode: 404, title: "Overlay nicht gefunden.");
            }
            catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt)
            {
                return Results.Problem(statusCode: 409, title: "Overlay wurde geändert; Bestand bitte neu laden.");
            }
            catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Ungueltig)
            {
                return Results.Problem(statusCode: 400, title: "Ungültige Overlay-Anfrage.");
            }
        })
        .Produces<Contracts.V1.OverlayInhaltAntwort>()
        .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        return app;
    }

    private static bool IsVersion(ReadOnlySpan<char> value)
    {
        foreach (char ch in value)
            if (!Uri.IsHexDigit(ch)) return false;
        return true;
    }
}
