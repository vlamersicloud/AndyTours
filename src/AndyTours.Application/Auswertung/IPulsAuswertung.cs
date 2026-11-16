using AndyTours.Contracts.V1;

namespace AndyTours.Application.Auswertung;

/// <summary>
/// Liest die aktuelle Pulsreferenz, aktualisiert sie nur auf ausdrückliche Anforderung
/// und berechnet eine gefilterte Pulsauswertung ohne impliziten Garmin-Abruf.
/// </summary>
public interface IPulsAuswertung
{
    Task<PulsReferenzAntwort> LadenReferenz(CancellationToken cancellationToken);

    Task<PulsReferenzAntwort> AktualisierenReferenz(
        PulsReferenzAktualisierenAnfrage anfrage,
        CancellationToken cancellationToken);

    Task<PulsAuswertungAntwort> Auswerten(
        PulsAuswertungsAnfrage anfrage,
        CancellationToken cancellationToken);
}
