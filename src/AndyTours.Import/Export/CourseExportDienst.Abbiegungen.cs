using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Import.Routing;
namespace AndyTours.Import.Export;

public sealed partial class CourseExportDienst
{
    private const string RoutingNichtKonfiguriert = "routing_nicht_konfiguriert";
    private const string RoutingNichtErreichbar = "routing_nicht_erreichbar";

    private async Task<(IReadOnlyList<Courseabbiegung> Abbiegungen, string? Warncode)> LadeAbbiegungen(
        IReadOnlyList<Geoposition> spur, Routingeinstellungen routing, Fahrprofil profil, CancellationToken cancellationToken)
    {
        List<Teilstueck> teilstuecke = [];
        HttpClient? http = null;
        try
        {
            foreach (var paket in WaehleStuetzpunkte.Ausfuehren(spur))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = BildeRoutinganfrage.Ausfuehren(routing, profil, paket);
                if (request is null) return ([], RoutingNichtKonfiguriert);
                http ??= _httpFactory();
                var part = await RoutingTransport.Ermittle(http, request, cancellationToken).ConfigureAwait(false);
                if (part is null) return ([], RoutingNichtErreichbar);
                teilstuecke.Add(part);
            }
            return (UebernimmAbbiegungen.Ausfuehren(spur, teilstuecke), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or ArgumentException or InvalidOperationException) { return ([], RoutingNichtErreichbar); }
        finally { http?.Dispose(); }
    }
}
