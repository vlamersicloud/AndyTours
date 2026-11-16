namespace AndyTours.Contracts.V1;

/// <summary>Geschlossene Menge der verwaltbaren, fachlich typisierten Textlisten.</summary>
public enum Textlistenart
{
    /// <summary>Trainingsbereiche von Touren.</summary>
    Trainingsbereich,
    /// <summary>Tätigkeiten für Wartungen und Alarme.</summary>
    Taetigkeit
}

/// <summary>Versionierter Inhalt einer einzelnen Textliste.</summary>
public sealed record TextlisteAntwort(Textlistenart Art, string Version, IReadOnlyList<string> Eintraege);

/// <summary>Anfrage zum Anlegen oder Löschen eines Listeneintrags.</summary>
public sealed record TextlisteneintragAnfrage(string Version, string Name);

/// <summary>Anfrage zum Umbenennen eines Listeneintrags.</summary>
public sealed record TextlisteUmbenennenAnfrage(string Version, string AlterName, string NeuerName);

/// <summary>Anfrage zum vollständigen Ersetzen der Listenreihenfolge.</summary>
public sealed record TextlisteReihenfolgeAnfrage(string Version, IReadOnlyList<string> Eintraege);
