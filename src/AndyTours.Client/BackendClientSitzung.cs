using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Export;
using AndyTours.Application.Garmin;
using AndyTours.Application.Import;
using AndyTours.Application.Overlays;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Touren;
using AndyTours.Application.Verbindungen;

namespace AndyTours.Client;

/// <summary>Besitzt eine authentifizierte HTTP-Sitzung zum AndyTours-Backend.</summary>
public sealed class BackendClientSitzung : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _importHttpClient;
    private readonly SocketsHttpHandler _handler;

    public BackendClientSitzung(Uri basisAdresse, string zugriffstoken)
    {
        ArgumentNullException.ThrowIfNull(basisAdresse);
        if (!basisAdresse.IsAbsoluteUri ||
            (!string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !(string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && basisAdresse.IsLoopback)) ||
            !string.IsNullOrEmpty(basisAdresse.UserInfo) || !string.IsNullOrEmpty(basisAdresse.Query) || !string.IsNullOrEmpty(basisAdresse.Fragment))
            throw new ArgumentException("Backendadresse muss HTTPS oder loopback-HTTP ohne Zusatzdaten sein.", nameof(basisAdresse));
        if (string.IsNullOrWhiteSpace(zugriffstoken) || Encoding.UTF8.GetByteCount(zugriffstoken) < 32 || zugriffstoken.Contains('\r') || zugriffstoken.Contains('\n'))
            throw new ArgumentException("Zugriffstoken muss mindestens 32 Bytes umfassen.", nameof(zugriffstoken));

        BasisAdresse = basisAdresse.AbsoluteUri.EndsWith('/') ? basisAdresse : new Uri(basisAdresse.AbsoluteUri + "/", UriKind.Absolute);
        _handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        _httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = BasisAdresse, Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", zugriffstoken);
        _importHttpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = BasisAdresse, Timeout = TimeSpan.FromMinutes(10) };
        _importHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", zugriffstoken);
        Touren = new TourenHttpClient(_httpClient);
        Auswertung = new AktivitaetsauswertungHttpClient(_httpClient);
        Stammdaten = new StammdatenHttpClient(_httpClient);
        Aktivitaeten = new AktivitaetenHttpVerwaltung(_httpClient);
        Kategorien = new KategorienVerwaltungHttpClient(_httpClient);
        Equipments = new EquipmentsVerwaltungHttpClient(_httpClient);
        Profil = new ProfilVerwaltungHttpClient(_httpClient);
        Textlisten = new TextlistenVerwaltungHttpClient(_httpClient);
        Saisonziele = new SaisonzieleVerwaltungHttpClient(_httpClient);
        Wartung = new WartungsverwaltungHttpClient(_httpClient);
        GarminEinstellungen = new GarminEinstellungenHttpClient(_httpClient);
        GarminImportJobs = new GarminImportJobsHttpClient(_httpClient);
        GarminImportPruefung = new GarminImportPruefungHttpClient(_importHttpClient);
        FitUploads = new FitUploadsHttpClient(_importHttpClient);
        Tourdetail = new TourdetailHttpClient(_importHttpClient);
        Puls = new PulsAuswertungHttpClient(_importHttpClient);
        OverlayLeser = new OverlayLeserHttpClient(_importHttpClient);
        OverlayVerwaltung = new OverlayVerwaltungHttpClient(_importHttpClient);
        OverlayGpxImport = new OverlayGpxImportHttpClient(_importHttpClient);
        CourseExport = new CourseExportHttpClient(_importHttpClient);
        PlanungsRouting = new PlanungsRoutingHttpClient(_importHttpClient);
        Verbindung = new BackendHttpVerbindung(_httpClient);
    }

    public Uri BasisAdresse { get; }
    public ITourenSpeicher Touren { get; }
    public IAktivitaetsauswertung Auswertung { get; }
    public IStammdatenLeser Stammdaten { get; }
    public IAktivitaetenVerwaltung Aktivitaeten { get; }
    public IKategorienVerwaltung Kategorien { get; }
    public IEquipmentsVerwaltung Equipments { get; }
    public IProfilVerwaltung Profil { get; }
    public ITextlistenVerwaltung Textlisten { get; }
    public ISaisonzieleVerwaltung Saisonziele { get; }
    public IWartungsverwaltung Wartung { get; }
    public IGarminEinstellungen GarminEinstellungen { get; }
    public IGarminImportJobs GarminImportJobs { get; }
    public IGarminImportPruefung GarminImportPruefung { get; }
    public IFitUploads FitUploads { get; }
    public ITourdetailLeser Tourdetail { get; }
    public IPulsAuswertung Puls { get; }
    public IOverlayLeser OverlayLeser { get; }
    public IOverlayVerwaltung OverlayVerwaltung { get; }
    public IOverlayGpxImport OverlayGpxImport { get; }
    public ICourseExport CourseExport { get; }
    public IPlanungsRouting PlanungsRouting { get; }
    public IBackendVerbindung Verbindung { get; }

    public void Dispose()
    {
        _importHttpClient.Dispose();
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
