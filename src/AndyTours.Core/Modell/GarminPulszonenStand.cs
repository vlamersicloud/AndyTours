namespace AndyTours.Core.Modell;

/// <summary>Aktuelle Garmin-Referenzprofile mit tatsächlichem Abrufdatum, kein historischer Zonenbeleg.</summary>
public sealed record GarminPulszonenStand(DateTimeOffset Abgerufen, IReadOnlyList<GarminPulsprofil> Profile);
