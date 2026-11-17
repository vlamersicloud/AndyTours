namespace AndyTours.Application.Koordination;

/// <summary>
/// In-process admission boundary for callers that access one dataset. Acquire at the outermost
/// request/worker boundary; do not nest leases in services called under an existing lease.
/// This coordination is not cross-process locking or authorization for live restore.
/// </summary>
public interface IDatenbestandskoordination
{
    /// <summary>Enters ordinary dataset work using one permit.</summary>
    ValueTask<IDisposable> Betreten(CancellationToken cancellationToken);

    /// <summary>Enters exclusive maintenance work using all configured permits.</summary>
    ValueTask<IDisposable> ExklusivBetreten(CancellationToken cancellationToken);
}

/// <summary>Raised when configured admission capacity or queueing cannot grant a lease.</summary>
public sealed class DatenbestandBelegtException : Exception
{
    public DatenbestandBelegtException()
        : base("Datenbestand ist vorübergehend belegt.") { }
}
