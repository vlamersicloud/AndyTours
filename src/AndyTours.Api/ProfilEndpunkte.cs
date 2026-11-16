using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Api;

/// <summary>HTTP-Endpunkte für das versionierte persönliche Profil.</summary>
public static class ProfilEndpunkte
{
    /// <summary>Registriert GET und PUT unter /api/v1/profil.</summary>
    public static IEndpointRouteBuilder MapProfil(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profil").WithTags("Profil")
            .WithDescription("Persönliches Profil des ersten Fahrers; Compare-and-swap über Familienversion.");
        group.MapGet("/", async (IProfilVerwaltung profile, HttpContext context, CancellationToken ct) =>
        {
            var result = await profile.Laden(ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<ProfilAntwort>().ProducesProblem(400);
        group.MapPut("/", async (ProfilSpeichernAnfrage request, IProfilVerwaltung profile, HttpContext context, CancellationToken ct) =>
        {
            var result = await profile.Speichern(request, ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<ProfilAntwort>().ProducesProblem(400).ProducesProblem(409);
        return endpoints;
    }
}
