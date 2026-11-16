using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Import.Planung;

public static class PlanungsVertragMapper
{
    private const int MaxPunkte = 2_000_000;

    public static Tourenplanung ToCore(PlanungsStand stand, CancellationToken cancellationToken)
    {
        if (stand is null || !Enum.IsDefined(stand.Profil) || stand.Wegpunkte is null || stand.Teilstuecke is null || stand.Wegpunkte.Count < 2 ||
            stand.Teilstuecke.Count != stand.Wegpunkte.Count - 1) throw Invalid();
        int total = 0;
        var waypoints = new Geoposition[stand.Wegpunkte.Count];
        foreach (var p in stand.Wegpunkte) { cancellationToken.ThrowIfCancellationRequested(); AddCount(ref total); if (p is null) throw Invalid(); Validate(p); waypoints[Array.IndexOf(stand.Wegpunkte.ToArray(), p)] = ToCore(p); }
        var segments = new Teilstueck?[stand.Teilstuecke.Count];
        for (int i = 0; i < segments.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var s = stand.Teilstuecke[i]; if (s is null) continue;
            if (s.Punkte is null || s.Abbiegepunkte is null || s.Punkte.Count < 2) throw Invalid();
            var points = new Verlaufspunkt[s.Punkte.Count];
            for (int j = 0; j < points.Length; j++) { cancellationToken.ThrowIfCancellationRequested(); AddCount(ref total); var p = s.Punkte[j]; if (p is null || p.Position is null) throw Invalid(); Validate(p.Position); if (p.Hoehe is double h && !double.IsFinite(h)) throw Invalid(); points[j] = new Verlaufspunkt { Position = ToCore(p.Position), Hoehe = p.Hoehe }; }
            var turns = new Abbiegepunkt[s.Abbiegepunkte.Count];
            for (int j = 0; j < turns.Length; j++) { cancellationToken.ThrowIfCancellationRequested(); AddCount(ref total); var t = s.Abbiegepunkte[j]; if (t is null || t.Position is null || !Enum.IsDefined(t.Richtung) || t.Ausfahrt is <= 0) throw Invalid(); Validate(t.Position); turns[j] = new Abbiegepunkt(ToCore(t.Position), (Abbiegerichtung)t.Richtung, t.Ausfahrt); }
            segments[i] = new Teilstueck(Array.AsReadOnly(points), Array.AsReadOnly(turns), s.IstLuftlinie);
        }
        return new Tourenplanung((Fahrprofil)stand.Profil, Array.AsReadOnly(waypoints), Array.AsReadOnly(segments));
    }

    public static PlanungsStand ToDto(Tourenplanung planung, CancellationToken cancellationToken)
    {
        if (planung is null) throw Invalid();
        var waypoints = planung.Wegpunkte.Select(p => { cancellationToken.ThrowIfCancellationRequested(); return FromCore(p); }).ToArray();
        var segments = planung.Teilstuecke.Select(s => { cancellationToken.ThrowIfCancellationRequested(); if (s is null) return null; var pts = s.Punkte.Select(p => { cancellationToken.ThrowIfCancellationRequested(); if (p.Position is null) throw Invalid(); return new PlanungsRoutingpunkt(FromCore(p.Position), p.Hoehe); }).ToArray(); var turns = s.Abbiegepunkte.Select(t => { cancellationToken.ThrowIfCancellationRequested(); return new PlanungsAbbiegepunkt(FromCore(t.Position), (PlanungsAbbiegerichtung)t.Richtung, t.Ausfahrt); }).ToArray(); return new PlanungsTeilstueck(Array.AsReadOnly(pts), Array.AsReadOnly(turns), s.IstLuftlinie); }).ToArray();
        var dto = new PlanungsStand((CourseFahrprofil)planung.Profil, Array.AsReadOnly(waypoints), Array.AsReadOnly(segments));
        _ = ToCore(dto, cancellationToken);
        return dto;
    }

    private static Geoposition ToCore(PlanungsPosition p) => new(p.Breite, p.Laenge);
    private static PlanungsPosition FromCore(Geoposition p) { if (p is null) throw Invalid(); Validate(p.Breite, p.Laenge); return new(p.Breite, p.Laenge); }
    private static void Validate(PlanungsPosition p) => Validate(p.Breite, p.Laenge);
    private static void Validate(double lat, double lon) { if (!double.IsFinite(lat) || lat is < -90 or > 90 || !double.IsFinite(lon) || lon is < -180 or > 180) throw Invalid(); }
    private static void AddCount(ref int total) { if (++total > MaxPunkte) throw Invalid(); }
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungsdatei ungültig.");
}
