namespace AndyTours.Daten.Datensicherung;

/// <summary>Strongly retains an unconfirmed checker child, its readers, and optional private staging.</summary>
internal sealed class SqlitePruefQuarantaeneintrag
{
    public Guid Id { get; } = Guid.NewGuid();
    public IPruefprozess Child { get; }
    public Task[] Readers { get; }
    public CancellationTokenSource ReaderCancellation { get; }
    public BereitgestelltePruefdatenbank? Stage { get; private set; }

    public SqlitePruefQuarantaeneintrag(IPruefprozess child, Task stdout, Task stderr, BereitgestelltePruefdatenbank? stage, CancellationTokenSource readerCancellation)
    {
        Child = child;
        Readers = [stdout, stderr];
        Stage = stage;
        ReaderCancellation = readerCancellation;
    }

    public bool BereinigenWennSicher()
    {
        try
        {
            if (!Child.HasExited || Readers.Any(task => !task.IsCompleted)) return false;
            ReaderCancellation.Dispose();
            Child.Dispose();
            Stage?.Dispose();
            Stage = null;
            return true;
        }
        catch { return false; }
    }
}

/// <summary>Process-lifetime strong owner for resources whose child exit has not been confirmed.</summary>
internal static class SqlitePruefQuarantaene
{
    private static readonly object Sync = new();
    private static readonly Dictionary<Guid, SqlitePruefQuarantaeneintrag> Entries = new();

    public static Guid Uebernehmen(SqlitePruefQuarantaeneintrag entry)
    {
        lock (Sync) { Entries.Add(entry.Id, entry); return entry.Id; }
    }

    public static bool Haelt(BereitgestelltePruefdatenbank stage)
    {
        lock (Sync) return Entries.Values.Any(entry => ReferenceEquals(entry.Stage, stage));
    }

    public static async Task<bool> BereinigeStageWennSicherAsync(BereitgestelltePruefdatenbank stage, CancellationToken cancellationToken)
    {
        SqlitePruefQuarantaeneintrag? entry;
        Guid id;
        lock (Sync)
        {
            var pair = Entries.FirstOrDefault(item => ReferenceEquals(item.Value.Stage, stage));
            if (pair.Value is null) return false;
            id = pair.Key; entry = pair.Value;
        }
        try { await Task.WhenAll(entry.Readers).WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { /* observe reader failures; completed readers are safe to dispose after confirmed child exit */ }
        return BereinigenWennSicher(id);
    }

    public static bool BereinigeStageWennSicher(BereitgestelltePruefdatenbank stage)
    {
        lock (Sync)
        {
            var pair = Entries.FirstOrDefault(item => ReferenceEquals(item.Value.Stage, stage));
            if (pair.Value is null || !pair.Value.BereinigenWennSicher()) return false;
            Entries.Remove(pair.Key);
            return true;
        }
    }

    public static bool BereinigenWennSicher(Guid id)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(id, out var entry) || !entry.BereinigenWennSicher()) return false;
            Entries.Remove(id);
            return true;
        }
    }
}

internal sealed class SqlitePruefQuarantaeneException(Guid quarantineId) : Exception("SQLite checker process exit could not be confirmed.")
{
    public Guid QuarantineId { get; } = quarantineId;
}
