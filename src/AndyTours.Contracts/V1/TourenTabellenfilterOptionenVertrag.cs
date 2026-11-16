namespace AndyTours.Contracts.V1;

/// <summary>Optionen für tabellenspezifische Textfilter; Einträge sind Namen, keine Ressourcenkennungen.</summary>
/// <param name="Equipments">Equipment-Anzeigenamen gemäß der Core-Auswahlregel.</param>
/// <param name="Trainingsbereiche">Trainingsbereichstexte.</param>
/// <remarks>
/// Die Optionen stammen aus der vollständigen Datums-/Aktivitätsauswahl vor Kategorie- und Tabellenfiltern sowie Paging.
/// Die führende Core-Auswahl „(alle)“ wird ausgelassen; die Oberfläche bildet „alle“ explizit als null ab.
/// Ein zuvor gewählter Name ohne aktuellen Treffer bleibt enthalten. Equipmentnamen und Reihenfolge folgen der Core-Regel,
/// einschließlich Ausmusterungszusatz. Die Werte sind Filterkriterien, keine IDs oder Zuordnungen.
/// Der Vertrag enthält keine WPF-, Core- oder Datenbanktypen.
/// </remarks>
public sealed record TourenTabellenfilterOptionenAntwort(
    IReadOnlyList<string> Equipments,
    IReadOnlyList<string> Trainingsbereiche);
