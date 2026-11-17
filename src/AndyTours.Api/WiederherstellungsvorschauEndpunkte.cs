namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>Maps the non-destructive restore-preview API; no restore/apply operation is exposed.</summary>
public static class WiederherstellungsvorschauEndpunkte
{
    public static IEndpointRouteBuilder MapWiederherstellungsvorschau(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sicherungen").WithTags("Sicherungen");
        group.MapPost("/wiederherstellungsvorschau/", async (
                WiederherstellungsvorschauAnfrage request,
                IWiederherstellungsvorschau preview,
                CancellationToken cancellationToken) =>
            Results.Ok(await preview.Pruefen(request, cancellationToken).ConfigureAwait(false)))
            .WithDescription("Liefert ausschließlich nichtdestruktive Vorschau-Metadaten. Keine SQLite-Prüfung, keine Freigabe, kein Anwenden, keine Extraktion und keine Schreibzugriffe.")
            .Produces<WiederherstellungsvorschauAntwort>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }
}
