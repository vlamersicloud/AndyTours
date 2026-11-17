namespace AndyTours.Contracts.V1;

/// <summary>Entry kind declared by a versioned backup manifest.</summary>
public enum SicherungsmanifestEintragart
{
    /// <summary>The single database payload, stored as andytours.db at the archive root.</summary>
    Datenbank = 0,

    /// <summary>A regular file payload; filesystem links are not manifest entries.</summary>
    Datei = 1
}

/// <summary>
/// Structural archive manifest. It describes relative payload paths and hashes but does not prove
/// payload readability, SQLite validity, or a coherent source snapshot.
/// </summary>
/// <param name="Formatversion">Supported manifest format version; currently 1.</param>
/// <param name="ErstelltUtc">UTC creation timestamp, with zero offset.</param>
/// <param name="Eintraege">Declared archive payload entries.</param>
public sealed record Sicherungsmanifest(
    int Formatversion,
    DateTimeOffset ErstelltUtc,
    IReadOnlyList<SicherungsmanifestEintrag> Eintraege);

/// <summary>A payload entry with a normalized archive-relative path and SHA-256 of its full content.</summary>
/// <param name="RelativerPfad">Normalized relative path; never an absolute host path.</param>
/// <param name="Art">Payload kind.</param>
/// <param name="Byteanzahl">Payload byte length.</param>
/// <param name="InhaltHash">SHA-256 hex digest of the complete payload.</param>
public sealed record SicherungsmanifestEintrag(
    string RelativerPfad,
    SicherungsmanifestEintragart Art,
    long Byteanzahl,
    string InhaltHash);
