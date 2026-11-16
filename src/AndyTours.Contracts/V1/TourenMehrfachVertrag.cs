namespace AndyTours.Contracts.V1;

/// <summary>Optimistische Versionsreferenz einer Tour in einer expliziten Mehrfachaktion.</summary>
public sealed record TourVersionsreferenz(long Id, string Version);

/// <summary>Fordert das gezielte Setzen und Entfernen von Kategorien bei mehreren Touren an.</summary>
/// <param name="AnfrageId">Stabile Kennung für Wiederholung derselben Anfrage nach unklarem ACK.</param>
/// <param name="Touren">Eindeutige Tour-IDs samt beim Lesen erhaltener Version.</param>
/// <param name="Setzen">Kategorien, die jede Tour danach zusätzlich haben soll.</param>
/// <param name="Entfernen">Kategorien, die von jeder Tour entfernt werden sollen.</param>
public sealed record TourenKategorienAnfrage(
    Guid AnfrageId,
    IReadOnlyList<TourVersionsreferenz> Touren,
    IReadOnlyList<long> Setzen,
    IReadOnlyList<long> Entfernen);

/// <summary>Fordert eine gemeinsame Aktivitätsänderung an; vorhandenes Equipment bleibt unberührt.</summary>
/// <param name="AnfrageId">Stabile Kennung für Wiederholung derselben Anfrage nach unklarem ACK.</param>
/// <param name="Touren">Eindeutige Tour-IDs samt erwarteter Version.</param>
/// <param name="AktivitaetId">Neue Aktivität.</param>
public sealed record TourenAktivitaetAnfrage(
    Guid AnfrageId,
    IReadOnlyList<TourVersionsreferenz> Touren,
    long AktivitaetId);

/// <summary>Fordert eine gemeinsame Equipmentänderung an; null entfernt das Equipment.</summary>
/// <param name="AnfrageId">Stabile Kennung für Wiederholung derselben Anfrage nach unklarem ACK.</param>
/// <param name="Touren">Eindeutige Tour-IDs samt erwarteter Version.</param>
/// <param name="EquipmentId">Neues Equipment oder null.</param>
public sealed record TourenEquipmentAnfrage(
    Guid AnfrageId,
    IReadOnlyList<TourVersionsreferenz> Touren,
    long? EquipmentId);

/// <summary>Fordert das versionierte Löschen mehrerer Touren an.</summary>
/// <param name="AnfrageId">Stabile Kennung für Wiederholung derselben Anfrage nach unklarem ACK.</param>
/// <param name="Touren">Eindeutige Tour-IDs samt erwarteter Version.</param>
public sealed record TourenLoeschenAnfrage(
    Guid AnfrageId,
    IReadOnlyList<TourVersionsreferenz> Touren);

/// <summary>Antwort einer atomaren Mehrfachaktion: entweder aktualisierte Touren oder gelöschte IDs.</summary>
/// <param name="AnfrageId">Kennung der ausgeführten Anfrage.</param>
/// <param name="Eintraege">Aktualisierte Touren; bei Löschung leer.</param>
/// <param name="GeloeschteIds">Gelöschte IDs; bei Änderungen leer.</param>
public sealed record TourenMehrfachAntwort(
    Guid AnfrageId,
    IReadOnlyList<TourAntwort> Eintraege,
    IReadOnlyList<long> GeloeschteIds);
