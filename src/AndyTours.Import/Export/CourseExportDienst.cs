using System.Net.Http.Headers;
using System.Reflection;
using AndyTours.Application.Export;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
namespace AndyTours.Import.Export;

public sealed partial class CourseExportDienst
{
    private readonly ICourseExportServerdaten _serverdaten;
    private readonly Func<HttpClient> _httpFactory;
    private readonly TimeProvider _zeit;

    public CourseExportDienst(ICourseExportServerdaten serverdaten, Func<HttpClient>? httpFactory = null, TimeProvider? zeit = null)
    {
        _serverdaten = serverdaten ?? throw new ArgumentNullException(nameof(serverdaten));
        _httpFactory = httpFactory ?? ErzeugeHttpClient;
        _zeit = zeit ?? TimeProvider.System;
    }

    public async Task<CourseExportEinstellungenAntwort> LadenEinstellungen(CancellationToken cancellationToken)
    {
        var context = await _serverdaten.Laden(cancellationToken).ConfigureAwait(false);
        return new(ToVertrag(context.Profil));
    }

    private static CourseFahrprofil ToVertrag(Fahrprofil profil) => profil switch
    {
        Fahrprofil.Rennrad => CourseFahrprofil.Rennrad,
        Fahrprofil.Trekking => CourseFahrprofil.Trekking,
        Fahrprofil.Mountainbike => CourseFahrprofil.Mountainbike,
        _ => throw Invalid()
    };

    private static Fahrprofil ToCore(CourseFahrprofil profil) => profil switch
    {
        CourseFahrprofil.Rennrad => Fahrprofil.Rennrad,
        CourseFahrprofil.Trekking => Fahrprofil.Trekking,
        CourseFahrprofil.Mountainbike => Fahrprofil.Mountainbike,
        _ => throw Invalid()
    };

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Course-Export-Anfrage ungültig.");

    private static HttpClient ErzeugeHttpClient()
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(20) };
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AndyTours", version));
        return client;
    }
}
