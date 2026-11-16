using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Versionierter Port für den Kategorienbestand.</summary>
public interface IKategorienVerwaltung
{
    /// <summary>Lädt Kategorien und Familienversion.</summary>
    Task<KategorienAntwort> Laden(CancellationToken ct = default);

    /// <summary>Legt eine Kategorie an.</summary>
    Task<KategorienAntwort> Anlegen(KategorieSpeichernAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Ändert den Namen einer Kategorie.</summary>
    Task<KategorienAntwort> Umbenennen(long id, KategorieSpeichernAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Löscht eine nicht verwendete Kategorie.</summary>
    Task<KategorienAntwort> Loeschen(long id, string version, CancellationToken ct = default);

    /// <summary>Setzt oder entfernt das Standardkennzeichen einer Kategorie.</summary>
    Task<KategorienAntwort> SetzeStandard(KategorieStandardAnfrage anfrage, CancellationToken ct = default);

    /// <summary>Führt eine Quellkategorie in eine Zielkategorie zusammen.</summary>
    Task<KategorienAntwort> Zusammenfuehren(KategorienZusammenfuehrenAnfrage anfrage, CancellationToken ct = default);
}
