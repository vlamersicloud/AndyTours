using System.Text.Json;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AndyTours.Api;

public static class PlanungsCourseEndpunkte
{
    private const long MaxRequestBytes = 128L * 1024 * 1024;

    public static IEndpointRouteBuilder MapPlanungsCourse(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/planung/course-export", async (HttpContext context, IPlanungsCourseExport service,
            IOptions<JsonOptions> jsonOptions, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var sizeFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = MaxRequestBytes;
            if (context.Request.ContentLength > MaxRequestBytes)
                throw new BadHttpRequestException("Anfrage zu groß.", StatusCodes.Status413PayloadTooLarge);
            if (!context.Request.HasJsonContentType())
                return Results.Problem(statusCode: 415, title: "JSON-Inhalt erforderlich.");
            PlanungsGpxExportAnfrage? request;
            try
            {
                using var body = new BegrenzterRequestLesestream(context.Request.Body, MaxRequestBytes);
                request = await JsonSerializer.DeserializeAsync<PlanungsGpxExportAnfrage>(body, jsonOptions.Value.SerializerOptions, ct);
            }
            catch (JsonException) { return Results.Problem(statusCode: 400, title: "Planungs-Course-Anfrage ungültig."); }
            if (request is null) return Results.Problem(statusCode: 400, title: "Planungs-Course-Anfrage ungültig.");
            return Results.Ok(await service.Erstellen(request, ct));
        })
        .WithTags("Planung")
        .Accepts<PlanungsGpxExportAnfrage>("application/json")
        .Produces<CourseExportAntwort>().ProducesProblem(400).ProducesProblem(413).ProducesProblem(415);
        return app;
    }
}
