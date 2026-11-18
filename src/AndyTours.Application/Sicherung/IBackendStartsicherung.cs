namespace AndyTours.Application.Sicherung;

using AndyTours.Contracts.V1;

/// <summary>Reads and atomically advances only the backend-start backup counter.</summary>
public interface IBackendStartsicherungsZaehler
{
    Task<BackendStartsicherungsStand> ZaehlenUndErhoehen(CancellationToken cancellationToken);
    Task<bool> Zuruecksetzen(int erwarteterStand, CancellationToken cancellationToken);
}

/// <summary>Supplies the existing full-archive writer after the caller holds the exclusive data lease.</summary>
public interface IBackendStartsicherungsArchiv
{
    ISicherungsarchivErstellung? Ermittle(CancellationToken cancellationToken);
}

/// <summary>Atomic policy/counter/unit-count snapshot after one backend start has been counted.</summary>
public sealed record BackendStartsicherungsStand(int Intervall, int StartsSeitSicherung, int Trainingseinheiten);

public enum BackendStartsicherungsStatus
{
    NichtVerfuegbar,
    NichtFaellig,
    Fehlgeschlagen,
    Gesichert
}

public sealed record BackendStartsicherungsErgebnis(BackendStartsicherungsStatus Status);
