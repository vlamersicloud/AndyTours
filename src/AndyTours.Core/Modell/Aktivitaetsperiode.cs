namespace AndyTours.Core.Modell;

/// <summary>Summen eines auf den gewählten Zeitraum begrenzten Kalenderabschnitts.</summary>
public sealed record Aktivitaetsperiode(DateOnly Von, DateOnly Bis, string Beschriftung,
    int Anzahl, double DistanzKm, long Hoehenmeter, TimeSpan Fahrzeit);
