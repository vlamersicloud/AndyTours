namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

public static class SicherungskonfigurationEndpunkte
{
    public static IEndpointRouteBuilder MapSicherungskonfiguration(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/einstellungen/sicherung").WithTags("Sicherungskonfiguration");
        group.MapGet("/", async (ISicherungskonfiguration service, CancellationToken ct) =>
            Results.Ok(await service.Laden(ct)))
            .Produces<SicherungskonfigurationAntwort>();

        group.MapPut("/", async (SicherungskonfigurationAnfrage request, ISicherungskonfiguration service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.Speichern(request, ct)); }
            catch (StammdatenException e) when (e.Art == StammdatenFehlerart.Ungueltig)
            { return Results.Problem(statusCode: 400, title: "Sicherungskonfiguration ungültig."); }
            catch (StammdatenException e) when (e.Art == StammdatenFehlerart.Konflikt)
            { return Results.Problem(statusCode: 409, title: "Sicherungskonfiguration wurde zwischenzeitlich geändert."); }
        })
            .Accepts<SicherungskonfigurationAnfrage>("application/json")
            .Produces<SicherungskonfigurationAntwort>().ProducesProblem(400).ProducesProblem(409);
        return app;
    }
}
