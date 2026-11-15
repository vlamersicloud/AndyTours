using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AndyTours.Api;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) &&
    !builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren().Any())
    builder.WebHost.UseUrls("http://localhost:5000");
var dataDirectory = builder.Configuration["Backend:Datenordner"];
var accessToken = builder.Configuration["Backend:Zugriffstoken"];
if (string.IsNullOrWhiteSpace(dataDirectory) || !Path.IsPathFullyQualified(dataDirectory))
    throw new InvalidOperationException("Backend:Datenordner muss absolut konfiguriert sein.");
if (string.IsNullOrWhiteSpace(accessToken) || Encoding.UTF8.GetByteCount(accessToken) < 32)
    throw new InvalidOperationException("Backend:Zugriffstoken muss mindestens 32 Bytes umfassen.");
Directory.CreateDirectory(dataDirectory);
var expectedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddOpenApi("v1");
builder.Services.AddSingleton<ITourenSpeicher>(_ => new SqliteTourenSpeicher(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IAktivitaetsauswertung>(_ => new SqliteAktivitaetsauswertung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IStammdatenLeser>(_ => new SqliteStammdatenLeser(Path.Combine(dataDirectory, "andytours.db")));

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (httpContext, next) =>
{
    if (httpContext.Request.Path.Equals("/api/v1/health", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }
    var remote = httpContext.Connection.RemoteIpAddress;
    if (remote is null)
    {
        await Results.Problem(statusCode: 403, title: "Remotezugriff nicht zulässig.").ExecuteAsync(httpContext);
        return;
    }
    if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
    if (!IPAddress.IsLoopback(remote) && !httpContext.Request.IsHttps)
    {
        await Results.Problem(statusCode: 403, title: "Remotezugriff erfordert HTTPS.").ExecuteAsync(httpContext);
        return;
    }
    if (!AuthenticationHeaderValue.TryParse(httpContext.Request.Headers.Authorization, out var auth) ||
        !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrEmpty(auth.Parameter) ||
        !CryptographicOperations.FixedTimeEquals(expectedTokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(auth.Parameter))))
    {
        httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        await Results.Problem(statusCode: 401, title: "Authentifizierung erforderlich.").ExecuteAsync(httpContext);
        return;
    }
    await next();
});
app.MapOpenApi("/openapi/{documentName}.json");
app.MapGet("/api/v1/health", () => Results.Ok(new VerbindungAntwort("bereit", "v1")))
    .WithName("BackendStatus").WithTags("System").Produces<VerbindungAntwort>();
var touren = app.MapGroup("/api/v1/touren").WithTags("Touren");
touren.MapGet("/", async (int? start, int? anzahl, DateOnly? von, DateOnly? bis, ITourenSpeicher store, CancellationToken ct) =>
    Results.Ok(await store.Liste(start ?? 0, anzahl ?? 50, von, bis, ct)))
    .Produces<TourListeAntwort>().ProducesProblem(400);
touren.MapGet("/{id:long}", async (long id, ITourenSpeicher store, HttpContext ctx, CancellationToken ct) =>
{
    var result = await store.Lesen(id, ct);
    if (result is null) return Results.NotFound();
    ctx.Response.Headers.ETag = $"\"{result.Version}\"";
    return Results.Ok(result);
}).Produces<TourAntwort>().ProducesProblem(404).ProducesProblem(400);
touren.MapPost("/", async (TourWerte values, ITourenSpeicher store, HttpContext ctx, CancellationToken ct) =>
{
    var key = ctx.Request.Headers["Idempotency-Key"].ToString();
    if (!Guid.TryParseExact(key, "D", out var anfrageId) || anfrageId == Guid.Empty)
        return Results.Problem(statusCode: 400, title: "Gültiger Idempotency-Key-Header erforderlich.");

    var result = await store.AnlegenIdempotent(values, anfrageId, ct);
    ctx.Response.Headers.ETag = $"\"{result.Version}\"";
    return Results.Created($"/api/v1/touren/{result.Id}", result);
})
    .WithDescription("Erfordert einen stabilen Idempotency-Key-Header (GUID), damit identische Wiederholungen dieselbe Antwort liefern.")
    .Produces<TourAntwort>(201).ProducesProblem(400);
touren.MapPut("/{id:long}", async (long id, TourAendernAnfrage request, ITourenSpeicher store, HttpContext ctx, CancellationToken ct) =>
{
    var result = await store.Aendern(id, request, ct);
    ctx.Response.Headers.ETag = $"\"{result.Version}\"";
    return Results.Ok(result);
}).Produces<TourAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
touren.MapDelete("/{id:long}", async (long id, ITourenSpeicher store, HttpContext ctx, CancellationToken ct) =>
{
    var value = ctx.Request.Headers.IfMatch.ToString();
    if (string.IsNullOrWhiteSpace(value)) return Results.Problem(statusCode: 400, title: "If-Match-Version erforderlich.");
    await store.Loeschen(id, value.Trim().Trim('"'), ct);
    return Results.NoContent();
}).Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
app.MapGet("/api/v1/auswertung/aktivitaeten",
    async (DateOnly? von, DateOnly? bis, Auswertungsraster? raster, long? aktivitaetId, long? kategorieId,
        IAktivitaetsauswertung auswertung, CancellationToken ct) =>
        Results.Ok(await auswertung.Laden(new AktivitaetsauswertungAnfrage(
            von ?? new DateOnly(DateTime.Today.Year, 1, 1),
            bis ?? DateOnly.FromDateTime(DateTime.Today),
            raster ?? Auswertungsraster.Monat, aktivitaetId, kategorieId), ct)))
    .WithTags("Auswertung").Produces<AktivitaetsauswertungAntwort>().ProducesProblem(400);
app.MapGet("/api/v1/stammdaten", async (IStammdatenLeser stammdaten, CancellationToken ct) =>
    Results.Ok(await stammdaten.Laden(ct)))
    .WithTags("Stammdaten").Produces<StammdatenAntwort>();
app.Run();
public partial class Program;
