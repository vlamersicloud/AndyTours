namespace AndyTours.Application.Planung;

/// <summary>Server-side stored relative FIT paths. Never expose these paths through public HTTP contracts.</summary>
public interface IPlanungsStartQuelldaten
{
    Task<IReadOnlyList<string>> Laden(CancellationToken cancellationToken);
}
