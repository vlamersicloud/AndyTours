namespace AndyTours.Application.Sicherung;

using AndyTours.Contracts.V1;

/// <summary>Downloads one selected catalog entry into a caller-owned temporary stream and verifies it before success.</summary>
public interface ISicherungsdownloadClient
{
    Task Download(SicherungAntwort selected, Stream destination, CancellationToken cancellationToken);
}
