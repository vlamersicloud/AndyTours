namespace AndyTours.Core.Modell;

/// <summary>
/// Ein Ausrüstungsgegenstand, mit dem Trainingseinheiten absolviert werden – meist ein Fahrrad,
/// bei der Aktivität „Laufen" ein Paar Laufschuhe.
/// </summary>
public class Equipment
{
    /// <summary>Eindeutige Nummer in der Datenbank.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Name des Equipments, z. B. „Genius/Bike". Der Teil hinter einem Schrägstrich ist in
    /// den Altdaten ein Namenszusatz; manche Altdateien verwenden nur den Teil davor.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Aktivität, für die das Equipment verwendet wird (Verweis auf <see cref="Aktivitaet.Id"/>).</summary>
    public long? AktivitaetId { get; set; }

    /// <summary>
    /// Gerätezuordnung aus den Altdaten (<c>assignment</c>, z. B. „Bike1"): Kennung, unter der
    /// ein Radcomputer dieses Rad führt. Wird für die automatische Zuordnung beim Import genutzt.
    /// </summary>
    public string Geraetezuordnung { get; set; } = string.Empty;

    /// <summary>Kategorie, die bei neuen Einheiten mit diesem Equipment vorgeschlagen wird (leer = keine).</summary>
    public string StandardKategorie { get; set; } = string.Empty;

    /// <summary><c>true</c>, wenn dieses Equipment bei neuen Einheiten vorausgewählt wird.</summary>
    public bool IstStandard { get; set; }

    /// <summary><c>true</c>, wenn das Equipment nicht mehr benutzt wird („ausgemustert").</summary>
    public bool Ausgemustert { get; set; }

    /// <summary>Anschaffungsdatum, falls bekannt.</summary>
    public DateOnly? Anschaffungsdatum { get; set; }

    /// <summary>
    /// Kilometerstand bei Übernahme in die Verwaltung (in den Altdaten: <c>initDst</c>); wird zur
    /// Gesamtlaufleistung aus den Trainingseinheiten addiert.
    /// </summary>
    public double AnfangsdistanzKm { get; set; }
}
