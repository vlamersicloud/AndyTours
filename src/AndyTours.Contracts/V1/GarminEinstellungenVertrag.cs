namespace AndyTours.Contracts.V1;
public sealed record GarminEinstellungenAntwort(string Version, bool HatSchluessel, bool AllePaketeAutomatisch);
public sealed record GarminSchluesselAnfrage(string Version, string Schluessel)
{
    public override string ToString() => "GarminSchluesselAnfrage { Schluessel = [REDACTED] }";
}
public sealed record GarminVersionAnfrage(string Version);
public sealed record GarminAutomatikAnfrage(string Version, bool AllePaketeAutomatisch);
