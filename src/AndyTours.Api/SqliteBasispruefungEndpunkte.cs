namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using Microsoft.AspNetCore.Builder;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class SqliteBasispruefungEndpunkte
{
    public static IEndpointRouteBuilder MapSqliteBasispruefung(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/sicherungen/sqlite-basispruefung", async (
            SqliteBasispruefungAnfrage request, ISqliteBasispruefung service, CancellationToken ct) =>
                Results.Ok(await service.Pruefen(request, ct).ConfigureAwait(false)))
            .WithMetadata(new ExklusiverDatenbestand())
            .WithTags("Sicherungen")
            .Produces<SqliteBasispruefungAntwort>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }
}
