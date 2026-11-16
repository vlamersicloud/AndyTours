using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Versionierter Port zur Verwaltung von Equipment.</summary>
public interface IEquipmentsVerwaltung
{
    /// <summary>Lädt Equipments und Familienversion.</summary>
    Task<EquipmentsAntwort> Laden(CancellationToken ct = default);

    /// <summary>Legt ein Equipment an.</summary>
    Task<EquipmentsAntwort> Anlegen(EquipmentSpeichernAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Ändert ein Equipment.</summary>
    Task<EquipmentsAntwort> Aendern(long id, EquipmentSpeichernAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Löscht ein Equipment.</summary>
    Task<EquipmentsAntwort> Loeschen(long id, string version, CancellationToken ct = default);
}
