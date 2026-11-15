namespace AndyTours.Import.Garmin;

/// <summary>Ergebnis eines kleinen Downloadschritts; fertige Dateien bleiben auch bei Netzwerkabbruch importierbar.</summary>
/// <param name="Dateien">Vollständig heruntergeladene und entpackte Aufzeichnungen.</param>
/// <param name="WeitereMoeglich">Die Liste ist noch nicht vollständig durchlaufen.</param>
/// <param name="Unterbrochen">Netzwerk, Ratenlimit oder Zeitlimit haben diesen Lauf beendet.</param>
public sealed record GarminDownloadPaket(
    IReadOnlyList<(long Id, string Pfad)> Dateien, bool WeitereMoeglich, bool Unterbrochen);
