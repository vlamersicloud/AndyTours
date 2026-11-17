using System.Diagnostics;

namespace AndyTours.Daten.Datensicherung;

/// <summary>Starts and owns only the configured checker child process.</summary>
public sealed class SystemPruefprozessStarter : IPruefprozessStarter
{
    public IPruefprozess Start(string trustedExecutable, string databasePath)
    {
        var info = new ProcessStartInfo
        {
            FileName = trustedExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(databasePath);
        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Checker start failed.");
            return new OwnedPruefprozess(process);
        }
        catch { process.Dispose(); throw; }
    }

    private sealed class OwnedPruefprozess(Process process) : IPruefprozess
    {
        public Stream StandardOutput => process.StandardOutput.BaseStream;
        public Stream StandardError => process.StandardError.BaseStream;
        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public void KillOwnedTree() { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        public void Dispose() => process.Dispose();
    }
}
