using System.Text.Json.Serialization;
using AndyTours.Contracts.V1;
using AndyTours.Application.Touren;
using AndyTours.Core.Modell;

namespace AndyTours.Application.Auswertung;

/// <summary>Serverinterne Referenzmetadaten; Schlüssel und Garmin-Core-Daten bleiben vertraulich.</summary>
public sealed record PulsReferenzServerbestand(
    string Version,
    GarminPulszonenStand? Stand,
    [property: JsonIgnore] string? Schluessel)
{
    public override string ToString() => "Server-Pulsreferenz (vertraulich)";
}

/// <summary>Serverinterner Snapshot; Core-Touren und Referenz sind nie HTTP-Verträge.</summary>
public sealed record PulsQuellbestand(
    IReadOnlyList<Trainingseinheit> Touren,
    string ReferenzVersion,
    GarminPulszonenStand? Referenz);

/// <summary>
/// Serverseitige Quelle für gefilterte Touren und aktuelle Garmin-Referenz.
/// Speichern verwendet CAS; Netzabrufe erfolgen außerhalb einer Datenbanktransaktion.
/// </summary>
public interface IPulsQuelldaten
{
    Task<PulsQuellbestand> Laden(PulsAuswertungsAnfrage anfrage, CancellationToken cancellationToken);

    Task<PulsReferenzServerbestand> ReferenzLaden(CancellationToken cancellationToken);

    Task<PulsReferenzServerbestand> ReferenzSpeichern(
        string erwarteteVersion,
        GarminPulszonenStand stand,
        CancellationToken cancellationToken);
}
