using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AndyTours.Api;

public static class OverlayGpxEndpunkte
{
    public static IEndpointRouteBuilder MapOverlayGpx(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/v1/overlays/gpx/{anfrageId:guid}", async (
            Guid anfrageId, long? ordnerId, IOverlayGpxImport importer,
            HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (anfrageId == Guid.Empty)
                return Results.Problem(statusCode: 400, title: "Ungültige Overlay-GPX-Anfrage.");

            if (!context.Request.Headers.TryGetValue("If-Match", out var match) ||
                match.Count != 1 || match[0] is not { } tag || !IstVersion(tag))
                return Results.Problem(statusCode: 400, title: "Ungültige Overlay-Version.");

            var dispositionHeaders = context.Request.Headers.ContentDisposition;
            if (dispositionHeaders.Count != 1 ||
                !ContentDispositionHeaderValue.TryParse(dispositionHeaders[0], out var disposition))
                return Results.Problem(statusCode: 400, title: "Content-Disposition ungültig.");

            string? filename = disposition.FileNameStar.HasValue
                ? HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value
                : disposition.FileName.HasValue ? HeaderUtilities.RemoveQuotes(disposition.FileName).Value : null;
            if (filename is null)
                return Results.Problem(statusCode: 400, title: "Overlay-GPX-Dateiname fehlt.");

            if (context.Request.ContentType is { } contentType)
            {
                var mediaType = contentType.Split(';', 2)[0].Trim();
                if (!mediaType.Equals("application/gpx+xml", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                    return Results.Problem(statusCode: 415, title: "Nicht unterstützter Overlay-GPX-Inhaltstyp.");
            }

            var request = new OverlayGpxImportAnfrage(tag[1..^1], anfrageId, ordnerId, filename);
            try { request = OverlayGpxRegeln.Normalisieren(request); }
            catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Ungueltig)
            {
                return Results.Problem(statusCode: 400, title: "Ungültige Overlay-GPX-Anfrage.");
            }

            var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = OverlayGpxRegeln.MaxDateiBytes;
            if (context.Request.ContentLength > OverlayGpxRegeln.MaxDateiBytes)
                return Results.Problem(statusCode: 413, title: "Overlay-GPX-Datei überschreitet das Größenlimit.");

            var result = await importer.Importieren(request, context.Request.Body, ct);
            context.Response.Headers.ETag = $"\"{result.Bestand.Version}\"";
            return Results.Ok(result);
        })
        .WithTags("Overlays").Produces<OverlayAnlegenAntwort>()
        .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);

        return endpoints;
    }

    private static bool IstVersion(string tag)
    {
        if (tag.Length != 66 || tag[0] != '"' || tag[^1] != '"') return false;
        foreach (char character in tag.AsSpan(1, 64))
            if (!Uri.IsHexDigit(character)) return false;
        return true;
    }
}
