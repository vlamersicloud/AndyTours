using AndyTours.Core.Modell;

namespace AndyTours.Core.Berechnungen;

/// <summary>
/// Ersetzt die Standardkategorie der Equipments im Speicher.
/// </summary>
/// <remarks>
/// Die Standardkategorie eines Equipments ist als Name gespeichert. Beim Umbenennen und Zusammenführen von
/// Kategorien muss sie deshalb eigens mitgezogen werden; verglichen wird mit <see cref="VergleicheNamen"/>.
/// </remarks>
public static class ErsetzeStandardkategorie
{
    /// <summary>
    /// Trägt bei allen Equipments mit der alten Standardkategorie die neue ein.
    /// </summary>
    /// <param name="equipments">Alle Equipments; sie werden direkt geändert.</param>
    /// <param name="alt">Bisheriger Name der Kategorie.</param>
    /// <param name="neu">Neuer Name.</param>
    /// <returns>Anzahl der geänderten Equipments.</returns>
    public static int Ausfuehren(IEnumerable<Equipment> equipments, string alt, string neu)
    {
        int anzahl = 0;
        foreach (Equipment equipment in equipments)
        {
            if (VergleicheNamen.Ausfuehren(equipment.StandardKategorie, alt))
            {
                equipment.StandardKategorie = neu;
                anzahl++;
            }
        }

        return anzahl;
    }
}
