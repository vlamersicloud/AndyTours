using AndyTours.Contracts.V1;

namespace AndyTours.Contracts.V1;

public sealed record PlanungsTeilstueck(IReadOnlyList<PlanungsRoutingpunkt> Punkte, IReadOnlyList<PlanungsAbbiegepunkt> Abbiegepunkte, bool IstLuftlinie);
public sealed record PlanungsStand(CourseFahrprofil Profil, IReadOnlyList<PlanungsPosition> Wegpunkte, IReadOnlyList<PlanungsTeilstueck?> Teilstuecke);
public sealed record PlanungsGpxExportAnfrage(string Name, PlanungsStand Planung);
public sealed record PlanungsGpxExportAntwort(string Dateiname, byte[] Inhalt);
