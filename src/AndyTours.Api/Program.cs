using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
    !builder.Configuration.GetSection("Urls").GetChildren().Any())
{
    builder.WebHost.UseUrls("http://localhost:5000");
}
var datenordner = builder.Configuration["Backend:Datenordner"];
var token = builder.Configuration["Backend:Zugriffstoken"];
if (string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner))
    throw new InvalidOperationException("Backend:Datenordner muss als absoluter Pfad konfiguriert sein.");
if (string.IsNullOrWhiteSpace(token) || Encoding.UTF8.GetByteCount(token) < 32)
    throw new InvalidOperationException("Backend:Zugriffstoken muss mindestens 32 Bytes umfassen.");
Directory.CreateDirectory(datenordner);
var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddSingleton<ITourenSpeicher>(_ => new SqliteTourenSpeicher(Path.Combine(datenordner, "andytours.db")));

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/api/v1/health", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }
    if (context.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip) &&
        !context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var auth) ||
        !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrEmpty(auth.Parameter))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(auth.Parameter));
    if (!CryptographicOperations.FixedTimeEquals(tokenHash, suppliedHash))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    await next();
});

app.MapGet("/api/v1/health", () => Results.Ok(new VerbindungAntwort("bereit", "v1")))
    .WithName("BackendStatus").WithTags("System").Produces<VerbindungAntwort>();

var touren = app.MapGroup("/api/v1/touren").WithTags("Touren");
touren.MapGet("/", async (int? start, int? anzahl, DateOnly? von, DateOnly? bis, ITourenSpeicher speicher, CancellationToken ct) =>
{
    var liste = await speicher.Liste(start ?? 0, anzahl ?? 50, von, bis, ct);
    return Results.Ok(liste);
});
touren.MapGet("/{id:long}", async (long id, ITourenSpeicher speicher, HttpContext context, CancellationToken ct) =>
{
    var antwort = await speicher.Lesen(id, ct);
    if (antwort is null) return Results.NotFound();
    context.Response.Headers.ETag = $"\"{antwort.Version}\"";
    return Results.Ok(antwort);
});
touren.MapPost("/", async (TourWerte werte, ITourenSpeicher speicher, HttpContext context, CancellationToken ct) =>
{
    var antwort = await speicher.Anlegen(werte, ct);
    context.Response.Headers.ETag = $"\"{antwort.Version}\"";
    return Results.Created($"/api/v1/touren/{antwort.Id}", antwort);
});
touren.MapPut("/{id:long}", async (long id, TourAendernAnfrage anfrage, ITourenSpeicher speicher, HttpContext context, CancellationToken ct) =>
{
    var antwort = await speicher.Aendern(id, anfrage, ct);
    context.Response.Headers.ETag = $"\"{antwort.Version}\"";
    return Results.Ok(antwort);
});
touren.MapDelete("/{id:long}", async (long id, ITourenSpeicher speicher, HttpContext context, CancellationToken ct) =>
{
    var header = context.Request.Headers.IfMatch.ToString();
    if (string.IsNullOrWhiteSpace(header)) return Results.Problem(statusCode: 400, title: "If-Match-Version erforderlich.");
    var version = header.Trim().Trim('"');
    await speicher.Loeschen(id, version, ct);
    return Results.NoContent();
});
app.Run();

/// <summary>Übersetzt bekannte Touren-/SQLite-Fehler in sichere ProblemDetails-Antworten.</summary>
public sealed class ProblemExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            UngueltigeTourException => (400, "Ungültige Tourdaten."),
            TourNichtGefundenException => (404, "Tour nicht gefunden."),
            TourVersionskonfliktException => (409, "Versionskonflikt."),
            SqliteException { SqliteErrorCode: 19 } => (400, "Ungültige Datenreferenz."),
            _ => (500, "Interner Serverfehler.")
        };
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}

/// <summary>Für Integrationstests zugänglicher Einstiegspunkt des API-Hosts.</summary>
public partial class Program;
