using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Versionierter Anwendungsport für die ausdrücklich unterstützten Textlisten.</summary>
public interface ITextlistenVerwaltung
{
    /// <summary>Lädt eine unterstützte Textliste.</summary>
    Task<TextlisteAntwort> Laden(Textlistenart art, CancellationToken ct = default);
    /// <summary>Liest die Verwendung eines kanonischen Eintrags in einem read-only Snapshot.</summary>
    Task<TextlistenNutzungAntwort> NutzungLesen(Textlistenart art, string listenVersion, string name, CancellationToken ct = default);
    /// <summary>Legt einen Eintrag an.</summary>
    Task<TextlisteAntwort> Anlegen(Textlistenart art, TextlisteneintragAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Benennt einen Eintrag fachbezogen und atomar um.</summary>
    Task<TextlisteAntwort> Umbenennen(Textlistenart art, TextlisteUmbenennenAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Löscht einen Eintrag.</summary>
    Task<TextlisteAntwort> Loeschen(Textlistenart art, TextlisteneintragAnfrage anfrage, CancellationToken ct = default);
    /// <summary>Ersetzt die Reihenfolge durch die vollständige Eintragsliste.</summary>
    Task<TextlisteAntwort> SetzeReihenfolge(Textlistenart art, TextlisteReihenfolgeAnfrage anfrage, CancellationToken ct = default);
}
