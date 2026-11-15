using AndyTours.Contracts.V1;

namespace AndyTours.Application.Auswertung;

/// <summary>Lädt eine nach Zeitraum und optionalen Stammdaten gefilterte Aktivitätsauswertung.</summary>
public interface IAktivitaetsauswertung
{
    /// <summary>Berechnet Gesamtkennzahlen und gruppierte Perioden für eine Anfrage.</summary>
    /// <param name="anfrage">Zeitraum, Gruppierungsraster und optionale Filter.</param>
    /// <param name="ct">Signal zum Abbrechen der Anfrage.</param>
    /// <returns>Die aggregierte Aktivitätsauswertung.</returns>
    Task<AktivitaetsauswertungAntwort> Laden(
        AktivitaetsauswertungAnfrage anfrage,
        CancellationToken ct = default);
}
