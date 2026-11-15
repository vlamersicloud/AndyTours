namespace AndyTours.Core.Modell;

/// <summary>
/// Aufsummierte Kennwerte aller Trainingseinheiten eines Jahres.
/// </summary>
/// <remarks>
/// Ergebnis von <see cref="Berechnungen.BerechneJahressummen"/>; Grundlage der Jahresrekorde
/// (<see cref="Berechnungen.ErmittleBestleistungen"/>).
/// </remarks>
/// <param name="Jahr">Kalenderjahr.</param>
/// <param name="Anzahl">Anzahl der Trainingseinheiten.</param>
/// <param name="DistanzKm">Summe der Distanzen in Kilometern.</param>
/// <param name="Hoehenmeter">Summe der Höhenmeter.</param>
/// <param name="Fahrzeit">Summe der Fahrzeiten.</param>
public record Jahressumme(int Jahr, int Anzahl, double DistanzKm, int Hoehenmeter, TimeSpan Fahrzeit);
