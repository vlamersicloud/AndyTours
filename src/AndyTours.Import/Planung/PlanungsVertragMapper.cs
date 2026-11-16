using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Import.Planung;

public static class PlanungsVertragMapper
{
    private const long MaxPunkte = 2_000_000;

    public static Tourenplanung ToCore(PlanungsStand stand, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (stand is null || !Enum.IsDefined(stand.Profil) || stand.Wegpunkte is null || stand.Teilstuecke is null || stand.Wegpunkte.Count < 2 ||
            stand.Teilstuecke.Count != stand.Wegpunkte.Count - 1) throw Invalid();
        long total = stand.Wegpunkte.Count;
        for (int i = 0; i < stand.Teilstuecke.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var s = stand.Teilstuecke[i]; if (s is null) continue;
            if (s.Punkte is null || s.Abbiegepunkte is null || s.Punkte.Count < 2) throw Invalid();
            total += (long)s.Punkte.Count + s.Abbiegepunkte.Count; if (total > MaxPunkte) throw Invalid();
        }
        if (total > MaxPunkte) throw Invalid();
        var waypoints = new Geoposition[stand.Wegpunkte.Count];
        for (int i = 0; i < waypoints.Length; i++) { cancellationToken.ThrowIfCancellationRequested(); var p = stand.Wegpunkte[i]; if (p is null) throw Invalid(); Validate(p); waypoints[i] = ToCore(p); }
        var segments = new Teilstueck?[stand.Teilstuecke.Count];
        for (int i = 0; i < segments.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var s = stand.Teilstuecke[i]; if (s is null) continue;
            var points = new Verlaufspunkt[s.Punkte.Count];
            for (int j = 0; j < points.Length; j++) { cancellationToken.ThrowIfCancellationRequested(); var p = s.Punkte[j]; if (p is null || p.Position is null) throw Invalid(); Validate(p.Position); if (p.Hoehe is double h && !double.IsFinite(h)) throw Invalid(); points[j] = new Verlaufspunkt { Position = ToCore(p.Position), Hoehe = p.Hoehe }; }
            var turns = new Abbiegepunkt[s.Abbiegepunkte.Count];
            for (int j = 0; j < turns.Length; j++) { cancellationToken.ThrowIfCancellationRequested(); var t = s.Abbiegepunkte[j]; if (t is null || t.Position is null || !TryToCore(t.Richtung, out var direction) || t.Ausfahrt is <= 0) throw Invalid(); Validate(t.Position); turns[j] = new Abbiegepunkt(ToCore(t.Position), direction, t.Ausfahrt); }
            segments[i] = new Teilstueck(Array.AsReadOnly(points), Array.AsReadOnly(turns), s.IstLuftlinie);
        }
        return new Tourenplanung(ToCore(stand.Profil), Array.AsReadOnly(waypoints), Array.AsReadOnly(segments));
    }

    public static PlanungsStand ToDto(Tourenplanung planung, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (planung is null || planung.Wegpunkte is null || planung.Teilstuecke is null || planung.Wegpunkte.Count < 2 || planung.Teilstuecke.Count != planung.Wegpunkte.Count - 1) throw Invalid();
        long total = planung.Wegpunkte.Count;
        for (int i = 0; i < planung.Teilstuecke.Count; i++) { cancellationToken.ThrowIfCancellationRequested(); var s = planung.Teilstuecke[i]; if (s is null) continue; if (s.Punkte is null || s.Abbiegepunkte is null || s.Punkte.Count < 2) throw Invalid(); total += (long)s.Punkte.Count + s.Abbiegepunkte.Count; if (total > MaxPunkte) throw Invalid(); }
        if (total > MaxPunkte || !TryToDto(planung.Profil, out var profile)) throw Invalid();
        var waypoints = new PlanungsPosition[planung.Wegpunkte.Count];
        for (int i = 0; i < waypoints.Length; i++) { cancellationToken.ThrowIfCancellationRequested(); waypoints[i] = FromCore(planung.Wegpunkte[i]); }
        var segments = new PlanungsTeilstueck?[planung.Teilstuecke.Count];
        for (int i = 0; i < segments.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var s = planung.Teilstuecke[i]; if (s is null) continue;
            var pts = new PlanungsRoutingpunkt[s.Punkte.Count];
            for (int j = 0; j < pts.Length; j++) { cancellationToken.ThrowIfCancellationRequested(); var p = s.Punkte[j]; if (p is null || p.Position is null || p.Hoehe is double h && !double.IsFinite(h)) throw Invalid(); pts[j] = new PlanungsRoutingpunkt(FromCore(p.Position), p.Hoehe); }
            var turns = new PlanungsAbbiegepunkt[s.Abbiegepunkte.Count];
            for (int j = 0; j < turns.Length; j++) { cancellationToken.ThrowIfCancellationRequested(); var t = s.Abbiegepunkte[j]; if (t is null || !TryToDto(t.Richtung, out var direction) || t.Ausfahrt is <= 0) throw Invalid(); turns[j] = new PlanungsAbbiegepunkt(FromCore(t.Position), direction, t.Ausfahrt); }
            segments[i] = new PlanungsTeilstueck(Array.AsReadOnly(pts), Array.AsReadOnly(turns), s.IstLuftlinie);
        }
        return new PlanungsStand(profile, Array.AsReadOnly(waypoints), Array.AsReadOnly(segments));
    }

    private static bool TryToCore(CourseFahrprofil p, out Fahrprofil result) { result = p switch { CourseFahrprofil.Rennrad => Fahrprofil.Rennrad, CourseFahrprofil.Trekking => Fahrprofil.Trekking, CourseFahrprofil.Mountainbike => Fahrprofil.Mountainbike, _ => (Fahrprofil)(-1) }; return Enum.IsDefined(result); }
    private static Fahrprofil ToCore(CourseFahrprofil p) => TryToCore(p, out var result) ? result : throw Invalid();
    private static bool TryToDto(Fahrprofil p, out CourseFahrprofil result) { result = p switch { Fahrprofil.Rennrad => CourseFahrprofil.Rennrad, Fahrprofil.Trekking => CourseFahrprofil.Trekking, Fahrprofil.Mountainbike => CourseFahrprofil.Mountainbike, _ => (CourseFahrprofil)(-1) }; return Enum.IsDefined(result); }
    private static bool TryToCore(PlanungsAbbiegerichtung p, out Abbiegerichtung result) { result = p switch { PlanungsAbbiegerichtung.Links => Abbiegerichtung.Links, PlanungsAbbiegerichtung.Rechts => Abbiegerichtung.Rechts, PlanungsAbbiegerichtung.LeichtLinks => Abbiegerichtung.LeichtLinks, PlanungsAbbiegerichtung.LeichtRechts => Abbiegerichtung.LeichtRechts, PlanungsAbbiegerichtung.ScharfLinks => Abbiegerichtung.ScharfLinks, PlanungsAbbiegerichtung.ScharfRechts => Abbiegerichtung.ScharfRechts, PlanungsAbbiegerichtung.Kreisverkehr => Abbiegerichtung.Kreisverkehr, PlanungsAbbiegerichtung.Wenden => Abbiegerichtung.Wenden, _ => (Abbiegerichtung)(-1) }; return Enum.IsDefined(result); }
    private static bool TryToDto(Abbiegerichtung p, out PlanungsAbbiegerichtung result) { result = p switch { Abbiegerichtung.Links => PlanungsAbbiegerichtung.Links, Abbiegerichtung.Rechts => PlanungsAbbiegerichtung.Rechts, Abbiegerichtung.LeichtLinks => PlanungsAbbiegerichtung.LeichtLinks, Abbiegerichtung.LeichtRechts => PlanungsAbbiegerichtung.LeichtRechts, Abbiegerichtung.ScharfLinks => PlanungsAbbiegerichtung.ScharfLinks, Abbiegerichtung.ScharfRechts => PlanungsAbbiegerichtung.ScharfRechts, Abbiegerichtung.Kreisverkehr => PlanungsAbbiegerichtung.Kreisverkehr, Abbiegerichtung.Wenden => PlanungsAbbiegerichtung.Wenden, _ => (PlanungsAbbiegerichtung)(-1) }; return Enum.IsDefined(result); }
    private static Geoposition ToCore(PlanungsPosition p) => new(p.Breite, p.Laenge);
    private static PlanungsPosition FromCore(Geoposition p) { if (p is null) throw Invalid(); Validate(p.Breite, p.Laenge); return new(p.Breite, p.Laenge); }
    private static void Validate(PlanungsPosition p) => Validate(p.Breite, p.Laenge);
    private static void Validate(double lat, double lon) { if (!double.IsFinite(lat) || lat is < -90 or > 90 || !double.IsFinite(lon) || lon is < -180 or > 180) throw Invalid(); }
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungsdatei ungültig.");
}
