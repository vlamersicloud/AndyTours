namespace AndyTours.Tests.Sicherung;

using System.Reflection;
using AndyTours.Contracts.V1;
using Xunit;

/// <summary>Structural guards for the explicit, exclusive, non-applying integration path.</summary>
public sealed class SqliteBasispruefungIntegrationsquellenTests
{
    private static readonly string[] ExpectedRequestProperties = { "ErwarteterInhaltHash", "SicherungsId" };
    [Fact]
    public void RequestContainsOnlyCatalogIdentityAndExpectedHash()
    {
        var properties = typeof(SqliteBasispruefungAnfrage).GetProperties().Select(x => x.Name).Order().ToArray();
        Assert.Equal(ExpectedRequestProperties, properties);
    }
    [Fact]
    public void ResponseDoesNotExposeRestoreAuthorization()
    {
        var names = typeof(SqliteBasispruefungAntwort).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("AnwendungFreigegeben", names); Assert.DoesNotContain("Dateipfad", names);
    }
    [Fact]
    public void RouteSessionAndPreviewXamlExposeOnlyExplicitCheck()
    {
        var endpoint = Resource("AndyTours.SqliteBasispruefung.Endpunkte.cs");
        Assert.Contains("MapPost(\"/api/v1/sicherungen/sqlite-basispruefung\"", endpoint);
        Assert.Contains("new ExklusiverDatenbestand()", endpoint);
        var program = Resource("AndyTours.Sicherung.Erstellung.Program.cs");
        Assert.Contains("AddSingleton<ISqliteBasispruefung>", program); Assert.Contains("MapSqliteBasispruefung()", program);
        var session = Resource("AndyTours.BackendClientSitzung.cs");
        Assert.Contains("SqliteBasispruefung = new SqliteBasispruefungHttpClient", session);
        Assert.Contains("ISqliteBasispruefung SqliteBasispruefung", session);
        var app = Resource("AndyTours.App.Api.cs"); Assert.Contains("uebergebeneSitzung.SqliteBasispruefung", app);
        var xaml = Resource("AndyTours.ApiWiederherstellungsvorschauFenster.xaml");
        Assert.Contains("{Binding SqlitePruefenCommand}", xaml); Assert.Contains("{Binding SqliteStatus, Mode=OneWay}", xaml);
        Assert.Contains("Anwendungsfreigabe", xaml); Assert.Contains("Nein", xaml);
    }
    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Missing test resource: {name}");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
}
