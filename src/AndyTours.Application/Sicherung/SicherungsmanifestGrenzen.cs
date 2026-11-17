namespace AndyTours.Application.Sicherung;

/// <summary>Explicit caller-supplied bounds for validating a manifest; no implicit product defaults.</summary>
/// <param name="MaxEintraege">Maximum number of manifest entries; must be positive.</param>
/// <param name="MaxDateiBytes">Maximum bytes in one payload; must be positive.</param>
/// <param name="MaxGesamtBytes">Maximum total payload bytes; must be positive.</param>
/// <param name="MaxPfadzeichen">Maximum normalized path length in characters; must be positive.</param>
public sealed record SicherungsmanifestGrenzen(
    int MaxEintraege,
    long MaxDateiBytes,
    long MaxGesamtBytes,
    int MaxPfadzeichen);
