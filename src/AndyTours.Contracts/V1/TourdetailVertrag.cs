namespace AndyTours.Contracts.V1;

/// <summary>Anfrage für geordnete Tourdetails; IDs sind Datenbank-IDs, keine Dateipfade.</summary>
public sealed record TourdetailAnfrage(IReadOnlyList<long> TourIds);

/// <summary>Geografische Position eines echten FIT-Messpunkts.</summary>
public sealed record TourdetailPosition(double Breite, double Laenge);

/// <summary>
/// FIT-Messpunkt. ZeitpunktUtc ist der tatsächliche UTC-Zeitstempel; FahrzeitMillisekunden ist aktive
/// Timerzeit ohne Timerpausen. Nicht vorhandene Messungen bleiben null; Puls wird weder interpoliert
/// noch werden Positionen erfunden.
/// </summary>
public sealed record TourdetailMesspunkt(
    DateTimeOffset? ZeitpunktUtc,
    double DistanzKm,
    long FahrzeitMillisekunden,
    double? Hoehe,
    int? Puls,
    double? Geschwindigkeit,
    int? Trittfrequenz,
    int? Temperatur,
    TourdetailPosition? Position);

/// <summary>Tour und optionaler FIT-Verlauf; null Verlauf bewahrt Lücken in Mehrtagestouren.</summary>
public sealed record TourdetailEtappeAntwort(
    TourAntwort Tour,
    string? DateiVersion,
    IReadOnlyList<TourdetailMesspunkt>? Verlauf,
    string? Warncode);

/// <summary>Geordnete Tourdetails mit opaker Repräsentationsversion, die keine Änderungsberechtigung erteilt.</summary>
public sealed record TourdetailAntwort(string Version, IReadOnlyList<TourdetailEtappeAntwort> Etappen);
