using AndyTours.Application.Touren;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;

namespace AndyTours.Api;

/// <summary>Übersetzt bekannte Fach- und Persistenzfehler in sichere ProblemDetails.</summary>
public sealed class ProblemExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            UngueltigeTourException => (400, "Ungültige Tourdaten."),
            TourNichtGefundenException => (404, "Tour nicht gefunden."),
            TourVersionskonfliktException => (409, "Versionskonflikt."),
            SqliteException { SqliteErrorCode: 19 } => (400, "Ungültige Datenreferenz."),
            _ => (500, "Interner Serverfehler.")
        };
        await Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier })
            .ExecuteAsync(httpContext);
        return true;
    }
}
