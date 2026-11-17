namespace AndyTours.Application.Sicherung;

using AndyTours.Contracts.V1;

/// <summary>
/// Creates or retrieves the same immutable full archive for an operation ID. Implementations require an outer
/// exclusive dataset lease at the API boundary; they must not acquire nested leases. In-process coordination
/// is not proof against external processes. Existing validated output may be returned; stale/in-progress stages
/// conflict and are not automatically erased. This port does not authorize restore or deletion.
/// The request operation ID must be non-empty; a retry returns the same immutable validated file without overwriting it.
/// </summary>
public interface ISicherungsarchivErstellung
{
    Task<SicherungsarchivErstellungsantwort> Erstellen(
        SicherungsarchivErstellungsanfrage anfrage,
        CancellationToken cancellationToken);
}
