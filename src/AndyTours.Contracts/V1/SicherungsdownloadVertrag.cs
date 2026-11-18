namespace AndyTours.Contracts.V1;

/// <summary>Requests a verified copy of one selected catalog backup; contains no filesystem path.</summary>
public sealed record SicherungsdownloadAnfrage(Guid SicherungsId, string ErwarteterInhaltHash);
