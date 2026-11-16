using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AndyTours.Api;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Export;
using AndyTours.Application.Planung;
using AndyTours.Application.Garmin;
using AndyTours.Application.Overlays;
using AndyTours.Application.Import;
using AndyTours.Application.Touren;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) &&
    !builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren().Any())
    builder.WebHost.UseUrls("http://localhost:5000");
var dataDirectory = builder.Configuration["Backend:Datenordner"];
var accessToken = builder.Configuration["Backend:Zugriffstoken"];
var fitZoneId = builder.Configuration["Backend:FitZeitzone"];
TimeZoneInfo fitZone;
try { fitZone = string.IsNullOrWhiteSpace(fitZoneId) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(fitZoneId); }
catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new InvalidOperationException("Backend:FitZeitzone ist ungültig."); }
if (string.IsNullOrWhiteSpace(dataDirectory) || !Path.IsPathFullyQualified(dataDirectory))
    throw new InvalidOperationException("Backend:Datenordner muss absolut konfiguriert sein.");
if (string.IsNullOrWhiteSpace(accessToken) || Encoding.UTF8.GetByteCount(accessToken) < 32)
    throw new InvalidOperationException("Backend:Zugriffstoken muss mindestens 32 Bytes umfassen.");
Directory.CreateDirectory(dataDirectory);
var expectedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.RespectRequiredConstructorParameters = true);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddOpenApi("v1");
builder.Services.AddSingleton<ITourenSpeicher>(_ => new SqliteTourenSpeicher(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<ITourdetailQuelldaten>(_ => new SqliteTourdetailQuelldaten(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<ITourdetailLeser>(sp => new AndyTours.Import.Dateien.TourdetailLeser(sp.GetRequiredService<ITourdetailQuelldaten>(), dataDirectory));
builder.Services.AddSingleton<ICourseExportServerdaten>(_ => new SqliteCourseExportServerdaten(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<ICourseExport>(sp => new AndyTours.Import.Export.CourseExportDienst(sp.GetRequiredService<ICourseExportServerdaten>()));
builder.Services.AddSingleton<IPlanungsRouting>(sp => new AndyTours.Import.Routing.PlanungsRoutingDienst(sp.GetRequiredService<ICourseExportServerdaten>()));
builder.Services.AddSingleton<IPlanungsDateien, AndyTours.Import.Planung.PlanungsDateiDienst>();
builder.Services.AddSingleton<IPlanungsCourseExport, AndyTours.Import.Planung.PlanungsCourseExportDienst>();
builder.Services.AddSingleton<IPlanungsProfil>(_ => new SqlitePlanungsProfil(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IPlanungsStartQuelldaten>(_ => new SqlitePlanungsStartQuelldaten(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IPlanungsStartgebiet>(sp => new AndyTours.Import.Planung.PlanungsStartgebietDienst(sp.GetRequiredService<IPlanungsStartQuelldaten>(), dataDirectory));
builder.Services.AddSingleton<IRoutingKonfiguration>(_ => new SqliteRoutingKonfiguration(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IImportUploadSpeicher>(_ => new SqliteImportUploadSpeicher(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IFitUploads>(sp => new AndyTours.Import.Dateien.FitUploadDienst(
    sp.GetRequiredService<IImportUploadSpeicher>(), dataDirectory, fitZone));
builder.Services.AddSingleton<IAktivitaetsauswertung>(_ => new SqliteAktivitaetsauswertung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IPulsQuelldaten>(_ => new SqlitePulsQuelldaten(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IPulsAuswertung>(sp => new AndyTours.Import.Auswertung.PulsAuswertungDienst(
    sp.GetRequiredService<IPulsQuelldaten>(), dataDirectory));
builder.Services.AddSingleton<IStammdatenLeser>(_ => new SqliteStammdatenLeser(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IAktivitaetenVerwaltung>(_ => new SqliteAktivitaetenVerwaltung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IKategorienVerwaltung>(_ => new SqliteKategorienVerwaltung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IEquipmentsVerwaltung>(_ => new SqliteEquipmentsVerwaltung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IProfilVerwaltung>(_ => new SqliteProfilVerwaltung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<ITextlistenVerwaltung>(_ => new SqliteTextlistenVerwaltung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<ISaisonzieleVerwaltung>(_ => new SqliteSaisonzieleVerwaltung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IGarminEinstellungen>(_ => new SqliteGarminEinstellungen(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton(sp => new SqliteGarminImportJobs(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IGarminImportJobs>(sp => sp.GetRequiredService<SqliteGarminImportJobs>());
builder.Services.AddSingleton<IGarminImportArbeitspeicher>(sp => sp.GetRequiredService<SqliteGarminImportJobs>());
builder.Services.AddSingleton<IGarminImportPruefspeicher>(sp => sp.GetRequiredService<SqliteGarminImportJobs>());
builder.Services.AddSingleton<IGarminImportPruefung>(sp => new AndyTours.Import.Garmin.GarminImportPruefung(sp.GetRequiredService<IGarminImportPruefspeicher>(), dataDirectory, fitZone));
builder.Services.AddSingleton<IGarminImportServerdaten>(_ => new SqliteGarminImportServerdaten(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton(_ => new AndyTours.Import.Garmin.GarminImportArtefakte(dataDirectory, fitZone));
builder.Services.AddSingleton(sp => new AndyTours.Import.Garmin.GarminImportPaketWorker(
    sp.GetRequiredService<IGarminImportArbeitspeicher>(),
    sp.GetRequiredService<IGarminImportServerdaten>(),
    sp.GetRequiredService<AndyTours.Import.Garmin.GarminImportArtefakte>()));
builder.Services.AddHostedService<GarminImportDienst>();
builder.Services.AddSingleton<IWartungsverwaltung>(_ => new SqliteWartungsverwaltung(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton(sp => new SqliteOverlaySpeicher(Path.Combine(dataDirectory, "andytours.db")));
builder.Services.AddSingleton<IOverlayLeser>(sp => sp.GetRequiredService<SqliteOverlaySpeicher>());
builder.Services.AddSingleton<IOverlayVerwaltung>(sp => sp.GetRequiredService<SqliteOverlaySpeicher>());
builder.Services.AddSingleton<IOverlayGpxSpeicher>(sp => sp.GetRequiredService<SqliteOverlaySpeicher>());
builder.Services.AddSingleton<IOverlayGpxImport>(sp => new AndyTours.Import.Overlays.OverlayGpxImportDienst(sp.GetRequiredService<IOverlayGpxSpeicher>()));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    await Results.Problem(
        statusCode: context.Response.StatusCode,
        title: "Anfrage konnte nicht verarbeitet werden.",
        extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
        .ExecuteAsync(context);
});
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
app.MapAktivitaeten();
app.MapKategorien();
app.MapEquipments();
app.MapProfil();
app.MapTextlisten();
app.MapSaisonziele();
app.MapWartungen();
app.MapGarminEinstellungen();
app.MapGarminImport();
app.MapFitUploads();
app.MapTourdetail();
app.MapPuls();
app.MapOverlays();
app.MapOverlayOrdner();
app.MapOverlayAenderungen();
app.MapOverlayGpx();
app.MapCourseExport();
app.MapPlanungsRouting();
app.MapPlanungsDateien();
app.MapPlanungsCourse();
app.MapPlanungsProfil();
app.MapPlanungsStartgebiet();
app.MapRoutingKonfiguration();
app.MapGet("/api/v1/health", () => Results.Ok(new VerbindungAntwort("bereit", "v1")))
    .WithName("BackendStatus").WithTags("System").Produces<VerbindungAntwort>();
var touren = app.MapGroup("/api/v1/touren").WithTags("Touren");
touren.MapGet("/", async (int? start, int? anzahl, DateOnly? von, DateOnly? bis,
        long? aktivitaetId, long? kategorieId, ITourenSpeicher store, CancellationToken ct) =>
    Results.Ok(await store.ListeGefiltert(start ?? 0, anzahl ?? 50,
        new TourenListenfilter(von, bis, aktivitaetId, kategorieId), ct)))
    .WithDescription("Optionale inklusive Datumsgrenzen sowie Aktivitäts- und Kategorienfilter werden vor Gesamtanzahl und Paging angewendet.")
    .Produces<TourListeAntwort>().ProducesProblem(400);
touren.MapGet("/{id:long}", async (long id, ITourenSpeicher store, HttpContext ctx, CancellationToken ct) =>
{
    var result = await store.Lesen(id, ct);
    if (result is null) return Results.NotFound();
    ctx.Response.Headers.ETag = $"\"{result.Version}\"";
    return Results.Ok(result);
}).Produces<TourAntwort>().ProducesProblem(404).ProducesProblem(400);
touren.MapPost("/", async (TourWerte values, [FromHeader(Name = "Idempotency-Key")] Guid anfrageId, ITourenSpeicher store, HttpContext ctx, CancellationToken ct) =>
{
    if (anfrageId == Guid.Empty)
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
