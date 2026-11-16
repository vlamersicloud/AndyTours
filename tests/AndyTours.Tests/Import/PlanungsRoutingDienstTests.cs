using System.Net;
using System.Text;
using AndyTours.Application.Export;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Routing;

namespace AndyTours.Tests.Import;

public sealed class PlanungsRoutingDienstTests
{
    [Fact]
    public async Task BRouter_response_maps_geometry_turns_and_disposes_owned_client()
    {
        var handler = new RecordingHandler();
        var source = new Serverdaten(new(Fahrprofil.Trekking, Routingeinstellungen.Standard));
        int factories = 0;
        var service = new PlanungsRoutingDienst(source, () => { factories++; return new HttpClient(handler, disposeHandler: true); });
        var answer = await service.Berechnen(Request(CourseFahrprofil.Rennrad), CancellationToken.None);

        Assert.Equal(1, factories);
        Assert.True(handler.Disposed);
        Assert.False(answer.IstLuftlinie);
        Assert.Null(answer.Warncode);
        Assert.Equal(2, answer.Punkte.Count);
        Assert.Equal(48.1, answer.Punkte[0].Position.Breite, 5);
        Assert.Equal(11.5, answer.Punkte[0].Position.Laenge, 5);
        Assert.Equal(520, answer.Punkte[0].Hoehe);
        Assert.Equal(PlanungsAbbiegerichtung.Rechts, Assert.Single(answer.Abbiegepunkte).Richtung);
        Assert.Contains("fastbike", handler.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Missing_OR_service_key_falls_back_without_creating_http_client()
    {
        var source = new Serverdaten(new(Fahrprofil.Trekking, new Routingeinstellungen(Routingdienst.Openrouteservice,
            Routingeinstellungen.StandardBRouterAdresse, null)));
        int factories = 0;
        var service = new PlanungsRoutingDienst(source, () => { factories++; throw new InvalidOperationException(); });
        var answer = await service.Berechnen(Request(CourseFahrprofil.Trekking), CancellationToken.None);

        Assert.Equal(0, factories);
        Assert.True(answer.IstLuftlinie);
        Assert.Equal("routing_einstellungen_unvollstaendig", answer.Warncode);
        Assert.Equal(2, answer.Punkte.Count);
        Assert.Empty(answer.Abbiegepunkte);
    }

    [Fact]
    public async Task Invalid_coordinates_and_profile_are_rejected_before_server_configuration_read()
    {
        var source = new Serverdaten(new(Fahrprofil.Trekking, Routingeinstellungen.Standard));
        var service = new PlanungsRoutingDienst(source, () => new HttpClient(new RecordingHandler()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Berechnen(
            new PlanungsRoutingAnfrage(CourseFahrprofil.Trekking, Point(double.NaN, 0), Point(48, 11)), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Berechnen(
            new PlanungsRoutingAnfrage((CourseFahrprofil)999, Point(48, 11), Point(49, 12)), CancellationToken.None));
        Assert.Equal(0, source.Calls);
    }

    private static PlanungsRoutingAnfrage Request(CourseFahrprofil profile) =>
        new(profile, Point(48.1, 11.5), Point(48.2, 11.6));
    private static PlanungsRoutingpunkt Point(double lat, double lon) => new(new PlanungsPosition(lat, lon), null);

    private sealed class Serverdaten(CourseExportServerkontext context) : ICourseExportServerdaten
    {
        public int Calls { get; private set; }
        public Task<CourseExportServerkontext> Laden(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(context);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            const string json = "{\"features\":[{\"geometry\":{\"coordinates\":[[11.5,48.1,520],[11.6,48.2,530]]},\"properties\":{\"voicehints\":[[1,5,0,0,0]]}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/geo+json")
            });
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
