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

/// <summary>Unveränderlicher Nutzungssnapshot einer kanonischen Textlistenidentität.</summary>
public sealed record TextlistenNutzungAntwort(Textlistenart Art, string ListenVersion, string Name,
    int Touren, int Wartungen, int Alarme, string VerwendungsVersion);

/// <summary>Anfrage zum Umbenennen mit vorher bestätigtem, identitätsgebundenem Nutzungssnapshot.</summary>
public sealed record TextlisteUmbenennenAnfrage(string Version, string AlterName, string NeuerName,
    string VerwendungsVersion);

/// <summary>Anfrage zum vollständigen Ersetzen der Listenreihenfolge.</summary>
public sealed record TextlisteReihenfolgeAnfrage(string Version, IReadOnlyList<string> Eintraege);
