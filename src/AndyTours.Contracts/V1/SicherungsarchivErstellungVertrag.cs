namespace AndyTours.Contracts.V1;

/// <summary>A client operation identifier for idempotent full-archive creation; contains no paths or server policy.</summary>
public sealed record SicherungsarchivErstellungsanfrage(Guid VorgangsId);

/// <summary>The immutable archive result for one creation operation.</summary>
/// <param name="Sicherung">The opaque catalog result, never a server filesystem path.</param>
/// <param name="BereitsVorhanden">True when this operation had already produced its validated archive.</param>
public sealed record SicherungsarchivErstellungsantwort(SicherungAntwort Sicherung, bool BereitsVorhanden);
