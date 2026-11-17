namespace AndyTours.Tests.Sicherung;

using System.Reflection;
using AndyTours.Api;
using Xunit;

public sealed class SicherungsarchivErstellungApiVertragTests
{
    private const string ProgramResource = "AndyTours.Sicherung.Erstellung.Program.cs";
    private const string EndpointResource = "AndyTours.Sicherung.Erstellung.Endpunkte.cs";

    [Fact]
    public void EndpointUsesExclusiveMetadataWithoutAcquiringItsOwnLease()
    {
        var endpoint = ReadResource(EndpointResource);
        Assert.Contains("group.MapPost(\"/\"", endpoint, StringComparison.Ordinal);
        Assert.Contains(".WithMetadata(new ExklusiverDatenbestand())", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("Betreten(", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("ExklusivBetreten(", endpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointValidatesAndDisablesFeatureBeforeCallingCreatorAndForwardsCancellation()
    {
        var endpoint = ReadResource(EndpointResource);
        var invalid = endpoint.IndexOf("request.VorgangsId == Guid.Empty", StringComparison.Ordinal);
        var resolve = endpoint.IndexOf("bereitstellung.Ermittle(cancellationToken)", StringComparison.Ordinal);
        var unavailable = endpoint.IndexOf("if (creator is null)", StringComparison.Ordinal);
        var invoke = endpoint.IndexOf("creator.Erstellen(request, cancellationToken)", StringComparison.Ordinal);
        Assert.True(invalid >= 0 && invalid < resolve);
        Assert.True(resolve < unavailable && unavailable < invoke);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", endpoint, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status400BadRequest", endpoint, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status409Conflict", endpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramRegistersProviderLazilyAndMapsEndpointAfterAuthenticationMiddleware()
    {
        var program = ReadResource(ProgramResource);
        Assert.Contains("AddSingleton(_ => new SicherungsarchivBereitstellung(builder.Configuration, dataDirectory, TimeProvider.System))", program, StringComparison.Ordinal);
        Assert.DoesNotContain(".Ermittle(", program, StringComparison.Ordinal);
        Assert.Contains("app.MapSicherungsarchivErstellung();", program, StringComparison.Ordinal);
        var auth = program.IndexOf("FixedTimeEquals(expectedTokenHash", StringComparison.Ordinal);
        var leaseMiddleware = program.IndexOf("app.UseMiddleware<DatenbestandsMiddleware>();", StringComparison.Ordinal);
        var mapEndpoint = program.IndexOf("app.MapSicherungsarchivErstellung();", StringComparison.Ordinal);
        Assert.True(auth >= 0 && auth < leaseMiddleware && leaseMiddleware < mapEndpoint);
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(SicherungsarchivErstellungApiVertragTests).Assembly.GetManifestResourceStream(name);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
