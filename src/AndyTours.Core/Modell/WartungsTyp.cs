namespace AndyTours.Core.Modell;

/// <summary>
/// Art eines Eintrags in der Wartungsliste.
/// </summary>
/// <remarks>
/// Die Zahlenwerte (0 = Wartung, 1 = Alarm) werden so in der Datenbank gespeichert.
/// </remarks>
public enum WartungsTyp
{
    /// <summary>Durchgeführte Wartung (z. B. „Kettenwechsel").</summary>
    Wartung = 0,

    /// <summary>Erinnerung, die nach einer bestimmten Anzahl Kilometer ausgelöst wird.</summary>
    Alarm = 1
}
