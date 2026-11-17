namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class SicherungsarchivErstellungEndpunkte
{
    public static IEndpointRouteBuilder MapSicherungsarchivErstellung(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sicherungen").WithTags("Sicherungen");
        group.MapPost("/", async (
                SicherungsarchivErstellungsanfrage request,
                SicherungsarchivBereitstellung bereitstellung,
                CancellationToken cancellationToken) =>
            {
                if (request is null || request.VorgangsId == Guid.Empty)
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Sicherungsanfrage ist ungültig.");

                var creator = bereitstellung.Ermittle(cancellationToken);
                if (creator is null)
                    return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Vollarchiv-Erstellung ist nicht verfügbar.");

                var response = await creator.Erstellen(request, cancellationToken);
                return Results.Ok(response);
            })
            .WithMetadata(new ExklusiverDatenbestand())
            .WithDescription("Erstellt oder liefert idempotent ein geprüftes Vollarchiv. Keine Pfade oder serverseitigen Limits werden vom Client angenommen.")
            .Produces<SicherungsarchivErstellungsantwort>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
