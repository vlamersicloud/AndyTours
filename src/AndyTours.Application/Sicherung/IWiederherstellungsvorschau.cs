namespace AndyTours.Application.Sicherung;

using AndyTours.Contracts.V1;

/// <summary>Produces a non-destructive restore preview; this contract does not establish runtime or SQLite validity.</summary>
public interface IWiederherstellungsvorschau
{
    /// <summary>
    /// Resolves the opaque catalog ID and expected complete-file hash fail-closed and returns preview metadata only.
    /// Must not write to or migrate the active database, restore files, or authorize applying a restore.
    /// Application readiness must remain false until staging validation, an operational guard, a pre-restore backup,
    /// and explicit confirmation are all established by a separate verified workflow.
    /// </summary>
    Task<WiederherstellungsvorschauAntwort> Pruefen(
        WiederherstellungsvorschauAnfrage anfrage,
        CancellationToken cancellationToken);
}
