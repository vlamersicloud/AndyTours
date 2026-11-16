using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Touren;
using AndyTours.Application.Verbindungen;

namespace AndyTours.Client;

/// <summary>Besitzt eine authentifizierte HTTP-Sitzung zum AndyTours-Backend.</summary>
public sealed class BackendClientSitzung : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SocketsHttpHandler _handler;

    /// <summary>Erstellt eine Sitzung mit Redirects deaktiviert.</summary>
    public BackendClientSitzung(Uri basisAdresse, string zugriffstoken)
    {
        ArgumentNullException.ThrowIfNull(basisAdresse);
        if (!basisAdresse.IsAbsoluteUri ||
            (!string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !(string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && basisAdresse.IsLoopback)) ||
            !string.IsNullOrEmpty(basisAdresse.UserInfo) ||
            !string.IsNullOrEmpty(basisAdresse.Query) ||
            !string.IsNullOrEmpty(basisAdresse.Fragment))
            throw new ArgumentException("Backendadresse muss HTTPS oder loopback-HTTP ohne Zusatzdaten sein.", nameof(basisAdresse));
        if (string.IsNullOrWhiteSpace(zugriffstoken) || Encoding.UTF8.GetByteCount(zugriffstoken) < 32 ||
            zugriffstoken.Contains('\r') || zugriffstoken.Contains('\n'))
            throw new ArgumentException("Zugriffstoken muss mindestens 32 Bytes umfassen.", nameof(zugriffstoken));

        BasisAdresse = basisAdresse.AbsoluteUri.EndsWith('/')
            ? basisAdresse
            : new Uri(basisAdresse.AbsoluteUri + "/", UriKind.Absolute);
        _handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        _httpClient = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = BasisAdresse,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", zugriffstoken);
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
        Verbindung = new BackendHttpVerbindung(_httpClient);
    }

    /// <summary>Tokenfreie Backend-Basisadresse.</summary>
    public Uri BasisAdresse { get; }
    /// <summary>Tourenressourcen.</summary>
    public ITourenSpeicher Touren { get; }
    /// <summary>Aktivitätsauswertung.</summary>
    public IAktivitaetsauswertung Auswertung { get; }
    /// <summary>Lesezugriff auf die Stammdaten.</summary>
    public IStammdatenLeser Stammdaten { get; }
    /// <summary>Versionierte Verwaltungsoperationen für Aktivitäten.</summary>
    public IAktivitaetenVerwaltung Aktivitaeten { get; }
    /// <summary>Versionierte Verwaltungsoperationen für Kategorien.</summary>
    public IKategorienVerwaltung Kategorien { get; }
    /// <summary>Versionierte Verwaltungsoperationen für Equipment.</summary>
    public IEquipmentsVerwaltung Equipments { get; }
    /// <summary>Versionierte Verwaltung persönlicher Profildaten.</summary>
    public IProfilVerwaltung Profil { get; }
    /// <summary>Versionierte Verwaltung saisonaler Ziele.</summary>
    public ISaisonzieleVerwaltung Saisonziele { get; }
    /// <summary>Versionierte Verwaltung der unterstützten Textlisten.</summary>
    public ITextlistenVerwaltung Textlisten { get; }
    /// <summary>Versionierte Verwaltung von Wartungen und Alarmen.</summary>
    public IWartungsverwaltung Wartung { get; }
    /// <summary>Versionierte Garmin-Einstellungen ohne Schlüssel-Lesezugriff.</summary>
    public IGarminEinstellungen GarminEinstellungen { get; }
    /// <summary>Durable Garmin import jobs.</summary>
    public IGarminImportJobs GarminImportJobs { get; }
    /// <summary>Verbindungsprüfung.</summary>
    public IBackendVerbindung Verbindung { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
