namespace AndyTours.Core.Berechnungen;

/// <summary>
/// Zählt, wie oft ein Eintrag einer Auswahlliste im Datenbestand verwendet wird.
/// </summary>
/// <remarks>
/// Verwendet für Rückfragen und Löschsperren der Listen „Trainingsbereich“ (Namen aus den Einheiten) und
/// „Tätigkeit“ (Namen aus den Wartungen bzw. Alarmen; der Aufrufer wählt vorher den Typ aus).
/// </remarks>
public static class ZaehleNamen
{
    /// <summary>
    /// Liefert die Anzahl der Namen, die dem gesuchten gleichen (<see cref="VergleicheNamen"/>).
    /// </summary>
    /// <param name="namen">Die Namen, z. B. die Trainingsbereiche aller Einheiten.</param>
    /// <param name="gesucht">Der gesuchte Name, z. B. „GA2“.</param>
    /// <returns>Die Anzahl (0, wenn der Name nicht verwendet wird).</returns>
    public static int Ausfuehren(IEnumerable<string> namen, string gesucht) =>
        namen.Count(n => VergleicheNamen.Ausfuehren(n, gesucht));
}
