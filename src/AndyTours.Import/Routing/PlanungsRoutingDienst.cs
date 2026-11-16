using System.Net;
using AndyTours.Application.Export;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Import.Routing;
using CoreDirection = AndyTours.Core.Modell.Abbiegerichtung;
using CoreProfile = AndyTours.Core.Modell.Fahrprofil;
using CorePosition = AndyTours.Core.Modell.Geoposition;
using CoreTurn = AndyTours.Core.Modell.Abbiegepunkt;
using CoreTrackPoint = AndyTours.Core.Modell.Verlaufspunkt;

namespace AndyTours.Import.Routing;

/// <summary>Serverseitige Begrenzung und Übersetzung der konfigurierten Routingprovider.</summary>
public sealed class PlanungsRoutingDienst : IPlanungsRouting
{
    private readonly ICourseExportServerdaten _serverdaten;
    private readonly Func<HttpClient> _httpFactory;

    public PlanungsRoutingDienst(ICourseExportServerdaten serverdaten, Func<HttpClient>? httpFactory = null)
    {
        _serverdaten = serverdaten ?? throw new ArgumentNullException(nameof(serverdaten));
        _httpFactory = httpFactory ?? CreateHttpClient;
    }

    public async Task<PlanungsRoutingAntwort> Berechnen(PlanungsRoutingAnfrage anfrage, CancellationToken cancellationToken)
    {
        Validate(anfrage);
        cancellationToken.ThrowIfCancellationRequested();
        var context = await _serverdaten.Laden(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var from = ToCore(anfrage.Von.Position);
        var to = ToCore(anfrage.Nach.Position);
        var profile = ToCore(anfrage.Profil);
        var request = BildeRoutinganfrage.Ausfuehren(context.Routing, profile, from, to);
        if (request is null) return Fallback(from, to, "routing_einstellungen_unvollstaendig");

        using var http = _httpFactory();
        var segment = await RoutingTransport.Ermittle(http, request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (segment is null || !ValidSegment(segment)) return Fallback(from, to, "routing_nicht_erreichbar");
        return new PlanungsRoutingAntwort(
            segment.Punkte.Select(p => new PlanungsRoutingpunkt(ToDto(p.Position!), Finite(p.Hoehe))).ToArray(),
            segment.Abbiegepunkte.Select(ToDto).ToArray(),
            segment.IstLuftlinie,
            segment.IstLuftlinie ? "routing_nicht_erreichbar" : null);
    }

    private static HttpClient CreateHttpClient() => new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static void Validate(PlanungsRoutingAnfrage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Profil)) throw new ArgumentException("Routinganfrage ungültig.", nameof(request));
        Validate(request.Von);
        Validate(request.Nach);
    }

    private static void Validate(PlanungsRoutingpunkt point)
    {
        if (point?.Position is not { } p || !double.IsFinite(p.Breite) || p.Breite is < -90 or > 90 ||
            !double.IsFinite(p.Laenge) || p.Laenge is < -180 or > 180 ||
            point.Hoehe is { } h && !double.IsFinite(h))
            throw new ArgumentException("Routinganfrage ungültig.");
    }

    private static bool ValidSegment(Teilstueck segment) =>
        !segment.IstLuftlinie && segment.Punkte is { Count: >= 2 } &&
        segment.Punkte.All(p => p.Position is { } pos && double.IsFinite(pos.Breite) && pos.Breite is >= -90 and <= 90 &&
            double.IsFinite(pos.Laenge) && pos.Laenge is >= -180 and <= 180 &&
            (p.Hoehe is null || double.IsFinite(p.Hoehe.Value))) &&
        segment.Abbiegepunkte.All(t => Enum.IsDefined(t.Richtung) && (t.Ausfahrt is null || t.Ausfahrt > 0));

    private static CoreProfile ToCore(CourseFahrprofil profile) => profile switch
    {
        CourseFahrprofil.Rennrad => CoreProfile.Rennrad,
        CourseFahrprofil.Trekking => CoreProfile.Trekking,
        CourseFahrprofil.Mountainbike => CoreProfile.Mountainbike,
        _ => throw new ArgumentException("Routinganfrage ungültig.")
    };

    private static CorePosition ToCore(PlanungsPosition point) => new(point.Breite, point.Laenge);
    private static PlanungsPosition ToDto(CorePosition point) => new(point.Breite, point.Laenge);
    private static double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;

    private static PlanungsAbbiegepunkt ToDto(CoreTurn turn) => new(ToDto(turn.Position), turn.Richtung switch
    {
        CoreDirection.Links => PlanungsAbbiegerichtung.Links,
        CoreDirection.Rechts => PlanungsAbbiegerichtung.Rechts,
        CoreDirection.LeichtLinks => PlanungsAbbiegerichtung.LeichtLinks,
        CoreDirection.LeichtRechts => PlanungsAbbiegerichtung.LeichtRechts,
        CoreDirection.ScharfLinks => PlanungsAbbiegerichtung.ScharfLinks,
        CoreDirection.ScharfRechts => PlanungsAbbiegerichtung.ScharfRechts,
        CoreDirection.Kreisverkehr => PlanungsAbbiegerichtung.Kreisverkehr,
        CoreDirection.Wenden => PlanungsAbbiegerichtung.Wenden,
        _ => throw new InvalidOperationException("Routingantwort ungültig.")
    }, turn.Ausfahrt is > 0 ? turn.Ausfahrt : null);

    private static PlanungsRoutingAntwort Fallback(CorePosition from, CorePosition to, string warning)
    {
        var line = ErstelleLuftlinie.Ausfuehren(from, to);
        return new PlanungsRoutingAntwort(
            line.Punkte.Select(p => new PlanungsRoutingpunkt(ToDto(p.Position!), null)).ToArray(),
            [],
            true,
            warning);
    }
}
