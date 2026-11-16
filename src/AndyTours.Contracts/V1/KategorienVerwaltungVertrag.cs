namespace AndyTours.Contracts.V1;

/// <summary>Versionierter vollständiger Kategorienbestand.</summary>
/// <param name="Version">SHA-256-Familienversion der nach ID geordneten Einträge.</param>
/// <param name="Eintraege">Kategorien und Standardkennzeichen.</param>
public sealed record KategorienAntwort(string Version, IReadOnlyList<KategorieAntwort> Eintraege);

/// <summary>Anfrage zum Anlegen oder Umbenennen einer Kategorie.</summary>
/// <param name="Version">Erwartete Familienversion.</param>
/// <param name="Name">Kategoriename.</param>
public sealed record KategorieSpeichernAnfrage(string Version, string Name);

/// <summary>Anfrage zum Setzen oder Entfernen des Standardkennzeichens.</summary>
/// <param name="Version">Erwartete Familienversion.</param>
/// <param name="KategorieId">Zielkategorie.</param>
/// <param name="IstStandard">Neuer Standardzustand.</param>
public sealed record KategorieStandardAnfrage(string Version, long KategorieId, bool IstStandard);

/// <summary>Anfrage zum Zusammenführen zweier Kategorien.</summary>
/// <param name="Version">Erwartete Familienversion.</param>
/// <param name="QuelleId">Zu entfernende Quellkategorie.</param>
/// <param name="ZielId">Erhaltende Zielkategorie.</param>
public sealed record KategorienZusammenfuehrenAnfrage(string Version, long QuelleId, long ZielId);
