namespace AndyTours.Application.Sicherung;

/// <summary>Explicit server-supplied archive bounds; never supplied by the client request and has no defaults.</summary>
public sealed record SicherungsarchivGrenzen(
    SicherungsmanifestGrenzen Manifest,
    long MaxArchivBytes,
    long MaxManifestBytes,
    long MaxZentralverzeichnisBytes);
