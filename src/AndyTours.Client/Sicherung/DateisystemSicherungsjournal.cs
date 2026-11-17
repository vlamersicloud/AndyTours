namespace AndyTours.Client.Sicherung;

using System.Security;
using System.Text;

/// <summary>
/// Local durable pending-operation journal. Requires trusted local filesystem semantics; hostile filesystem races
/// and absolute power-loss durability are not guaranteed. Never removes pending state except matching confirmation.
/// </summary>
public sealed class DateisystemSicherungsjournal : ISicherungsjournal
{
    private const string Fehlertext = "Sicherungsjournal ist nicht verfügbar.";
    private readonly string _journalRoot;
    private readonly string _key;

    public DateisystemSicherungsjournal(string journalRoot, Uri basisAdresse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalRoot);
        if (!Path.IsPathFullyQualified(journalRoot)) throw new ArgumentException("Journalordner muss absolut sein.");
        _journalRoot = Path.GetFullPath(journalRoot);
        _key = SicherungsjournalBackend.Kennung(basisAdresse);
    }

    public Task<ISicherungsjournalSperre> Oeffnen(CancellationToken cancellationToken) =>
        Task.Run<ISicherungsjournalSperre>(() => OpenCore(cancellationToken), cancellationToken);

    private JournalLease OpenCore(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        FileStream? lockHandle = null;
        string? ownedTemp = null;
        try
        {
            EnsureSafeDirectory(_journalRoot);
            var lockPath = Path.Combine(_journalRoot, _key + ".lock");
            var pendingPath = Path.Combine(_journalRoot, _key + ".pending");
            if (HasReparseAncestor(lockPath) || HasReparseAncestor(pendingPath)) throw Invalid();

            var lockOptions = new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                BufferSize = 4096
            };
            if (!OperatingSystem.IsWindows()) lockOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            try { lockHandle = new FileStream(lockPath, lockOptions); }
            catch (IOException) { throw Busy(); }

            if (HasReparseAncestor(lockPath) || (File.GetAttributes(lockPath) & FileAttributes.Directory) != 0 ||
                lockHandle.Length != 0) throw Invalid();
            ct.ThrowIfCancellationRequested();

            Guid operationId;
            try
            {
                operationId = ParsePending(ReadPending(pendingPath));
            }
            catch (FileNotFoundException)
            {
                operationId = Guid.NewGuid();
                if (operationId == Guid.Empty) throw Invalid();

                var candidateTemp = Path.Combine(_journalRoot, _key + "." + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = CreatePrivateFile(candidateTemp))
                {
                    ownedTemp = candidateTemp;
                    var bytes = Encoding.ASCII.GetBytes(operationId.ToString("N"));
                    stream.Write(bytes);
                    ct.ThrowIfCancellationRequested();
                    stream.Flush(flushToDisk: true);
                }

                ct.ThrowIfCancellationRequested();
                File.Move(ownedTemp, pendingPath, overwrite: false);
                ownedTemp = null;
            }
            catch (DirectoryNotFoundException)
            {
                operationId = Guid.NewGuid();
                if (operationId == Guid.Empty) throw Invalid();

                var candidateTemp = Path.Combine(_journalRoot, _key + "." + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = CreatePrivateFile(candidateTemp))
                {
                    ownedTemp = candidateTemp;
                    var bytes = Encoding.ASCII.GetBytes(operationId.ToString("N"));
                    stream.Write(bytes);
                    ct.ThrowIfCancellationRequested();
                    stream.Flush(flushToDisk: true);
                }

                ct.ThrowIfCancellationRequested();
                File.Move(ownedTemp, pendingPath, overwrite: false);
                ownedTemp = null;
            }

            ct.ThrowIfCancellationRequested();
            var lease = new JournalLease(pendingPath, lockHandle, operationId);
            lockHandle = null;
            return lease;
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException) { throw Invalid(); }
        catch (UnauthorizedAccessException) { throw Invalid(); }
        catch (SecurityException) { throw Invalid(); }
        catch (ArgumentException) { throw Invalid(); }
        catch (NotSupportedException) { throw Invalid(); }
        finally
        {
            SafeDispose(lockHandle);
            SafeDeleteOwnedTemp(ownedTemp);
        }
    }

    private static byte[] ReadPending(string path)
    {
        if (HasReparseAncestor(path)) throw Invalid();
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw Invalid();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32, FileOptions.SequentialScan);
        if (stream.Length != 32) throw Invalid();
        var bytes = new byte[32];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static FileStream CreatePrivateFile(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private static void EnsureSafeDirectory(string path)
    {
        if (HasReparseAncestor(path)) throw Invalid();
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0) throw Invalid();
        }
        catch (FileNotFoundException)
        {
            CreatePrivateDirectory(path);
            if (HasReparseAncestor(path)) throw Invalid();
        }
        catch (DirectoryNotFoundException)
        {
            CreatePrivateDirectory(path);
            if (HasReparseAncestor(path)) throw Invalid();
        }
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static Guid ParsePending(byte[] bytes)
    {
        if (bytes.Length != 32 || bytes.Any(value => !((value >= (byte)'0' && value <= (byte)'9') ||
                (value >= (byte)'a' && value <= (byte)'f') || (value >= (byte)'A' && value <= (byte)'F'))))
            throw Invalid();
        if (!Guid.TryParseExact(Encoding.ASCII.GetString(bytes), "N", out var id) || id == Guid.Empty) throw Invalid();
        return id;
    }

    private static bool HasReparseAncestor(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || SamePath(parent, current)) break;
            current = parent;
        }
        return false;
    }

    private static bool SamePath(string a, string b) => string.Equals(a, b,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static IOException Invalid() => new(Fehlertext);
    private static IOException Busy() => new("Sicherungsjournal wird bereits verwendet.");

    private static void SafeDispose(FileStream? value)
    {
        try { value?.Dispose(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
    }

    private static void SafeDeleteOwnedTemp(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
    }

    private sealed class JournalLease(string pendingPath, FileStream lockHandle, Guid operationId) : ISicherungsjournalSperre
    {
        private readonly object _gate = new();
        private FileStream? _lockHandle = lockHandle;
        private bool _disposed;
        private bool _confirmed;

        public Guid VorgangsId
        {
            get { lock (_gate) { ThrowIfDisposed(); return operationId; } }
        }

        public Task Bestaetigen(Guid vorgangsId, CancellationToken cancellationToken) =>
            Task.Run(() => ConfirmCore(vorgangsId, cancellationToken), cancellationToken);

        private void ConfirmCore(Guid id, CancellationToken ct)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                ct.ThrowIfCancellationRequested();
                if (id != operationId) throw Invalid();
                if (_confirmed) return;

                try
                {
                    var bytes = ReadPending(pendingPath);
                    if (ParsePending(bytes) != operationId) throw Invalid();
                    ct.ThrowIfCancellationRequested();
                    File.Delete(pendingPath);
                    _confirmed = true;
                }
                catch (OperationCanceledException) { throw; }
                catch (IOException) { throw Invalid(); }
                catch (UnauthorizedAccessException) { throw Invalid(); }
                catch (SecurityException) { throw Invalid(); }
                catch (ArgumentException) { throw Invalid(); }
                catch (NotSupportedException) { throw Invalid(); }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                SafeDispose(_lockHandle);
                _lockHandle = null;
            }
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
