using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Versionierter Anwendungsport für saisonale Aktivitätsziele.</summary>
public interface ISaisonzieleVerwaltung
{
    /// <summary>Lädt Aktivitäten mit Basiszielen und alle expliziten Saisonziele.</summary>
    Task<SaisonzieleAntwort> Laden(CancellationToken ct = default);
    /// <summary>Ersetzt explizite Saisonziele nur für die ausgewählten Jahre.</summary>
    Task<SaisonzieleAntwort> Speichern(SaisonzieleSpeichernAnfrage anfrage, CancellationToken ct = default);
}
