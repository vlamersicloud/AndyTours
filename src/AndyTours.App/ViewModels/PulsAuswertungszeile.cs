namespace AndyTours.App.ViewModels;

/// <summary>Ein Pulsbereich pro Kalenderperiode; Anteil bezieht sich auf die auswertbare Messzeit dieser Periode.</summary>
public sealed record PulsAuswertungszeile(string Zeitraum, string Zone, string Pulsbereich, string Dauer, string Anteil);
