using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

public static class PlanungsProfilEndpunkte
{
    public static IEndpointRouteBuilder MapPlanungsProfil(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/planung/profil", async (IPlanungsProfil profil, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await profil.Laden(cancellationToken);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).WithTags("Planung").Produces<PlanungsProfilAntwort>();
        app.MapPut("/api/v1/planung/profil", async (PlanungsProfilSpeichernAnfrage anfrage, IPlanungsProfil profil,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                var result = await profil.Speichern(anfrage, cancellationToken);
                context.Response.Headers.ETag = $"\"{result.Version}\"";
                return Results.Ok(result);
            }
            catch (StammdatenException exception) when (exception.Art == StammdatenFehlerart.Ungueltig)
            {
                return Results.Problem(statusCode: 400, title: "Planungsprofil ungültig.");
            }
            catch (StammdatenException exception) when (exception.Art == StammdatenFehlerart.Konflikt)
            {
                return Results.Problem(statusCode: 409, title: "Planungsprofil wurde zwischenzeitlich geändert.");
            }
        }).WithTags("Planung").Accepts<PlanungsProfilSpeichernAnfrage>("application/json")
          .Produces<PlanungsProfilAntwort>().ProducesProblem(400).ProducesProblem(409);
        return app;
    }
}
