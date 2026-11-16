using System.Text.Json;
using AndyTours.Application.Export;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AndyTours.Api;

public static class CourseExportEndpunkte
{
    private const long MaxRequestBytes = 128L * 1024 * 1024;

    public static IEndpointRouteBuilder MapCourseExport(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/course-export").WithTags("Course-Export");
        group.MapGet("/einstellungen", async (ICourseExport service, CancellationToken ct) =>
            Results.Ok(await service.LadenEinstellungen(ct)))
            .Produces<CourseExportEinstellungenAntwort>().ProducesProblem(400);

        group.MapPost("/", async (HttpContext context, ICourseExport service,
            IOptions<JsonOptions> jsonOptions, CancellationToken ct) =>
        {
            var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = MaxRequestBytes;
            if (context.Request.ContentLength > MaxRequestBytes)
                throw new BadHttpRequestException("Anfrage zu groß.", StatusCodes.Status413PayloadTooLarge);
            if (!context.Request.HasJsonContentType())
                return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "JSON-Inhalt erforderlich.");

            CourseExportAnfrage? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<CourseExportAnfrage>(
                    new BegrenzterRequestLesestream(context.Request.Body, MaxRequestBytes),
                    jsonOptions.Value.SerializerOptions, ct);
            }
            catch (JsonException)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Course-Export-Anfrage ungültig.");
            }
            if (request is null)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Course-Export-Anfrage ungültig.");
            return Results.Ok(await service.Erstellen(request, ct));
        })
        .Produces<CourseExportAntwort>().ProducesProblem(400).ProducesProblem(413).ProducesProblem(415);
        return app;
    }
}