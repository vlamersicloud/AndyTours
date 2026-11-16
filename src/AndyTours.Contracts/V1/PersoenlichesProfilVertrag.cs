namespace AndyTours.Contracts.V1;

/// <summary>Versionierter Snapshot des persönlichen Profils.</summary>
public sealed record ProfilAntwort(string Version, FahrerAntwort? Fahrer);

/// <summary>Bearbeitbare persönliche Profildaten.</summary>
public sealed record ProfilWerte(string Vorname, string Nachname, DateOnly? Geburtstag, int GroesseCm,
    double GewichtKg, int Ruhepuls, int Maxpuls, string Heimatort);

/// <summary>Compare-and-swap Anfrage zum Speichern persönlicher Daten.</summary>
public sealed record ProfilSpeichernAnfrage(string Version, ProfilWerte Werte);
