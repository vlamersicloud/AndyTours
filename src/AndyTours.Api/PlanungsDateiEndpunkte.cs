using System.Text.Json;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AndyTours.Api;

public static class PlanungsDateiEndpunkte
{
    private const long MaxBytes = PlanungsDateiRegeln.MaxDateiBytes;
    private static readonly string[] GpxTypes = ["application/gpx+xml", "application/xml", "text/xml", "application/octet-stream"];

    public static IEndpointRouteBuilder MapPlanungsDateien(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/planung/gpx/lesen", async (HttpContext context, IPlanungsDateien service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            await PrepareBody(context);
            if (!IsGpx(context.Request.ContentType)) return Results.Problem(statusCode: 415, title: "GPX-Inhalt erforderlich.");
            try { return Results.Ok(await service.Lesen(context.Request.Body, ct)); }
            catch (PlanungsDateiZuGrossException) { throw; }
        }).WithTags("Planung").Accepts<string>("application/gpx+xml").Produces<PlanungsStand>().ProducesProblem(400).ProducesProblem(413).ProducesProblem(415);

        app.MapPost("/api/v1/planung/gpx/schreiben", async (HttpContext context, IPlanungsDateien service, IOptions<JsonOptions> options, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            await PrepareBody(context);
            if (!context.Request.HasJsonContentType()) return Results.Problem(statusCode: 415, title: "JSON-Inhalt erforderlich.");
            PlanungsGpxExportAnfrage? request;
            try
            {
                using var limited = new BegrenzterRequestLesestream(context.Request.Body, MaxBytes);
                request = await JsonSerializer.DeserializeAsync<PlanungsGpxExportAnfrage>(limited, options.Value.SerializerOptions, ct);
            }
            catch (JsonException) { return Results.Problem(statusCode: 400, title: "Planungsdatei-Anfrage ungültig."); }
            if (request is null) return Results.Problem(statusCode: 400, title: "Planungsdatei-Anfrage ungültig.");
            var result = await service.Schreiben(request, ct);
            context.Response.Headers.ContentDisposition = $"attachment; filename=\"{result.Dateiname}\"";
            return Results.Bytes(result.Inhalt, "application/gpx+xml");
        }).WithTags("Planung").Accepts<PlanungsGpxExportAnfrage>("application/json").Produces<byte[]>(contentType: "application/gpx+xml").ProducesProblem(400).ProducesProblem(413).ProducesProblem(415);
        return app;
    }

    private static async Task PrepareBody(HttpContext context)
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = MaxBytes;
        if (context.Request.ContentLength > MaxBytes) throw new BadHttpRequestException("Anfrage zu groß.", StatusCodes.Status413PayloadTooLarge);
        await Task.CompletedTask;
    }
    private static bool IsGpx(string? value) => value is not null && GpxTypes.Contains(value.Split(';', 2)[0].Trim(), StringComparer.OrdinalIgnoreCase);
}
