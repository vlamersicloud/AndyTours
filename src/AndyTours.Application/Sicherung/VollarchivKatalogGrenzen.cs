namespace AndyTours.Application.Sicherung;

/// <summary>
/// Explizite serverseitige Grenzen für die Vollarchiv-Katalogprüfung; Anzahl und aggregierte entpackte Nutzdatenbytes.
/// Sie begrenzen nicht pauschal alle komprimierten Betriebssystem-Lesezugriffe, die zusätzlich durch Archivanzahl mal
/// MaxArchivBytes beschränkt bleiben. Ohne Defaults und niemals clientgesteuert.
/// </summary>
public sealed record VollarchivKatalogGrenzen(
    SicherungsarchivGrenzen Archiv,
    int MaxVollarchive,
    long MaxNutzdatenBytes);
