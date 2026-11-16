using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Versionierter Anwendungsport für Wartungen und Kilometeralarme.</summary>
public interface IWartungsverwaltung
{
    /// <summary>Lädt Bestand und gefilterte Übersicht; die Familienversion bleibt ungefiltert.</summary>
    Task<WartungsuebersichtAntwort> Laden(Wartungsfilter filter, CancellationToken ct = default);
    /// <summary>Legt eine Wartung an.</summary>
    Task<WartungsbestandAntwort> WartungAnlegen(WartungSpeichernAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Ändert eine Wartung.</summary>
    Task<WartungsbestandAntwort> WartungAendern(long id, WartungSpeichernAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Legt einen Alarm an.</summary>
    Task<WartungsbestandAntwort> AlarmAnlegen(AlarmSpeichernAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Ändert einen Alarm.</summary>
    Task<WartungsbestandAntwort> AlarmAendern(long id, AlarmSpeichernAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Löscht die ausgewählten Wartungen atomar.</summary>
    Task<WartungsbestandAntwort> Loeschen(WartungenAuswahlAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Schaltet die ausgewählten Alarme atomar um.</summary>
    Task<WartungsbestandAntwort> AlarmeUmschalten(WartungenAuswahlAnfrage anfrage, CancellationToken ct = default);
}
