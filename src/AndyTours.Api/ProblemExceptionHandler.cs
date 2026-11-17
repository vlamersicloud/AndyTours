using AndyTours.Application.Touren;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Import;
using AndyTours.Application.Overlays;
using AndyTours.Application.Planung;
using AndyTours.Application.Sicherung;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace AndyTours.Api;

/// <summary>Übersetzt bekannte Fach- und Persistenzfehler in sichere ProblemDetails.</summary>
public sealed class ProblemExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is FitUploadZuGrossException or OverlayGpxZuGrossException or PlanungsDateiZuGrossException or BadHttpRequestException { StatusCode: 413 })
        {
            await Results.Problem(statusCode: 413, title: "Anfrage überschreitet die zulässige Größe.",
                extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier, ["code"] = exception is OverlayGpxZuGrossException ? "overlay_gpx.zu_gross" : "fit_upload.zu_gross" })
                .ExecuteAsync(httpContext);
            return true;
        }

        if (exception is SicherungskatalogNichtVerfuegbarException)
        {
            await Results.Problem(statusCode: 503,
                title: "Vollarchiv-Katalog ist derzeit nicht verfügbar.",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier,
                    ["code"] = "sicherungskatalog.nicht_verfuegbar"
                })
                .ExecuteAsync(httpContext);
            return true;
        }

        if (exception is StammdatenException stammdaten)
        {
            var (status, title, code) = stammdaten.Art switch
            {
                StammdatenFehlerart.Ungueltig => (400, "Ungültige Stammdatenanforderung.", "stammdaten.ungueltig"),
                StammdatenFehlerart.NichtGefunden => (404, "Stammdaten nicht gefunden.", "stammdaten.nichtgefunden"),
                StammdatenFehlerart.Konflikt => (409, "Stammdatenkonflikt.", "stammdaten.konflikt"),
                _ => (500, "Interner Serverfehler.", (string?)null)
            };
            await Results.Problem(statusCode: status, title: title, detail: status == 500 ? null : stammdaten.Message,
                extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier, ["code"] = code })
                .ExecuteAsync(httpContext);
            return true;
        }

        var (legacyStatus, legacyTitle) = exception switch
        {
            UngueltigeTourException => (400, "Ungültige Tourdaten."),
            TourNichtGefundenException => (404, "Tour nicht gefunden."),
            TourVersionskonfliktException => (409, "Versionskonflikt."),
            SqliteException { SqliteErrorCode: 19 } => (400, "Ungültige Datenreferenz."),
            _ => (500, "Interner Serverfehler.")
        };
        await Results.Problem(statusCode: legacyStatus, title: legacyTitle,
            extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier })
            .ExecuteAsync(httpContext);
        return true;
    }
}
