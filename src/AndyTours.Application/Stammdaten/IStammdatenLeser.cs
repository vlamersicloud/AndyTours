using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Lese-Port für die Stammdaten, die Touren- und Eingabeabläufe benötigen.</summary>
public interface IStammdatenLeser
{
    /// <summary>Lädt Fahrer, Aktivitäten, Kategorien, Equipments und Trainingsbereiche.</summary>
    /// <param name="ct">Signal zum Abbrechen des Ladevorgangs.</param>
    /// <returns>Die verfügbaren Stammdaten als transportneutrale Antwort.</returns>
    Task<StammdatenAntwort> Laden(CancellationToken ct = default);
}
