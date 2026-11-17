namespace AndyTours.Application.Sicherung;

/// <summary>Read-only catalog metadata; exposes no storage paths or destructive operations.</summary>
public interface ISicherungskatalog
{
    /// <summary>Lists backup metadata; listing does not grant download, restore, or delete capability.</summary>
    Task<AndyTours.Contracts.V1.SicherungListeAntwort> Liste(CancellationToken cancellationToken);
}
