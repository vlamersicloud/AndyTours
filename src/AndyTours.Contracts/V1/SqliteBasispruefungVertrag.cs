namespace AndyTours.Contracts.V1;

/// <summary>Explicit request to inspect the isolated SQLite base of a listed full archive.</summary>
public sealed record SqliteBasispruefungAnfrage(Guid SicherungsId, string ErwarteterInhaltHash);

/// <summary>SQLite base inspection only; this result never authorizes restore or application use.</summary>
public sealed record SqliteBasispruefungAntwort(Guid SicherungsId, string Status, int? SchemaVersion,
    int? CoreTablesChecked, bool QuickCheck, IReadOnlyList<string> Hinweise);
