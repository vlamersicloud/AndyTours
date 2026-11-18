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
            .WithDescription("Listet Metadaten vorhandener Legacy-SQLite-Sicherungen und vollständig validierter Vollarchive. Dieser List-Endpunkt gibt keine Dateipfade aus und erteilt selbst keine Download-, Lösch- oder Wiederherstellungsaktion; ein separater expliziter Download ist auf kataloggelistete ID plus erwarteten Hash begrenzt.")
            .Produces<SicherungListeAntwort>()
            .ProducesProblem(400)
            .ProducesProblem(503);
        return app;
    }
}
