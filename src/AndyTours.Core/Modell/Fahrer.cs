namespace AndyTours.Core.Modell;

/// <summary>
/// Ein Sportler, dessen Trainingseinheiten verwaltet werden.
/// </summary>
/// <remarks>
/// AndyTours legt die Sportler in einer Tabelle ab und ordnet jede Trainingseinheit über
/// <see cref="Trainingseinheit.FahrerId"/> einem Sportler zu. Die persönlichen Daten werden im Fenster
/// „Persönliche Daten“ gepflegt und sonst nicht ausgewertet.
/// </remarks>
public class Fahrer
{
    /// <summary>Eindeutige Nummer in der Datenbank.</summary>
    public long Id { get; set; }

    /// <summary>Vorname.</summary>
    public string Vorname { get; set; } = string.Empty;

    /// <summary>Nachname.</summary>
    public string Nachname { get; set; } = string.Empty;

    /// <summary>Geburtstag, falls bekannt.</summary>
    public DateOnly? Geburtstag { get; set; }

    /// <summary>Körpergröße in Zentimetern.</summary>
    public int GroesseCm { get; set; }

    /// <summary>Aktuelles Körpergewicht in Kilogramm.</summary>
    public double GewichtKg { get; set; }

    /// <summary>Ruhepuls in Schlägen pro Minute (in den Altdaten: <c>pulsemin</c>).</summary>
    public int Ruhepuls { get; set; }

    /// <summary>Maximalpuls in Schlägen pro Minute (in den Altdaten: <c>pulsemax</c>).</summary>
    public int Maxpuls { get; set; }

    /// <summary>Name des Heimatorts.</summary>
    public string Heimatort { get; set; } = string.Empty;
}
