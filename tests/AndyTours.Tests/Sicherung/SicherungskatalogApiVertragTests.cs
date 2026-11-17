namespace AndyTours.Tests.Sicherung;

using System.Text;
using System.Text.Json;
using AndyTours.Api;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class SicherungskatalogApiVertragTests
{
    private static readonly string ProgramSource = ReadResource("AndyTours.Sicherung.Erstellung.Program.cs");

    [Fact]
    public async Task CatalogUnavailableMapsToFixed503WithoutExceptionDetails()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = "catalog-test-trace"
        };
        context.Response.Body = body;

        var handled = await new ProblemExceptionHandler().TryHandleAsync(
            context, new SicherungskatalogNichtVerfuegbarException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        var root = json.RootElement;
        Assert.Equal("Vollarchiv-Katalog ist derzeit nicht verfügbar.", root.GetProperty("title").GetString());
        Assert.Equal("sicherungskatalog.nicht_verfuegbar", root.GetProperty("code").GetString());
        Assert.Equal("catalog-test-trace", root.GetProperty("traceId").GetString());
        Assert.True(!root.TryGetProperty("detail", out var detail) || detail.ValueKind == JsonValueKind.Null);
        Assert.DoesNotContain("private/path", Encoding.UTF8.GetString(body.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingStammdatenConflictMappingRemains409()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;

        var handled = await new ProblemExceptionHandler().TryHandleAsync(
            context,
            new StammdatenException(StammdatenFehlerart.Konflikt, "conflict detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal("Stammdatenkonflikt.", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("conflict detail", json.RootElement.GetProperty("detail").GetString());
        Assert.Equal("stammdaten.konflikt", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void CatalogRegistrationIsLazyAndUsesReadonlyUnpooledDatabaseAccess()
    {
        var registration = Slice(
            ProgramSource,
            "builder.Services.AddSingleton<ISicherungskatalog>(_ =>",
            "builder.Services.AddSingleton<IImportUploadSpeicher>");

        Assert.Contains("builder.Services.AddSingleton<ISicherungskatalog>(_ =>", ProgramSource, StringComparison.Ordinal);
        Assert.Contains("SqliteOpenMode.ReadOnly", registration, StringComparison.Ordinal);
        Assert.Contains("Sicherungsdatei.Verbindungstext", registration, StringComparison.Ordinal);
        Assert.Contains("new SqliteConnection(connectionString)", registration, StringComparison.Ordinal);
        Assert.Contains("db.Open();", registration, StringComparison.Ordinal);
        Assert.Contains("db.BeginTransaction(deferred: true)", registration, StringComparison.Ordinal);
        Assert.Contains("SicherungEinstellung.Laden(db)", registration, StringComparison.Ordinal);
        Assert.Contains("ErmittleSicherungsordner.Ausfuehren(dataDirectory, settings)", registration, StringComparison.Ordinal);
        Assert.Contains("VollarchivKatalogKonfiguration.TryLesen(builder.Configuration, out var fullLimits)", registration, StringComparison.Ordinal);
        Assert.Contains("new DateisystemSicherungskatalog(folder, fullLimits)", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("OeffneDatenbank", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.CreateDirectory", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("SicherungEinstellung.Speichern", registration, StringComparison.Ordinal);
    }

    private static string Slice(string source, string start, string end)
    {
        var first = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(first >= 0, $"Missing source marker: {start}");
        var last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
        Assert.True(last > first, $"Missing source marker: {end}");
        return source[first..last];
    }

    private static string ReadResource(string logicalName)
    {
        using var stream = typeof(SicherungskatalogApiVertragTests).Assembly.GetManifestResourceStream(logicalName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
