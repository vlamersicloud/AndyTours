namespace AndyTours.Contracts.V1;

/// <summary>Transportwerte einer Tour ohne Datenbank-, Fachmodell- oder Dateisystemtypen.</summary>
/// <param name="FahrerId">Kennung des Fahrers.</param>
/// <param name="Datum">Kalendertag der Tour.</param>
/// <param name="Startzeit">Optionale Startzeit.</param>
/// <param name="Name">Anzeigename.</param>
/// <param name="FahrzeitSekunden">Fahrzeit in Sekunden.</param>
/// <param name="DistanzKm">Distanz in Kilometern.</param>
/// <param name="Hoehenmeter">Aufstieg in Metern.</param>
/// <param name="MaxHoehe">Maximale Höhe in Metern.</param>
/// <param name="StartHoehe">Start-Höhe in Metern.</param>
/// <param name="MaxGeschwindigkeit">Maximalgeschwindigkeit in km/h.</param>
/// <param name="HfDurchschnitt">Durchschnittspuls.</param>
/// <param name="HfMax">Maximalpuls.</param>
/// <param name="HfMin">Minimalpuls.</param>
/// <param name="Trittfrequenz">Trittfrequenz.</param>
/// <param name="Temperatur">Temperatur in Grad Celsius.</param>
/// <param name="LeistungWatt">Leistung in Watt.</param>
/// <param name="KoerpergewichtKg">Körpergewicht in kg.</param>
/// <param name="Erholungspuls">Erholungspuls.</param>
/// <param name="Trainingsbereich">Trainingsbereich.</param>
/// <param name="Kommentar">Kommentar.</param>
/// <param name="AktivitaetId">Aktivitätskennung.</param>
/// <param name="EquipmentId">Optionale Equipmentkennung.</param>
/// <param name="KategorieIds">Zugeordnete Kategoriekennungen.</param>
public sealed record TourWerte(
    long FahrerId,
    DateOnly Datum,
    TimeOnly? Startzeit,
    string Name,
    long FahrzeitSekunden,
    double DistanzKm,
    int Hoehenmeter,
    int MaxHoehe,
    int StartHoehe,
    double MaxGeschwindigkeit,
    int HfDurchschnitt,
    int HfMax,
    int HfMin,
    int Trittfrequenz,
    int Temperatur,
    double LeistungWatt,
    double KoerpergewichtKg,
    int Erholungspuls,
    string Trainingsbereich,
    string Kommentar,
    long AktivitaetId,
    long? EquipmentId,
    long[] KategorieIds);

/// <summary>Tour mit opaque Versionskennung für optimistische Änderungen.</summary>
public sealed record TourAntwort(long Id, string Version, TourWerte Werte);

/// <summary>Seite einer gefilterten Tourenliste.</summary>
public sealed record TourListeAntwort(IReadOnlyList<TourAntwort> Eintraege, int Gesamt, int Start, int Anzahl);

/// <summary>Änderungsanforderung mit der beim Lesen erhaltenen Versionskennung.</summary>
public sealed record TourAendernAnfrage(string Version, TourWerte Werte);
/// <summary>Optionale Datums-, Aktivitäts- und Kategorienfilter der Tourenliste.</summary>
/// <param name="Von">Einschließendes Startdatum.</param>
/// <param name="Bis">Einschließendes Enddatum.</param>
/// <param name="AktivitaetId">Optionale Aktivitätskennung.</param>
/// <param name="KategorieId">Optionale Kategorienkennung.</param>
public sealed record TourenListenfilter(DateOnly? Von, DateOnly? Bis, long? AktivitaetId, long? KategorieId);
