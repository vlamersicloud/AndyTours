namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

/// <summary>Streams a catalog-identified backup only after same-handle size and SHA-256 verification.</summary>
public static class SicherungsdownloadEndpunkt
{
    public static IEndpointRouteBuilder MapSicherungsdownload(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/sicherungen/download", async (
            SicherungsdownloadAnfrage request,
            ISicherungskatalog catalog,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (request is null)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Sicherungsdownloadanforderung ungültig.");
            if (!SicherungsdownloadGrenzen.TryParse(
                    configuration["Backend:Sicherung:MaxDownloadBytes"], out var maximum))
                return NichtVerfuegbar();
            var validated = WiederherstellungsvorschauRegeln.PruefeAnfrage(
                new WiederherstellungsvorschauAnfrage(request.SicherungsId, request.ErwarteterInhaltHash));
            var checkedRequest = new SicherungsdownloadAnfrage(validated.SicherungsId, validated.ErwarteterInhaltHash);
            if (catalog is not ISicherungsdownload download)
                return NichtVerfuegbar();

            var listing = await catalog.Liste(ct).ConfigureAwait(false);
            var matches = listing.Eintraege.Where(x => x.Id == checkedRequest.SicherungsId).Take(2).ToArray();
            if (matches.Length == 0)
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Sicherungsquelle nicht gefunden.");
            if (matches.Length != 1)
                return NichtVerfuegbar();
            var entry = matches[0];
            if (!WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(entry))
                return NichtVerfuegbar();
            if (!string.Equals(entry.InhaltHash, checkedRequest.ErwarteterInhaltHash, StringComparison.OrdinalIgnoreCase))
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Sicherungsquelle stimmt nicht mit dem erwarteten Inhalt überein.");
            if (entry.Byteanzahl > maximum)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Sicherung überschreitet die konfigurierte Downloadgrenze.",
                    extensions: new Dictionary<string, object?> { ["code"] = "sicherungsdownload.zu_gross" });

            var source = await download.Oeffnen(checkedRequest, maximum, ct).ConfigureAwait(false);
            var metadata = source.Metadaten;
            if (metadata.Id != checkedRequest.SicherungsId ||
                !string.Equals(metadata.InhaltHash, checkedRequest.ErwarteterInhaltHash, StringComparison.OrdinalIgnoreCase) ||
                metadata.Byteanzahl < 0 || metadata.Byteanzahl > maximum ||
                !WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(metadata))
            {
                await source.DisposeAsync().ConfigureAwait(false);
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Sicherungsquelle stimmt nicht mit dem Katalog überein.");
            }

            var extension = metadata.Art == Sicherungsart.LegacySqlite ? ".db" : ".zip";
            var filename = $"AndyTours-Sicherung-{metadata.Id:N}{extension}";
            return new OwnedSicherungsdownloadResult(source, metadata, filename);
        })
        .WithTags("Sicherungen")
        .WithDescription("Liefert ausschließlich eine im Sicherungskatalog gelistete Sicherung nach Prüfung von ID, erwartetem SHA-256 und vollständiger Dateigröße; kein Restore oder Löschen.")
        .Accepts<SicherungsdownloadAnfrage>("application/json")
        .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
        .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(503);
        return endpoints;
    }

    private static IResult NichtVerfuegbar() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Sicherungsdownload ist nicht verfügbar.",
        extensions: new Dictionary<string, object?> { ["code"] = "sicherungsdownload.nicht_verfuegbar" });

    internal sealed class OwnedSicherungsdownloadResult(
        SicherungsdownloadQuelle source, SicherungAntwort metadata, string filename) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            try
            {
                httpContext.RequestAborted.ThrowIfCancellationRequested();
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/octet-stream";
                httpContext.Response.Headers.CacheControl = "no-store";
                httpContext.Response.ContentLength = metadata.Byteanzahl;
                httpContext.Response.Headers.ETag = string.Concat("\"", metadata.InhaltHash, "\"");
                httpContext.Response.Headers.ContentDisposition =
                    new System.Net.Mime.ContentDisposition { FileName = filename, Inline = false }.ToString();
                await CopyExact(source.Inhalt, httpContext.Response.Body, metadata.Byteanzahl,
                    httpContext.RequestAborted).ConfigureAwait(false);
            }
            finally
            {
                await source.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task CopyExact(Stream source, Stream destination, long expectedBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        var remaining = expectedBytes;
        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), ct).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Sicherungsquelle wurde während des Downloads verkürzt.");
            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            remaining -= read;
        }
        ct.ThrowIfCancellationRequested();
        if (await source.ReadAsync(buffer.AsMemory(0, 1), ct).ConfigureAwait(false) != 0)
            throw new InvalidDataException("Sicherungsquelle wurde während des Downloads verlängert.");
    }
}
