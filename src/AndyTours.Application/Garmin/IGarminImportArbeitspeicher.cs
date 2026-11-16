using AndyTours.Contracts.V1;

namespace AndyTours.Application.Garmin;

/// <summary>Internal server-side persistence/worker coordination port; never exposed over HTTP.</summary>
internal interface IGarminImportArbeitspeicher
{
    Task<GarminImportLease?> Beanspruchen(Guid besitzer, TimeSpan dauer, CancellationToken ct = default);

    Task<GarminImportLease?> Verlaengern(GarminImportLease lease, TimeSpan dauer, CancellationToken ct = default);

    Task<IReadOnlyList<GarminImportKandidatAntwort>> BeobachteteKandidaten(GarminImportLease lease, CancellationToken ct = default);

    Task<GarminImportKandidatAntwort> DownloadKandidatSpeichern(GarminImportLease lease, GarminImportKandidatAntwort kandidat, CancellationToken ct = default);

    Task<GarminImportJobAntwort> PaketAbschliessen(
        GarminImportLease lease,
        bool unterbrochen,
        bool weitereMoeglich,
        IReadOnlyList<string> warncodes,
        CancellationToken ct = default);
}

/// <summary>A fencing token for one time-bounded worker claim.</summary>
internal sealed record GarminImportLease(
    Guid JobId,
    Guid Besitzer,
    long Generation,
    DateTimeOffset GueltigBisUtc,
    GarminImportJobAntwort Auftrag);

/// <summary>Finite, public-safe warning identifiers. Never include exception text.</summary>
internal static class GarminImportWarncodes
{
    public const string Netzwerk = "netzwerk";
    public const string Zeitlimit = "zeitlimit";
    public const string Abgebrochen = "abgebrochen";
    public const string FitUngueltig = "fit_ungueltig";
    public const string ArchivUngueltig = "archiv_ungueltig";
    public const string DateiFehler = "datei_fehler";
    public const string SchluesselFehlt = "schluessel_fehlt";
    public const string RateLimit = "rate_limit";
}
