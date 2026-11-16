namespace AndyTours.Application.Garmin;

/// <summary>Secret-bearing server context; never serialize to HTTP or write to logs.</summary>
public sealed record GarminImportServerkontext(string Schluessel, IReadOnlySet<long> BereitsImportiert)
{
    public override string ToString() => "[Garmin-Importserverkontext redigiert]";
}

/// <summary>Server-only access to the configured key and durable import deduplication set.</summary>
public interface IGarminImportServerdaten
{
    Task<GarminImportServerkontext> Laden(CancellationToken ct = default);
}
