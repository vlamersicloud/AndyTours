namespace AndyTours.Contracts.V1;

public enum RoutingAnbieter
{
    BRouter,
    Openrouteservice
}

public sealed record RoutingKonfigurationAntwort(
    string Version,
    RoutingAnbieter Dienst,
    string? BRouterAdresse,
    bool AdresseAusgeblendet,
    bool HatOrsSchluessel);

public sealed record RoutingDienstAnfrage(string Version, RoutingAnbieter Dienst)
{
    public override string ToString() => "RoutingDienstAnfrage { Version = [REDACTED], Dienst = [REDACTED] }";
}

public sealed record RoutingAdresseAnfrage(string Version, string? Adresse)
{
    public override string ToString() => "RoutingAdresseAnfrage { Version = [REDACTED], Adresse = [REDACTED] }";
}

public sealed record RoutingOrsSchluesselAnfrage(string Version, string Schluessel)
{
    public override string ToString() => "RoutingOrsSchluesselAnfrage { Version = [REDACTED], Schluessel = [REDACTED] }";
}

public sealed record RoutingVersionAnfrage(string Version)
{
    public override string ToString() => "RoutingVersionAnfrage { Version = [REDACTED] }";
}
