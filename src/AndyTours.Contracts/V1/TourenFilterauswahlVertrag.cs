namespace AndyTours.Contracts.V1;

/// <summary>Exakte Identität der angewandten Filter für eine explizite Auswahl aller Treffer.</summary>
public sealed record TourenFilterauswahlAnfrage(
    DateOnly? Von,
    DateOnly? Bis,
    long? AktivitaetId,
    long? KategorieId,
    TourenTabellenfilter? Tabellenfilter);

/// <summary>Begrenzter Snapshot der Tourversionen und des angewandten Filters.</summary>
public sealed record TourenFilterauswahlAntwort(
    TourenFilterauswahlAnfrage Filter,
    IReadOnlyList<TourAntwort> Touren);
