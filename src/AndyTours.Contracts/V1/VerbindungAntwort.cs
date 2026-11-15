namespace AndyTours.Contracts.V1;

/// <summary>Versionsgebundene Antwort zur Erreichbarkeit des AndyTours-Backends.</summary>
public sealed record VerbindungAntwort(string Status, string Version);
