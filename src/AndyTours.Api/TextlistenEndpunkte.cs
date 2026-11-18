using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

/// <summary>Endpunkte für die geschlossene Menge unterstützter Textlisten.</summary>
public static class TextlistenEndpunkte
{
    /// <summary>Registriert typisierte Textlistenrouten unter /api/v1/textlisten/{art}.</summary>
    public static IEndpointRouteBuilder MapTextlisten(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/textlisten/{art}").WithTags("Textlisten");
        group.MapGet("", async (Textlistenart art, ITextlistenVerwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.Laden(art, ct), context))
            .Produces<TextlisteAntwort>().ProducesProblem(400).ProducesProblem(404);
        group.MapGet("/nutzung", async (Textlistenart art, string version, string name, ITextlistenVerwaltung service, CancellationToken ct) =>
            Results.Ok(await service.NutzungLesen(art, version, name, ct)))
            .Produces<TextlistenNutzungAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/eintraege", async (Textlistenart art, TextlisteneintragAnfrage request, ITextlistenVerwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.Anlegen(art, request, ct), context))
            .Produces<TextlisteAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/name", async (Textlistenart art, TextlisteUmbenennenAnfrage request, ITextlistenVerwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.Umbenennen(art, request, ct), context))
            .Produces<TextlisteAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/eintraege/loeschen", async (Textlistenart art, TextlisteneintragAnfrage request, ITextlistenVerwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.Loeschen(art, request, ct), context))
            .Produces<TextlisteAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/reihenfolge", async (Textlistenart art, TextlisteReihenfolgeAnfrage request, ITextlistenVerwaltung service, HttpContext context, CancellationToken ct) =>
            WithVersion(await service.SetzeReihenfolge(art, request, ct), context))
            .Produces<TextlisteAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }

    private static IResult WithVersion(TextlisteAntwort response, HttpContext context)
    {
        context.Response.Headers.ETag = $"\"{response.Version}\"";
        return Results.Ok(response);
    }
}
