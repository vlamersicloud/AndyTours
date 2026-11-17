namespace AndyTours.Contracts.V1;

/// <summary>
/// Identifies the represented backup contents. LegacySqlite contains only the database;
/// Vollarchiv denotes metadata from a validated archive, not proof of SQLite or restore validity.
/// </summary>
public enum Sicherungsart
{
    /// <summary>Database-only SQLite backup; does not include FIT files or attachments.</summary>
    LegacySqlite = 0,

    /// <summary>Validated full-archive metadata; not proof of SQLite validity or restoreability.</summary>
    Vollarchiv = 1
}

/// <summary>
/// Catalog metadata addressed by an opaque backup identifier, never by a filename or path.
/// Listing a catalog entry does not authorize restore or deletion.
/// </summary>
/// <param name="Id">Non-empty opaque backup identifier, not a storage path.</param>
/// <param name="Art">Declared content type; the hash alone does not prove schema or restore validity.</param>
/// <param name="ErstelltUtc">UTC timestamp when known; null when legacy metadata has no timezone.</param>
/// <param name="Byteanzahl">Complete file size in bytes; non-negative.</param>
/// <param name="InhaltHash">SHA-256 of the complete represented file.</param>
public sealed record SicherungAntwort(
    Guid Id,
    Sicherungsart Art,
    DateTimeOffset? ErstelltUtc,
    long Byteanzahl,
    string InhaltHash)
{
    /// <summary>Exact legacy filename timestamp (yyyy-MM-dd_HH-mm-ss), not a path; null when not applicable.</summary>
    public string? LegacyZeitstempel { get; init; }
}

/// <summary>Catalog entries, without paths, download data, or restore/delete authority.</summary>
public sealed record SicherungListeAntwort(IReadOnlyList<SicherungAntwort> Eintraege);
