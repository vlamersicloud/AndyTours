using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Versionierter Port für Änderungen am Aktivitätsbestand.</summary>
public interface IAktivitaetenVerwaltung
{
    /// <summary>Lädt den geordneten Aktivitätsbestand einschließlich seiner ETag-Version.</summary>
    Task<AktivitaetenAntwort> Laden(CancellationToken ct = default);

    /// <summary>Legt eine Aktivität an, sofern die Familienversion noch aktuell ist.</summary>
    Task<AktivitaetenAntwort> Anlegen(AktivitaetSpeichernAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Ändert den Namen einer Aktivität nach Versionsprüfung.</summary>
    Task<AktivitaetenAntwort> Umbenennen(long id, AktivitaetSpeichernAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Löscht eine Aktivität nach Versionsprüfung.</summary>
    Task<AktivitaetenAntwort> Loeschen(long id, string version, CancellationToken ct = default);

    /// <summary>Setzt die Hauptaktivität nach Versionsprüfung.</summary>
    Task<AktivitaetenAntwort> SetzeHauptaktivitaet(AktivitaetAuswahlAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Setzt die vollständige Aktivitätsreihenfolge nach Versionsprüfung.</summary>
    Task<AktivitaetenAntwort> SetzeReihenfolge(AktivitaetenReihenfolgeAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Führt eine Quellaktivität in eine Zielaktivität zusammen.</summary>
    Task<AktivitaetenAntwort> Zusammenfuehren(AktivitaetenZusammenfuehrenAnfrage anfrage, CancellationToken ct = default);
}
