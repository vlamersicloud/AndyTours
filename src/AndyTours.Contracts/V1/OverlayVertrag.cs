namespace AndyTours.Contracts.V1;

/// <summary>
/// Farbwerte der Overlay-API. Namen und Reihenfolge entsprechen der bestehenden Oberfläche;
/// dieser Vertrag referenziert keine Core- oder WPF-Typen.
/// </summary>
public enum OverlayFarbe
{
    Violett,
    Blau,
    Hellblau,
    Tuerkis,
    Gruen,
    Gelb,
    Orange,
    Rot,
    Pink,
    Braun,
    Grau,
    Schwarz
}

/// <summary>Ordner im Overlay-Bestand; null als Eltern-ID bezeichnet die oberste Ebene.</summary>
public sealed record OverlayOrdnerAntwort(long Id, long? ElternId, string Name);

/// <summary>
/// Änderbare Overlay-Metadaten. Version ist die opake Versionsmarke dieses Eintrags;
/// der Bestand führt zusätzlich eine aggregierte CAS-Version für strukturelle Änderungen.
/// </summary>
public sealed record OverlayMetadatenAntwort(
    long Id,
    long? OrdnerId,
    string Name,
    OverlayFarbe? Farbe,
    bool IstEingeschaltet,
    string Version);

/// <summary>
/// Metadaten-Snapshot mit aggregierter CAS-Version. Punkte werden separat geladen,
/// damit Baum-/Listenabfragen keine großen Streckendaten übertragen.
/// </summary>
public sealed record OverlayBestandAntwort(
    string Version,
    OverlayFarbe GemeinsameFarbe,
    IReadOnlyList<OverlayOrdnerAntwort> Ordner,
    IReadOnlyList<OverlayMetadatenAntwort> Overlays);

/// <summary>Geografische Position eines Overlay-Punktes in Grad.</summary>
public sealed record OverlayPosition(double Breite, double Laenge);

/// <summary>
/// Separat geladener Inhalt eines Overlays. Abschnitte und einzelne Punkte bleiben
/// in ihrer Reihenfolge erhalten; Version ist die opake Inhalts-/Eintragsversion.
/// </summary>
public sealed record OverlayInhaltAntwort(
    long Id,
    string Version,
    IReadOnlyList<IReadOnlyList<OverlayPosition>> Abschnitte);
