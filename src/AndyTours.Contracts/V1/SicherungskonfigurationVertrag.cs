namespace AndyTours.Contracts.V1;

/// <summary>Lesbare Sicherungsrichtlinie und serververwalteter Startzähler des Datenbestands.</summary>
/// <param name="Version">Opaque CAS-Version ausschließlich für Richtlinienänderungen.</param>
/// <param name="Intervall">Sicherung nach 1 bis 99 Programmstarts des Backends.</param>
/// <param name="Hoechstzahl">Maximal 1 bis 99 aufzubewahrende Sicherungen.</param>
/// <param name="StartsSeitSicherung">Serververwalteter Zähler; unabhängig von der Richtlinienversion und nicht schreibbar durch Clients.</param>
public sealed record SicherungskonfigurationAntwort(
    string Version,
    int Intervall,
    int Hoechstzahl,
    int StartsSeitSicherung);

/// <summary>Änderung ausschließlich der Sicherungsrichtlinie, geschützt durch CAS-Version.</summary>
/// <param name="Version">Opaque, zuvor gelesene Richtlinienversion.</param>
/// <param name="Intervall">Sicherung nach 1 bis 99 Programmstarts des Backends, nicht Clientverbindungen.</param>
/// <param name="Hoechstzahl">Maximal 1 bis 99 aufzubewahrende Sicherungen.</param>
/// <remarks>
/// Der Startzähler ist absichtlich nicht schreibbar. Sicherungsordner, Dateipfade, Zugangsdaten
/// und beliebige Konfigurationswerte gehören nicht zu diesem Vertrag. Legacy-Standardwerte bleiben 3 Starts und 10 Sicherungen.
/// </remarks>
public sealed record SicherungskonfigurationAnfrage(
    string Version,
    int Intervall,
    int Hoechstzahl);
