namespace AndyTours.Client.Sicherung;

/// <summary>
/// Opens the durable pending-operation journal while holding an exclusive per-backend local lock.
/// A non-empty operation ID must be persisted before this method returns; unreadable or corrupt journals fail closed. Implementations must move filesystem I/O off the UI thread, and pending IDs must survive window close and application restart.
/// </summary>
public interface ISicherungsjournal
{
    Task<ISicherungsjournalSperre> Oeffnen(CancellationToken cancellationToken);
}

/// <summary>
/// Holds the backend-scoped journal lock for the whole create/retry HTTP operation.
/// </summary>
public interface ISicherungsjournalSperre : IDisposable
{
    Guid VorgangsId { get; }

    /// <summary>Clears only the matching pending ID after a validated success; failures retain it.</summary>
    Task Bestaetigen(Guid vorgangsId, CancellationToken cancellationToken);
}
