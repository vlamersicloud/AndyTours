using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AndyTours.Api;

public static class FitUploadEndpunkte
{
    public static IEndpointRouteBuilder MapFitUploads(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/fit-uploads").WithTags("FIT-Uploads");
        group.MapPut("/{uploadId:guid}", async (Guid uploadId, IFitUploads uploads, HttpContext context, CancellationToken ct) =>
        {
            var dispositionHeaders = context.Request.Headers.ContentDisposition;
            if (dispositionHeaders.Count != 1 ||
                !ContentDispositionHeaderValue.TryParse(dispositionHeaders[0], out var disposition))
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Content-Disposition ungültig.");

            string? filename = null;
            if (disposition.FileNameStar.HasValue)
                filename = HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value;
            else if (disposition.FileName.HasValue)
                filename = HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
            if (filename is null)
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Dateiname fehlt.");
            filename = FitUploadRegeln.Dateiname(filename);

            var bodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySize is { IsReadOnly: false }) bodySize.MaxRequestBodySize = FitUploadRegeln.MaxDateiBytes;
            if (context.Request.ContentLength > FitUploadRegeln.MaxDateiBytes)
                throw new FitUploadZuGrossException();

            var result = await uploads.Hochladen(uploadId, filename, context.Request.Body, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<FitUploadAntwort>().ProducesProblem(400).ProducesProblem(409).ProducesProblem(413);

        group.MapGet("/{uploadId:guid}", async (Guid uploadId, IFitUploads uploads, HttpContext context, CancellationToken ct) =>
        {
            var result = await uploads.Laden(uploadId, ct);
            if (result is null) return Results.NotFound();
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<FitUploadAntwort>().ProducesProblem(404).ProducesProblem(400);

        group.MapGet("/", async (int? start, int? anzahl, IFitUploads uploads, CancellationToken ct) =>
            Results.Ok(await uploads.Liste(start ?? 0, anzahl ?? 50, ct)))
            .Produces<FitUploadListeAntwort>().ProducesProblem(400);

        group.MapPost("/pruefung", async (FitUploadPruefanfrage request, IFitUploads uploads, HttpContext context, CancellationToken ct) =>
        {
            var result = await uploads.Pruefen(request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<FitUploadPruefungAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        group.MapPost("/uebernehmen", async (FitUploadUebernahmeAnfrage request, IFitUploads uploads, HttpContext context, CancellationToken ct) =>
        {
            var result = await uploads.Uebernehmen(request, ct);
            context.Response.Headers.ETag = $"\"{FitUploadVersionen.Bestand(result.Uploads)}\"";
            return Results.Ok(result);
        }).Produces<FitUploadUebernahmeAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }
}
