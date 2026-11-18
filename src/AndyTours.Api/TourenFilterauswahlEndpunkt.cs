using System.Text.Json;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AndyTours.Api;

/// <summary>Expliziter, fail-closed Endpunkt für eine begrenzte Auswahl aller Filtertreffer.</summary>
public static class TourenFilterauswahlEndpunkt
{
    public static IEndpointRouteBuilder MapTourenFilterauswahl(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/touren/auswahl/filtertreffer",
            async (ITourenFilterauswahl leser, IConfiguration configuration,
                IOptions<JsonOptions> json, HttpContext context, CancellationToken ct) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                if (!TourenFilterauswahlGrenzen.TryParse(
                        configuration["Backend:Tourenauswahl:MaxTouren"],
                        configuration["Backend:Tourenauswahl:MaxAntwortBytes"],
                        out var maxTouren, out var maxAntwortBytes))
                    return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Filtertreffer-Auswahl ist nicht verfügbar.",
                        extensions: new Dictionary<string, object?> { ["code"] = "touren.filterauswahl_nicht_verfuegbar" });
                if (!context.Request.HasJsonContentType())
                    return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                        title: "JSON-Inhalt erforderlich.");

                TourenFilterauswahlAnfrage? filter;
                try
                {
                    filter = await JsonSerializer.DeserializeAsync<TourenFilterauswahlAnfrage>(
                        context.Request.Body, json.Value.SerializerOptions, ct).ConfigureAwait(false);
                }
                catch (JsonException)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Filtertreffer-Auswahl ist ungültig.");
                }
                if (filter is null)
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Filtertreffer-Auswahl ist ungültig.");

                try
                {
                    var touren = await leser.AlleTreffer(filter, maxTouren, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    var antwort = new TourenFilterauswahlAntwort(filter, touren);
                    var serialisiert = TourenFilterauswahlAntwortSerialisierer.TrySerialisieren(
                        antwort, json.Value.SerializerOptions, maxAntwortBytes, out var payload, ct);
                    ct.ThrowIfCancellationRequested();
                    return serialisiert
                        ? Results.Bytes(payload, "application/json")
                        : ErgebnisseZuGross();
                }
                catch (TourenFilterauswahlLimitException)
                {
                    return ErgebnisseZuGross();
                }
                catch (UngueltigeTourException)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Filtertreffer-Auswahl ist ungültig.");
                }
            })
            .WithTags("Touren")
            .Accepts<TourenFilterauswahlAnfrage>("application/json")
            .Produces<TourenFilterauswahlAntwort>()
            .ProducesProblem(400).ProducesProblem(413).ProducesProblem(503);
        return app;
    }

    private static IResult ErgebnisseZuGross() =>
        Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Filtertreffer-Auswahl überschreitet die konfigurierte Grenze.",
            extensions: new Dictionary<string, object?> { ["code"] = "touren.filterauswahl_zu_gross" });
}
