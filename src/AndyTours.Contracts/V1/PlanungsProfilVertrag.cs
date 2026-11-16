using AndyTours.Contracts.V1;

namespace AndyTours.Contracts.V1;

public sealed record PlanungsProfilAntwort(string Version, CourseFahrprofil Profil);
public sealed record PlanungsProfilSpeichernAnfrage(string Version, CourseFahrprofil Profil);
