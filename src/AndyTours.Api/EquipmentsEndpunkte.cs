using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace AndyTours.Api;

/// <summary>HTTP-Endpunkte für versionierte Equipmentverwaltung.</summary>
public static class EquipmentsEndpunkte
{
    /// <summary>Registriert die Equipmentrouten unter /api/v1/equipments.</summary>
    public static IEndpointRouteBuilder MapEquipments(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/equipments").WithTags("Equipment")
            .WithDescription("Familienversion über die vollständige nach ID geordnete Equipmentliste; Änderungen verwenden Compare-and-Swap.");
        group.MapGet("/", async (IEquipmentsVerwaltung management, HttpContext context, CancellationToken ct) =>
        {
            var result = await management.Laden(ct);
            context.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Ok(result);
        }).Produces<EquipmentsAntwort>().ProducesProblem(400);
        group.MapPost("/", async (EquipmentSpeichernAnfrage request, IEquipmentsVerwaltung management,
            HttpContext context, CancellationToken ct) => WithVersion(await management.Anlegen(request, ct), context))
            .Produces<EquipmentsAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/{id:long}", async (long id, EquipmentSpeichernAnfrage request,
            IEquipmentsVerwaltung management, HttpContext context, CancellationToken ct) =>
            WithVersion(await management.Aendern(id, request, ct), context))
            .Produces<EquipmentsAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapDelete("/{id:long}", async (long id, [FromHeader(Name = "If-Match")] string version,
            IEquipmentsVerwaltung management, HttpContext context, CancellationToken ct) =>
        {
            var normalized = Normalize(version);
            return WithVersion(await management.Loeschen(id, normalized, ct), context);
        }).WithDescription("If-Match ist erforderlich; akzeptiert werden ein unquotierter 64-stelliger Hash oder ein quotierter starker ETag.")
            .Produces<EquipmentsAntwort>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }

    private static string Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "If-Match mit der Familienversion ist erforderlich.");
        if (version.Length >= 2 && version[0] == '"' && version[^1] == '"') return version[1..^1];
        return version;
    }

    private static IResult WithVersion(EquipmentsAntwort response, HttpContext context)
    {
        context.Response.Headers.ETag = $"\"{response.Version}\"";
        return Results.Ok(response);
    }
}
