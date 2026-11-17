namespace AndyTours.Api;

using AndyTours.Application.Stammdaten;
using AndyTours.Application.Tabellenlayout;
using AndyTours.Contracts.V1;

public static class EinheitentabellenlayoutEndpunkte
{
    public static IEndpointRouteBuilder MapEinheitentabellenlayout(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/einstellungen/einheitentabelle").WithTags("Einheitentabellenlayout");
        group.MapGet("/", async (IEinheitentabellenlayout store, CancellationToken ct) =>
        {
            try { return Results.Ok(await store.Laden(ct)); }
            catch (EinheitentabellenlayoutNichtVerfuegbarException)
            { return Results.Problem(statusCode: 503, title: "Tabellenlayout ist derzeit nicht verfügbar."); }
        }).Produces<EinheitentabellenlayoutAntwort>().ProducesProblem(503);
        group.MapPut("/", async (EinheitentabellenlayoutAnfrage request, IEinheitentabellenlayout store, CancellationToken ct) =>
        {
            try { return Results.Ok(await store.Speichern(request, ct)); }
            catch (StammdatenException e) when (e.Art == StammdatenFehlerart.Ungueltig)
            { return Results.Problem(statusCode: 400, title: "Tabellenlayout ungültig."); }
            catch (StammdatenException e) when (e.Art == StammdatenFehlerart.Konflikt)
            { return Results.Problem(statusCode: 409, title: "Tabellenlayout wurde zwischenzeitlich geändert."); }
            catch (EinheitentabellenlayoutNichtVerfuegbarException)
            { return Results.Problem(statusCode: 503, title: "Tabellenlayout ist derzeit nicht verfügbar."); }
        }).Accepts<EinheitentabellenlayoutAnfrage>("application/json")
          .Produces<EinheitentabellenlayoutAntwort>().ProducesProblem(400).ProducesProblem(409).ProducesProblem(503);
        return app;
    }
}
