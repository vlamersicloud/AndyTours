using AndyTours.Contracts.V1;

namespace AndyTours.Application.Touren;

/// <summary>Port für explizite, versionierte Mehrfachaktionen auf Touren.</summary>
/// <remarks>Implementierungen müssen alle referenzierten Versionen atomar prüfen und dürfen keinen Vollbestand überschreiben.</remarks>
public interface ITourenMehrfachverwaltung
{
    /// <summary>Setzt und entfernt die angegebenen Kategorien atomar.</summary>
    Task<TourenMehrfachAntwort> Kategorien(TourenKategorienAnfrage anfrage, CancellationToken cancellationToken);

    /// <summary>Ändert die Aktivität aller referenzierten Touren atomar.</summary>
    Task<TourenMehrfachAntwort> Aktivitaet(TourenAktivitaetAnfrage anfrage, CancellationToken cancellationToken);

    /// <summary>Setzt oder entfernt das Equipment aller referenzierten Touren atomar.</summary>
    Task<TourenMehrfachAntwort> Equipment(TourenEquipmentAnfrage anfrage, CancellationToken cancellationToken);

    /// <summary>Löscht alle referenzierten Touren atomar.</summary>
    Task<TourenMehrfachAntwort> Loeschen(TourenLoeschenAnfrage anfrage, CancellationToken cancellationToken);
}
