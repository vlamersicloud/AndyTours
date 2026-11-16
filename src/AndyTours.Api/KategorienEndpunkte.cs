using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace AndyTours.Api;

/// <summary>HTTP-Endpunkte zur versionierten Kategorienverwaltung.</summary>
public static class KategorienEndpunkte
{
    /// <summary>Registriert die Kategorienrouten unter /api/v1/kategorien.</summary>
    public static IEndpointRouteBuilder MapKategorien(this IEndpointRouteBuilder endpoints)
    {
        var gruppe = endpoints.MapGroup("/api/v1/kategorien").WithTags("Kategorien")
            .WithDescription("Familienversion über die vollständige nach ID geordnete Kategorienliste. Änderungen sind Compare-and-Swap.");

        gruppe.MapGet("/", async (IKategorienVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
        {
            var antwort = await verwaltung.Laden(ct);
            ctx.Response.Headers.ETag = $"\"{antwort.Version}\"";
            return Results.Ok(antwort);
        }).Produces<KategorienAntwort>().ProducesProblem(400);

        gruppe.MapPost("/", async (KategorieSpeichernAnfrage anfrage, IKategorienVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.Anlegen(anfrage, ct), ctx))
            .Produces<KategorienAntwort>().ProducesProblem(400).ProducesProblem(409);

        gruppe.MapPut("/{id:long}/name", async (long id, KategorieSpeichernAnfrage anfrage,
            IKategorienVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.Umbenennen(id, anfrage, ct), ctx))
            .Produces<KategorienAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        gruppe.MapDelete("/{id:long}", async (long id, [FromHeader(Name = "If-Match")] string version,
            IKategorienVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "If-Match mit der Familienversion ist erforderlich.");
            var normalized = version.Length >= 2 && version[0] == '"' && version[^1] == '"'
                ? version[1..^1]
                : version;
            return MitVersion(await verwaltung.Loeschen(id, normalized, ct), ctx);
        }).WithDescription("If-Match ist erforderlich und akzeptiert die Familienversion als unquotierten Hash oder als quotierten starken ETag; Wildcards, schwache und Listen-Tags sind ungültig.")
            .Produces<KategorienAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        gruppe.MapPut("/standard", async (KategorieStandardAnfrage anfrage, IKategorienVerwaltung verwaltung,
            HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.SetzeStandard(anfrage, ct), ctx))
            .Produces<KategorienAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        gruppe.MapPost("/zusammenfuehren", async (KategorienZusammenfuehrenAnfrage anfrage,
            IKategorienVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.Zusammenfuehren(anfrage, ct), ctx))
            .Produces<KategorienAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        return endpoints;
    }

    private static IResult MitVersion(KategorienAntwort antwort, HttpContext ctx)
    {
        ctx.Response.Headers.ETag = $"\"{antwort.Version}\"";
        return Results.Ok(antwort);
    }
}
