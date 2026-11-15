using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AndyTours.Application.Touren;
using AndyTours.Application.Verbindungen;

namespace AndyTours.Client;

/// <summary>Besitzt eine authentifizierte HTTP-Sitzung zu einem lokalen oder zentralen AndyTours-Backend.</summary>
public sealed class BackendClientSitzung : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SocketsHttpHandler _handler;

    /// <summary>Erstellt eine eigene HTTP-Verbindung mit sicher begrenztem Redirect-Verhalten.</summary>
    /// <param name="basisAdresse">Absolute Backendadresse ohne Zugangsdaten, Query oder Fragment.</param>
    /// <param name="zugriffstoken">Bearertoken für den Backend-Datenbestand.</param>
    public BackendClientSitzung(Uri basisAdresse, string zugriffstoken)
    {
        ArgumentNullException.ThrowIfNull(basisAdresse);
        if (!basisAdresse.IsAbsoluteUri ||
            (!string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !(string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
               basisAdresse.IsLoopback)) ||
            !string.IsNullOrEmpty(basisAdresse.UserInfo) ||
            !string.IsNullOrEmpty(basisAdresse.Query) ||
            !string.IsNullOrEmpty(basisAdresse.Fragment))
            throw new ArgumentException("Backendadresse muss HTTPS oder eine loopback-HTTP-Adresse ohne Zusatzdaten sein.", nameof(basisAdresse));
        if (string.IsNullOrWhiteSpace(zugriffstoken) ||
            Encoding.UTF8.GetByteCount(zugriffstoken) < 32 ||
            zugriffstoken.Contains('\r') || zugriffstoken.Contains('\n'))
            throw new ArgumentException("Zugriffstoken muss mindestens 32 Bytes umfassen und darf keine Zeilenumbrüche enthalten.", nameof(zugriffstoken));

        var adresse = basisAdresse.AbsoluteUri.EndsWith('/')
            ? basisAdresse
            : new Uri(basisAdresse.AbsoluteUri + "/", UriKind.Absolute);
        BasisAdresse = adresse;

        _handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        _httpClient = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = adresse,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", zugriffstoken);
        Touren = new TourenHttpClient(_httpClient);
        Verbindung = new BackendHttpVerbindung(_httpClient);
    }

    /// <summary>Die tokenfreie Basisadresse zum Anzeigen in der Oberfläche.</summary>
    public Uri BasisAdresse { get; }

    /// <summary>Typisierter Zugriff auf Tourenressourcen.</summary>
    public ITourenSpeicher Touren { get; }

    /// <summary>Erreichbarkeitsprüfung des Backends.</summary>
    public IBackendVerbindung Verbindung { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
