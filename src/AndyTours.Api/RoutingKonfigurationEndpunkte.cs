using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

public static class RoutingKonfigurationEndpunkte
{
    public static IEndpointRouteBuilder MapRoutingKonfiguration(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/routing-konfiguration").WithTags("Routingkonfiguration");
        group.MapGet("/", async (IRoutingKonfiguration service, HttpContext context, CancellationToken ct) =>
            Success(context, await service.Laden(ct)))
            .Produces<RoutingKonfigurationAntwort>();
        group.MapPut("/dienst", async (RoutingDienstAnfrage request, IRoutingKonfiguration service, HttpContext context, CancellationToken ct) =>
            await Execute(context, () => service.DienstSpeichern(request, ct)))
            .Accepts<RoutingDienstAnfrage>("application/json").Produces<RoutingKonfigurationAntwort>().ProducesProblem(400).ProducesProblem(409);
        group.MapPut("/brouter-adresse", async (RoutingAdresseAnfrage request, IRoutingKonfiguration service, HttpContext context, CancellationToken ct) =>
            await Execute(context, () => service.AdresseSpeichern(request, ct)))
            .Accepts<RoutingAdresseAnfrage>("application/json").Produces<RoutingKonfigurationAntwort>().ProducesProblem(400).ProducesProblem(409);
        group.MapPost("/brouter-adresse/zuruecksetzen", async (RoutingVersionAnfrage request, IRoutingKonfiguration service, HttpContext context, CancellationToken ct) =>
            await Execute(context, () => service.AdresseZuruecksetzen(request, ct)))
            .Accepts<RoutingVersionAnfrage>("application/json").Produces<RoutingKonfigurationAntwort>().ProducesProblem(400).ProducesProblem(409);
        group.MapPut("/ors-schluessel", async (RoutingOrsSchluesselAnfrage request, IRoutingKonfiguration service, HttpContext context, CancellationToken ct) =>
            await Execute(context, () => service.OrsKeySpeichern(request, ct)))
            .Accepts<RoutingOrsSchluesselAnfrage>("application/json").Produces<RoutingKonfigurationAntwort>().ProducesProblem(400).ProducesProblem(409);
        group.MapPost("/ors-schluessel/entfernen", async (RoutingVersionAnfrage request, IRoutingKonfiguration service, HttpContext context, CancellationToken ct) =>
            await Execute(context, () => service.OrsKeyEntfernen(request, ct)))
            .Accepts<RoutingVersionAnfrage>("application/json").Produces<RoutingKonfigurationAntwort>().ProducesProblem(400).ProducesProblem(409);
        return app;
    }

    private static IResult Success(HttpContext context, RoutingKonfigurationAntwort response)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ETag = string.Concat("\"", response.Version, "\"");
        return Results.Ok(response);
    }

    private static async Task<IResult> Execute(HttpContext context, Func<Task<RoutingKonfigurationAntwort>> action)
    {
        try { return Success(context, await action()); }
        catch (StammdatenException e) when (e.Art == StammdatenFehlerart.Ungueltig)
        { return Results.Problem(statusCode: 400, title: "Routingkonfiguration ungültig."); }
        catch (StammdatenException e) when (e.Art == StammdatenFehlerart.Konflikt)
        { return Results.Problem(statusCode: 409, title: "Routingkonfiguration wurde zwischenzeitlich geändert."); }
    }
}
