namespace AndyTours.Contracts.V1;

/// <summary>Vollständiger, geordneter Aktivitätsbestand mit Familienversion.</summary>
/// <param name="Version">ETag-Version des Aktivitätsbestands.</param>
/// <param name="Eintraege">Aktivitäten in ihrer aktuellen Reihenfolge.</param>
public sealed record AktivitaetenAntwort(string Version, IReadOnlyList<AktivitaetAntwort> Eintraege);

/// <summary>Angaben zum Anlegen oder Umbenennen einer Aktivität.</summary>
/// <param name="Version">Erwartete Version des Aktivitätsbestands.</param>
/// <param name="Name">Neuer oder anzulegender Anzeigename.</param>
public sealed record AktivitaetSpeichernAnfrage(string Version, string Name);

/// <summary>Angaben zur Auswahl der Hauptaktivität.</summary>
/// <param name="Version">Erwartete Version des Aktivitätsbestands.</param>
/// <param name="AktivitaetId">Kennung der Hauptaktivität.</param>
public sealed record AktivitaetAuswahlAnfrage(string Version, long AktivitaetId);

/// <summary>Angaben zur vollständigen Reihenfolge der Aktivitäten.</summary>
/// <param name="Version">Erwartete Version des Aktivitätsbestands.</param>
/// <param name="Ids">Alle Aktivitätskennungen in der gewünschten Reihenfolge.</param>
public sealed record AktivitaetenReihenfolgeAnfrage(string Version, IReadOnlyList<long> Ids);

/// <summary>Angaben zum Zusammenführen zweier Aktivitäten.</summary>
/// <param name="Version">Erwartete Version des Aktivitätsbestands.</param>
/// <param name="QuelleId">Kennung der zusammenzuführenden Quellaktivität.</param>
/// <param name="ZielId">Kennung der Zielaktivität.</param>
public sealed record AktivitaetenZusammenfuehrenAnfrage(string Version, long QuelleId, long ZielId);
