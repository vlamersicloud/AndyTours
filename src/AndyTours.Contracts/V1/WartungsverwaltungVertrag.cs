namespace AndyTours.Contracts.V1;

/// <summary>Wartungseintrag oder Kilometeralarm.</summary>
public enum Wartungsart { Wartung, Alarm }

/// <summary>Expliziter Status eines Kilometeralarms.</summary>
public enum Wartungsalarmzustand { Faellig, BaldFaellig, Normal, Ausgeschaltet }

/// <summary>Einzelner gespeicherter Wartungs- oder Alarmeintrag.</summary>
public sealed record WartungsdatensatzAntwort(long Id, DateOnly Datum, long? EquipmentId, string Taetigkeit,
    string Kommentar, Wartungsart Art, double AlarmKm, bool Aktiv);

/// <summary>Ungefilterter versionierter Bestand.</summary>
public sealed record WartungsbestandAntwort(string Version, IReadOnlyList<WartungsdatensatzAntwort> Eintraege);

/// <summary>Optionale Filter für die Übersicht; null bedeutet alle.</summary>
public sealed record Wartungsfilter(long? AktivitaetId, long? EquipmentId);

/// <summary>Aufbereitete Zeile einer durchgeführten Wartung.</summary>
public sealed record WartungszeileAntwort(long Id, DateOnly Datum, string Rad, double? KmStand,
    double? KmSeitLetzter, int? TageSeitLetzter, string Taetigkeit, string Kommentar);

/// <summary>Aufbereitete Zeile eines Kilometeralarms.</summary>
public sealed record AlarmzeileAntwort(long Id, string Rad, string Taetigkeit, double IntervallKm,
    double GefahrenKm, double VerbleibendKm, Wartungsalarmzustand Zustand);

/// <summary>Bestand und nach den aktuellen Filtern berechnete Übersicht.</summary>
public sealed record WartungsuebersichtAntwort(WartungsbestandAntwort Bestand,
    IReadOnlyList<WartungszeileAntwort> Wartungszeilen, IReadOnlyList<AlarmzeileAntwort> Alarmzeilen,
    IReadOnlyList<string> Taetigkeiten);

/// <summary>Anfrage zum Anlegen oder Ändern einer Wartung.</summary>
public sealed record WartungSpeichernAnfrage(string Version, DateOnly Datum, long? EquipmentId,
    string Taetigkeit, string Kommentar);

/// <summary>Anfrage zum Anlegen oder Ändern eines Kilometeralarms.</summary>
public sealed record AlarmSpeichernAnfrage(string Version, long EquipmentId, string Taetigkeit,
    int IntervallKm, bool Eingeschaltet);

/// <summary>Atomare Auswahl für Lösch- oder Umschaltaktionen.</summary>
public sealed record WartungenAuswahlAnfrage(string Version, IReadOnlyList<long> Ids);
