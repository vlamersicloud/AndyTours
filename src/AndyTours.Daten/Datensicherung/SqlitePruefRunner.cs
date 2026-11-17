using System.Security.Cryptography;

namespace AndyTours.Daten.Datensicherung;

public sealed class SqlitePruefRunner
{
    private readonly string _executable;
    private readonly TimeSpan _timeout;
    private readonly int _maxOutputBytes;
    private readonly long _maxDatabaseBytes;
    private readonly IPruefprozessStarter _starter;

    public SqlitePruefRunner(string trustedCheckerExecutable, TimeSpan timeout, int maxOutputBytes,
        long maxDatabaseBytes, IPruefprozessStarter starter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedCheckerExecutable);
        ArgumentNullException.ThrowIfNull(starter);
        if (!Path.IsPathFullyQualified(trustedCheckerExecutable) || !File.Exists(trustedCheckerExecutable) ||
            HasReparseAncestor(trustedCheckerExecutable) || timeout <= TimeSpan.Zero ||
            timeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1) || maxOutputBytes <= 0 || maxDatabaseBytes <= 0)
            throw new ArgumentException("Checker configuration is invalid.");
        _executable = trustedCheckerExecutable;
        _timeout = timeout;
        _maxOutputBytes = maxOutputBytes;
        _maxDatabaseBytes = maxDatabaseBytes;
        _starter = starter;
    }

    public async Task<SqlitePruefprotokoll> PruefenAsync(BereitgestelltePruefdatenbank stage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stage);
        cancellationToken.ThrowIfCancellationRequested();
        var path = stage.DatenbankPfad;
        if (!IstVersiegelteDatei(path, _maxDatabaseBytes)) return SqlitePruefprotokoll.Ergebnis("invalid_input");
        byte[] before;
        try { before = await HashBoundedAsync(path, _maxDatabaseBytes, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { return SqlitePruefprotokoll.Ergebnis("failed"); }
        cancellationToken.ThrowIfCancellationRequested();

        using var deadline = new CancellationTokenSource(_timeout);
        var readerCts = new CancellationTokenSource();
        var timeoutSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeoutRegistration = deadline.Token.Register(() => timeoutSignal.TrySetResult());
        using var callerRegistration = cancellationToken.Register(() => cancelSignal.TrySetResult());

        IPruefprozess child;
        try { child = _starter.Start(_executable, path); }
        catch { readerCts.Dispose(); return SqlitePruefprotokoll.Ergebnis("child_failed"); }

        Task<byte[]> stdout = Task.FromResult(Array.Empty<byte>());
        Task<byte[]> stderr = Task.FromResult(Array.Empty<byte>());
        var transferred = false;
        var disposed = false;
        try
        {
            try
            {
                var outStream = child.StandardOutput;
                var errStream = child.StandardError;
                stdout = ReadBoundedAsync(outStream, _maxOutputBytes, readerCts.Token);
                stderr = ReadBoundedAsync(errStream, _maxOutputBytes, readerCts.Token);
                ObserveFault(stdout);
                ObserveFault(stderr);
            }
            catch
            {
                readerCts.Cancel();
                TryKill(child);
                return FailOrQuarantine(child, stdout, stderr, stage, readerCts, ref transferred, ref disposed);
            }

            Task exit;
            try { exit = child.WaitForExitAsync(CancellationToken.None); ObserveFault(exit); }
            catch
            {
                readerCts.Cancel();
                TryKill(child);
                return FailOrQuarantine(child, stdout, stderr, stage, readerCts, ref transferred, ref disposed);
            }

            var failed = false;
            var timedOut = false;
            var cancelled = false;
            while (!exit.IsCompleted || !stdout.IsCompleted || !stderr.IsCompleted)
            {
                if (exit.IsFaulted || exit.IsCanceled || stdout.IsFaulted || stdout.IsCanceled || stderr.IsFaulted || stderr.IsCanceled)
                { failed = true; break; }
                if (deadline.IsCancellationRequested) { timedOut = true; break; }
                if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }

                var pending = new List<Task>(5);
                if (!exit.IsCompleted) pending.Add(exit);
                if (!stdout.IsCompleted) pending.Add(stdout);
                if (!stderr.IsCompleted) pending.Add(stderr);
                pending.Add(timeoutSignal.Task);
                pending.Add(cancelSignal.Task);
                var completed = await Task.WhenAny(pending).ConfigureAwait(false);
                if (completed == timeoutSignal.Task) { timedOut = true; break; }
                if (completed == cancelSignal.Task) { cancelled = true; break; }
            }

            if (failed || timedOut || cancelled)
            {
                readerCts.Cancel();
                TryKill(child);
                return FailOrQuarantine(child, stdout, stderr, stage, readerCts, ref transferred, ref disposed,
                    cancelled ? new OperationCanceledException(cancellationToken) : null);
            }

            bool exited;
            try { exited = child.HasExited; } catch { exited = false; }
            if (!exited || !exit.IsCompletedSuccessfully || !stdout.IsCompletedSuccessfully || !stderr.IsCompletedSuccessfully)
            {
                readerCts.Cancel();
                TryKill(child);
                return FailOrQuarantine(child, stdout, stderr, stage, readerCts, ref transferred, ref disposed);
            }

            int exitCode;
            try { exitCode = child.ExitCode; }
            catch { return FailOrQuarantine(child, stdout, stderr, stage, readerCts, ref transferred, ref disposed); }

            var after = await HashBoundedAsync(path, _maxDatabaseBytes, CancellationToken.None).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(before, after))
                return SqlitePruefprotokoll.Ergebnis("database_changed");

            var parsed = SqlitePruefprotokoll.Parse(stdout.Result);
            if (parsed.Status == "protocol_invalid") return parsed;
            if (parsed.Status == "ok")
                return exitCode == 0 ? parsed : SqlitePruefprotokoll.Ergebnis("protocol_invalid");
            return exitCode is 1 or 2 ? parsed : SqlitePruefprotokoll.Ergebnis("protocol_invalid");
        }
        catch
        {
            if (!transferred && !disposed)
            {
                readerCts.Cancel();
                TryKill(child);
                _ = FailOrQuarantine(child, stdout, stderr, stage, readerCts, ref transferred, ref disposed);
            }
            throw;
        }
        finally
        {
            if (!transferred && !disposed)
            {
                bool safe = false;
                try { safe = child.HasExited && stdout.IsCompleted && stderr.IsCompleted; } catch { }
                if (safe) { child.Dispose(); readerCts.Dispose(); disposed = true; }
                else Transfer(child, stdout, stderr, stage, readerCts, ref transferred);
            }
        }
    }

    private static SqlitePruefprotokoll FailOrQuarantine(IPruefprozess child, Task stdout, Task stderr,
        BereitgestelltePruefdatenbank stage, CancellationTokenSource readerCts, ref bool transferred, ref bool disposed,
        OperationCanceledException? cancellation = null)
    {
        bool exited = false;
        try { exited = child.HasExited; } catch { }
        if (!exited || !stdout.IsCompleted || !stderr.IsCompleted)
        {
            Transfer(child, stdout, stderr, stage, readerCts, ref transferred);
            if (cancellation is not null) throw cancellation;
            return SqlitePruefprotokoll.Ergebnis("failed");
        }
        child.Dispose();
        readerCts.Dispose();
        disposed = true;
        if (cancellation is not null) throw cancellation;
        return SqlitePruefprotokoll.Ergebnis("failed");
    }

    private static void Transfer(IPruefprozess child, Task stdout, Task stderr, BereitgestelltePruefdatenbank stage,
        CancellationTokenSource readerCts, ref bool transferred)
    {
        SqlitePruefQuarantaene.Uebernehmen(new SqlitePruefQuarantaeneintrag(child, stdout, stderr, stage, readerCts));
        transferred = true;
    }

    private static void ObserveFault(Task task) => _ = task.ContinueWith(
        static faulted => _ = faulted.Exception,
        CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    private static void TryKill(IPruefprozess child) { try { if (!child.HasExited) child.KillOwnedTree(); } catch { } }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximum, CancellationToken token)
    {
        using var output = new MemoryStream(Math.Min(maximum, 4096));
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (read == 0) return output.ToArray();
            if (output.Length + read > maximum) throw new IOException();
            output.Write(buffer, 0, read);
        }
    }

    private static bool IstVersiegelteDatei(string path, long maximum)
    {
        try { var info = new FileInfo(path); return Path.IsPathFullyQualified(path) && info.Exists && info.Length <= maximum && !HasReparseAncestor(path); }
        catch { return false; }
    }

    private static bool HasReparseAncestor(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; } catch { return true; }
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current) break;
            current = parent;
        }
        return false;
    }

    private static async Task<byte[]> HashBoundedAsync(string path, long maximum, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[8192];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (read == 0) break;
            total = checked(total + read);
            if (total > maximum) throw new IOException();
            hash.AppendData(buffer, 0, read);
        }
        return hash.GetHashAndReset();
    }
}
