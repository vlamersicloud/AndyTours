namespace AndyTours.Daten.Datensicherung;

/// <summary>Owned child process boundary; implementations must expose raw bounded-readable streams.</summary>
public interface IPruefprozess : IDisposable
{
    Stream StandardOutput { get; }
    Stream StandardError { get; }
    bool HasExited { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken);
    int ExitCode { get; }
    void KillOwnedTree();
}

public interface IPruefprozessStarter
{
    IPruefprozess Start(string trustedExecutable, string databasePath);
}
