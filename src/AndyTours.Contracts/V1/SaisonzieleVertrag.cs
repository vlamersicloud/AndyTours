namespace AndyTours.Contracts.V1;

/// <summary>Ein explizites Saisonziel für eine Aktivität ab einem bestimmten Jahr.</summary>
public sealed record SaisonzielAntwort(long AktivitaetId, int AbJahr, int ZielKm, int ZielHm, int ZielAnzahl);

/// <summary>Versionierter Snapshot der Aktivitätenbasisziele und expliziten Saisonzielüberschreibungen.</summary>
public sealed record SaisonzieleAntwort(string Version, IReadOnlyList<AktivitaetAntwort> Aktivitaeten,
    IReadOnlyList<SaisonzielAntwort> Ziele);

/// <summary>Ersetzt explizite Ziele für die angegebenen Jahre; alle anderen Jahre bleiben unangetastet.</summary>
public sealed record SaisonzieleSpeichernAnfrage(string Version, IReadOnlyList<int> Jahre,
    IReadOnlyList<SaisonzielAntwort> Ziele);
