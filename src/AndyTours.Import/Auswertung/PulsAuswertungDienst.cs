using System.Net;
using System.Net.Http.Headers;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Dateien;

namespace AndyTours.Import.Auswertung;

/// <summary>Serverseitige Pulsreferenz- und Auswertungsdienste.</summary>
public sealed partial class PulsAuswertungDienst
{
    private readonly IPulsQuelldaten _quelle;
    private readonly Func<HttpClient> _httpFactory;
    private readonly TimeProvider _zeit;
    private readonly FitDateiDienst _fit;

    public PulsAuswertungDienst(IPulsQuelldaten quelle, string datenordner,
        Func<HttpClient>? httpFactory = null, TimeProvider? zeit = null)
    {
        ArgumentNullException.ThrowIfNull(quelle);
        _quelle = quelle;
        _fit = new FitDateiDienst(datenordner, TimeZoneInfo.Utc);
        _zeit = zeit ?? TimeProvider.System;
        _httpFactory = httpFactory ?? CreaHttpClient;
    }

    public async Task<PulsReferenzAntwort> LadenReferenz(CancellationToken cancellationToken)
    {
        var state = await _quelle.ReferenzLaden(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return MappaReferenz(state.Stand, state.Version);
    }

    private static PulsReferenzAntwort MappaReferenz(GarminPulszonenStand? stand, string version) =>
        new(version, stand?.Abgerufen,
            stand?.Profile.Select(profile => new PulsprofilAntwort(profile.Sport, profile.Untergrenzen.ToArray())).ToArray()
            ?? Array.Empty<PulsprofilAntwort>());

    private static HttpClient CreaHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AndyTours", "1.0"));
        return client;
    }

    private static StammdatenException Ungueltig() =>
        new(StammdatenFehlerart.Ungueltig, "Pulsauswertungsanfrage ungültig.");

    private static StammdatenException Konflikt() =>
        new(StammdatenFehlerart.Konflikt, "Pulsreferenz wurde zwischenzeitlich geändert.");

    private static Zeitraster MappaRaster(Auswertungsraster raster) => raster switch
    {
        Auswertungsraster.Jahr => Zeitraster.Jahr,
        Auswertungsraster.Monat => Zeitraster.Monat,
        Auswertungsraster.Woche => Zeitraster.Woche,
        Auswertungsraster.Tag => Zeitraster.Tag,
        _ => throw Ungueltig()
    };
}
