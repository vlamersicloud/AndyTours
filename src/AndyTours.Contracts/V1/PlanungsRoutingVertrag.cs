using AndyTours.Contracts.V1;

namespace AndyTours.Contracts.V1;

public sealed record PlanungsPosition(double Breite, double Laenge);
public sealed record PlanungsRoutingpunkt(PlanungsPosition Position, double? Hoehe);

public enum PlanungsAbbiegerichtung
{
    Links,
    Rechts,
    LeichtLinks,
    LeichtRechts,
    ScharfLinks,
    ScharfRechts,
    Kreisverkehr,
    Wenden
}

public sealed record PlanungsAbbiegepunkt(PlanungsPosition Position, PlanungsAbbiegerichtung Richtung, int? Ausfahrt);
public sealed record PlanungsRoutingAnfrage(CourseFahrprofil Profil, PlanungsRoutingpunkt Von, PlanungsRoutingpunkt Nach);
public sealed record PlanungsRoutingAntwort(
    IReadOnlyList<PlanungsRoutingpunkt> Punkte,
    IReadOnlyList<PlanungsAbbiegepunkt> Abbiegepunkte,
    bool IstLuftlinie,
    string? Warncode);
