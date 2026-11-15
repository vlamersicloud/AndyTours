namespace AndyTours.Daten;

/// <summary>
/// Die Sicherung vor dem Umbau einer älteren Datenbank ist gescheitert; die Datenbank wurde nicht umgebaut.
/// </summary>
/// <remarks>
/// Ohne Sicherung baut AndyTours einen älteren Datenbestand nicht um (siehe
/// <c>docs/adr/0010-anpassung-des-datenbankschemas.md</c>). Die Oberfläche zeigt <see cref="Exception.Message"/>
/// an; beim nächsten Öffnen wird es erneut versucht.
/// </remarks>
/// <param name="sicherungsordner">Der Ordner, in den gesichert werden sollte.</param>
/// <param name="grund">Verständlicher Grund des Fehlschlags.</param>
/// <param name="fehler">Der ursprüngliche Fehler.</param>
public class SicherungVorUmbauFehlgeschlagenException(string sicherungsordner, string grund, Exception fehler)
    : Exception(
        "Der Datenbestand stammt von einer früheren Version von AndyTours und muss angepasst werden. " +
        $"Vorher wird er gesichert, doch die Sicherung ist fehlgeschlagen.\n\nSicherungsordner:\n{sicherungsordner}\n\n{grund}",
        fehler)
{
    /// <summary>Der Ordner, in den gesichert werden sollte.</summary>
    public string Sicherungsordner { get; } = sicherungsordner;
}
