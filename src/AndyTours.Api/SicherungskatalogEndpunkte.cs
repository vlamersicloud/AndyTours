namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;

public static class SicherungskatalogEndpunkte
{
    public static IEndpointRouteBuilder MapSicherungskatalog(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sicherungen").WithTags("Sicherungen");
        group.MapGet("/", async (ISicherungskatalog catalog, CancellationToken cancellationToken) =>
                Results.Ok(await catalog.Liste(cancellationToken)))
            .WithDescription("Listet Metadaten vorhandener Legacy-SQLite-Sicherungen. Der Endpunkt gibt keine Dateipfade aus und ermöglicht weder Download noch Löschen oder Wiederherstellen.")
            .Produces<SicherungListeAntwort>()
            .ProducesProblem(400);
        return app;
    }
}
