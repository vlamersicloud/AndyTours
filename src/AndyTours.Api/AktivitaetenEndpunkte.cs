using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace AndyTours.Api;

/// <summary>HTTP-Endpunkte zur versionierten Verwaltung des Aktivitätsbestands.</summary>
public static class AktivitaetenEndpunkte
{
    /// <summary>Registriert die Aktivitätenrouten unter /api/v1/aktivitaeten.</summary>
    public static IEndpointRouteBuilder MapAktivitaeten(this IEndpointRouteBuilder endpoints)
    {
        var gruppe = endpoints.MapGroup("/api/v1/aktivitaeten").WithTags("Aktivitäten")
            .WithDescription("Die Version bezieht sich auf die vollständige geordnete Aktivitätsliste. Schreibvorgänge sind Compare-and-Swap; Reihenfolgeänderungen enthalten ausschließlich eine exakte ID-Permutation.");

        gruppe.MapGet("/", async (IAktivitaetenVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
        {
            var antwort = await verwaltung.Laden(ct);
            ctx.Response.Headers.ETag = $"\"{antwort.Version}\"";
            return Results.Ok(antwort);
        }).Produces<AktivitaetenAntwort>().ProducesProblem(400);

        gruppe.MapPost("/", async (AktivitaetSpeichernAnfrage anfrage, IAktivitaetenVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.Anlegen(anfrage, ct), ctx))
            .WithDescription("Legt eine Aktivität an, wenn die übermittelte Familienversion der vollständigen geordneten Liste aktuell ist.")
            .Produces<AktivitaetenAntwort>().ProducesProblem(400).ProducesProblem(409);

        gruppe.MapPut("/{id:long}/name", async (long id, AktivitaetSpeichernAnfrage anfrage, IAktivitaetenVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.Umbenennen(id, anfrage, ct), ctx))
            .Produces<AktivitaetenAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        gruppe.MapDelete("/{id:long}", async (long id, [FromHeader(Name = "If-Match")] string version,
            IAktivitaetenVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "If-Match mit der Familienversion ist erforderlich.");
            var normalizedVersion = version.Length >= 2 && version[0] == '"' && version[^1] == '"'
                ? version[1..^1]
                : version;
            return MitVersion(await verwaltung.Loeschen(id, normalizedVersion, ct), ctx);
        }).WithDescription("If-Match enthält die unquotierte Familienversion der vollständigen geordneten Aktivitätsliste.")
            .Produces<AktivitaetenAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        gruppe.MapPut("/hauptaktivitaet", async (AktivitaetAuswahlAnfrage anfrage, IAktivitaetenVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.SetzeHauptaktivitaet(anfrage, ct), ctx))
            .Produces<AktivitaetenAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        gruppe.MapPut("/reihenfolge", async (AktivitaetenReihenfolgeAnfrage anfrage, IAktivitaetenVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.SetzeReihenfolge(anfrage, ct), ctx))
            .WithDescription("Setzt die Reihenfolge per exakter Permutation aller vorhandenen Aktivitäts-IDs.")
            .Produces<AktivitaetenAntwort>().ProducesProblem(400).ProducesProblem(409);

        gruppe.MapPost("/zusammenfuehren", async (AktivitaetenZusammenfuehrenAnfrage anfrage, IAktivitaetenVerwaltung verwaltung, HttpContext ctx, CancellationToken ct) =>
            MitVersion(await verwaltung.Zusammenfuehren(anfrage, ct), ctx))
            .Produces<AktivitaetenAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        return endpoints;
    }

    private static IResult MitVersion(AktivitaetenAntwort antwort, HttpContext ctx)
    {
        ctx.Response.Headers.ETag = $"\"{antwort.Version}\"";
        return Results.Ok(antwort);
    }
}
