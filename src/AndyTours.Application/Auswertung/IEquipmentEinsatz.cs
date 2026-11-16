using AndyTours.Contracts.V1;

namespace AndyTours.Application.Auswertung;

/// <summary>Read-only Zugriff auf die Equipment-Einsatz-Auskunft.</summary>
public interface IEquipmentEinsatz
{
    /// <summary>Lädt die Anzeigezeilen ohne Änderungen am Equipment- oder Wartungsbestand.</summary>
    Task<EquipmentEinsatzAntwort> Laden(CancellationToken ct = default);
}
