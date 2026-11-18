using AndyTours.Contracts.V1;

namespace AndyTours.Application.Touren;

/// <summary>Ermittelt in einem konsistenten Lesesnapshot alle Treffer eines explizit begrenzten Filters.</summary>
public interface ITourenFilterauswahl
{
    /// <summary>Wirft TourenFilterauswahlLimitException, wenn die Trefferzahl die konfigurierte Grenze überschreitet.</summary>
    Task<IReadOnlyList<TourAntwort>> AlleTreffer(
        TourenFilterauswahlAnfrage filter, int maximaleTouren, CancellationToken ct = default);
}

/// <summary>Ein Filtertreffer-Snapshot überschreitet die konfigurierte Touranzahl.</summary>
public sealed class TourenFilterauswahlLimitException : Exception { }
