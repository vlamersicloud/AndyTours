using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AndyTours.Api;

public static class TourenMehrfachEndpunkte
{
    public static IEndpointRouteBuilder MapTourenMehrfach(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/touren/mehrfach").WithTags("Touren");
        group.MapPost("/kategorien", (HttpContext context, ITourenMehrfachverwaltung service,
            IOptions<JsonOptions> options, CancellationToken ct) =>
            TourenMehrfachJson.Ausfuehren<TourenKategorienAnfrage>(context, options, service.Kategorien, ct))
            .Accepts<TourenKategorienAnfrage>("application/json").Produces<TourenMehrfachAntwort>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);
        group.MapPost("/aktivitaet", (HttpContext context, ITourenMehrfachverwaltung service,
            IOptions<JsonOptions> options, CancellationToken ct) =>
            TourenMehrfachJson.Ausfuehren<TourenAktivitaetAnfrage>(context, options, service.Aktivitaet, ct))
            .Accepts<TourenAktivitaetAnfrage>("application/json").Produces<TourenMehrfachAntwort>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);
        group.MapPost("/equipment", (HttpContext context, ITourenMehrfachverwaltung service,
            IOptions<JsonOptions> options, CancellationToken ct) =>
            TourenMehrfachJson.Ausfuehren<TourenEquipmentAnfrage>(context, options, service.Equipment, ct))
            .Accepts<TourenEquipmentAnfrage>("application/json").Produces<TourenMehrfachAntwort>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);
        group.MapPost("/loeschen", (HttpContext context, ITourenMehrfachverwaltung service,
            IOptions<JsonOptions> options, CancellationToken ct) =>
            TourenMehrfachJson.Ausfuehren<TourenLoeschenAnfrage>(context, options, service.Loeschen, ct))
            .Accepts<TourenLoeschenAnfrage>("application/json").Produces<TourenMehrfachAntwort>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);
        return app;
    }
}
