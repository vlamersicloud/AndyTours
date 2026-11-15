namespace AndyTours.Contracts.V1;

/// <summary>Transportdaten des Fahrers.</summary>
/// <param name="Id">Eindeutige Fahrerkennung.</param>
/// <param name="Vorname">Vorname.</param>
/// <param name="Nachname">Nachname.</param>
/// <param name="Geburtstag">Geburtstag, falls bekannt.</param>
/// <param name="GroesseCm">Körpergröße in Zentimetern.</param>
/// <param name="GewichtKg">Körpergewicht in Kilogramm.</param>
/// <param name="Ruhepuls">Ruhepuls in Schlägen pro Minute.</param>
/// <param name="Maxpuls">Maximalpuls in Schlägen pro Minute.</param>
/// <param name="Heimatort">Name des Heimatorts.</param>
public sealed record FahrerAntwort(
    long Id,
    string Vorname,
    string Nachname,
    DateOnly? Geburtstag,
    int GroesseCm,
    double GewichtKg,
    int Ruhepuls,
    int Maxpuls,
    string Heimatort);

/// <summary>Transportdaten einer Aktivität einschließlich Grundzielen.</summary>
/// <param name="Id">Eindeutige Aktivitätskennung.</param>
/// <param name="Name">Anzeigename der Aktivität.</param>
/// <param name="IstHaupt">Gibt an, ob die Aktivität standardmäßig vorausgewählt wird.</param>
/// <param name="Geschwindigkeitseinheit">Geschwindigkeitseinheit, etwa km/h oder min/km.</param>
/// <param name="ZielKm">Grundziel der Distanz in Kilometern.</param>
/// <param name="ZielHm">Grundziel der Höhenmeter.</param>
/// <param name="ZielAnzahl">Grundziel der Anzahl an Trainingseinheiten.</param>
public sealed record AktivitaetAntwort(
    long Id,
    string Name,
    bool IstHaupt,
    string Geschwindigkeitseinheit,
    double ZielKm,
    double ZielHm,
    int ZielAnzahl);

/// <summary>Transportdaten einer Kategorie.</summary>
/// <param name="Id">Eindeutige Kategoriekennung.</param>
/// <param name="Name">Kategoriename.</param>
/// <param name="IstStandard">Gibt an, ob die Kategorie für neue Einheiten vorgeschlagen wird.</param>
public sealed record KategorieAntwort(long Id, string Name, bool IstStandard);

/// <summary>Transportdaten eines Equipments.</summary>
/// <param name="Id">Eindeutige Equipmentkennung.</param>
/// <param name="Name">Anzeigename des Equipments.</param>
/// <param name="AktivitaetId">Zugeordnete Aktivität, falls vorhanden.</param>
/// <param name="Geraetezuordnung">Gerätekennung für lokale Importzuordnungen.</param>
/// <param name="StandardKategorie">Vorgeschlagene Standardkategorie.</param>
/// <param name="IstStandard">Gibt an, ob das Equipment standardmäßig ausgewählt wird.</param>
/// <param name="Ausgemustert">Gibt an, ob das Equipment ausgemustert ist.</param>
/// <param name="Anschaffungsdatum">Anschaffungsdatum, falls bekannt.</param>
/// <param name="AnfangsdistanzKm">Anfangsdistanz in Kilometern.</param>
public sealed record EquipmentAntwort(
    long Id,
    string Name,
    long? AktivitaetId,
    string Geraetezuordnung,
    string StandardKategorie,
    bool IstStandard,
    bool Ausgemustert,
    DateOnly? Anschaffungsdatum,
    double AnfangsdistanzKm);

/// <summary>Zusammenstellung der für Bearbeitungsdialoge benötigten Stammdaten.</summary>
/// <param name="Fahrer">Fahrerdaten.</param>
/// <param name="Aktivitaeten">Aktivitäten und ihre Grundziele.</param>
/// <param name="Kategorien">Kategorien.</param>
/// <param name="Equipments">Equipments.</param>
/// <param name="Trainingsbereiche">Verfügbare Trainingsbereichskürzel.</param>
public sealed record StammdatenAntwort(
    IReadOnlyList<FahrerAntwort> Fahrer,
    IReadOnlyList<AktivitaetAntwort> Aktivitaeten,
    IReadOnlyList<KategorieAntwort> Kategorien,
    IReadOnlyList<EquipmentAntwort> Equipments,
    IReadOnlyList<string> Trainingsbereiche);
