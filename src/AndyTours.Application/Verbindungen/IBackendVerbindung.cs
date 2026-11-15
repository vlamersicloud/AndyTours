namespace AndyTours.Application.Verbindungen;

/// <summary>Prüft die Verbindung ohne Kenntnis von HTTP, Oberfläche oder Datenspeicherung.</summary>
public interface IBackendVerbindung
{
    /// <summary>Ermittelt, ob das konfigurierte Backend erreichbar ist.</summary>
    /// <param name="abbruch">Signal zum Abbrechen des Netzwerkaufrufs.</param>
    /// <returns>Wahr, wenn das Backend antwortet.</returns>
    Task<bool> IstErreichbarAsync(CancellationToken abbruch = default);
}
