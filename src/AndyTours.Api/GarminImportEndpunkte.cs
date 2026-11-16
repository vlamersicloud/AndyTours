using AndyTours.Application.Garmin;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace AndyTours.Api;

public static class GarminImportEndpunkte
{
    public static IEndpointRouteBuilder MapGarminImport(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/garmin/importe").WithTags("Garmin-Import");

        group.MapPost("/", async (GarminImportStartAnfrage request, IGarminImportJobs jobs, HttpContext context, CancellationToken ct) =>
        {
            var result = await jobs.Starten(request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Accepted($"/api/v1/garmin/importe/{result.JobId:D}", result);
        }).Produces<GarminImportJobAntwort>(202).ProducesProblem(400).ProducesProblem(409);

        group.MapGet("/aktuell", async (IGarminImportJobs jobs, HttpContext context, CancellationToken ct) =>
        {
            var result = await jobs.AktuellenLaden(ct);
            if (result is null) return Results.NoContent();
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<GarminImportJobAntwort>().Produces(204).ProducesProblem(400);

        group.MapGet("/{jobId:guid}", async (Guid jobId, IGarminImportJobs jobs, HttpContext context, CancellationToken ct) =>
        {
            var result = await jobs.Laden(jobId, ct);
            if (result is null) return Results.NotFound();
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<GarminImportJobAntwort>().ProducesProblem(400).ProducesProblem(404);

        group.MapGet("/{jobId:guid}/kandidaten", async (Guid jobId, int? start, int? anzahl, IGarminImportJobs jobs, CancellationToken ct) =>
            Results.Ok(await jobs.Kandidaten(jobId, start ?? 0, anzahl ?? 50, ct)))
            .Produces<GarminImportKandidatenAntwort>().ProducesProblem(400).ProducesProblem(404);

        group.MapPost("/{jobId:guid}/fortsetzen", async (Guid jobId, GarminImportSteuerAnfrage request, IGarminImportJobs jobs, HttpContext context, CancellationToken ct) =>
        {
            var result = await jobs.Fortsetzen(jobId, request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Accepted($"/api/v1/garmin/importe/{result.JobId:D}", result);
        }).Produces<GarminImportJobAntwort>(202).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        group.MapPost("/{jobId:guid}/abbrechen", async (Guid jobId, GarminImportSteuerAnfrage request, IGarminImportJobs jobs, HttpContext context, CancellationToken ct) =>
        {
            var result = await jobs.Abbrechen(jobId, request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<GarminImportJobAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        return endpoints;
    }
}
