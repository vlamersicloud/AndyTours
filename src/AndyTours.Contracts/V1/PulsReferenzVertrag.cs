namespace AndyTours.Contracts.V1;

/// <summary>
/// Aktuelles Garmin-Referenzprofil beim Abrufzeitpunkt; dies ist keine historische Pulszonenhistorie.
/// Fünf streng aufsteigende bpm-Untergrenzen definieren Zonen 1 bis 5; Zone 0 liegt unter der ersten
/// Grenze und Zone 5 ist nach oben offen.
/// </summary>
public sealed record PulsprofilAntwort(string Sport, IReadOnlyList<int> Untergrenzen);

/// <summary>Aktuelle Pulsreferenz samt Abrufzeitpunkt und opaker CAS-Version.</summary>
/// <remarks>Die Version erlaubt nur einen expliziten Aktualisierungsversuch und enthält keine Geheimnisse.</remarks>
public sealed record PulsReferenzAntwort(
    string Version,
    DateTimeOffset? Abgerufen,
    IReadOnlyList<PulsprofilAntwort> Profile);

/// <summary>Explizite Aktualisierung mit opaker Compare-and-swap-Version.</summary>
public sealed record PulsReferenzAktualisierenAnfrage(string Version);
