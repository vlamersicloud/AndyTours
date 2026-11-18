using System.Reflection;
using System.Text;

namespace AndyTours.Tests.Sicherung;

public sealed class ApiSicherungsdownloadStrukturTests
{
    private static string Source(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void EndpointUsesOpaqueCatalogIdentityRequiredCapAndVerifiedSourceBeforeStreaming()
    {
        var endpoint = Source("AndyTours.SicherungsdownloadEndpunkt.cs");
        var catalog = Source("AndyTours.DateisystemSicherungskatalog.Pruefquelle.cs");
        var catalogType = Source("AndyTours.DateisystemSicherungskatalog.cs");
        var contract = Source("AndyTours.SicherungsdownloadVertrag.cs");
        var limits = Source("AndyTours.SicherungsdownloadGrenzen.cs");
        var program = Source("AndyTours.Sicherung.Erstellung.Program.cs");
        Assert.Equal(1, program.Split("app.MapSicherungsdownload();", StringSplitOptions.None).Length - 1);
        Assert.Contains("Backend:Sicherung:MaxDownloadBytes", endpoint, StringComparison.Ordinal);
        Assert.Contains("SicherungsdownloadGrenzen.TryParse", endpoint, StringComparison.Ordinal);
        Assert.Contains("long.TryParse", limits, StringComparison.Ordinal);
        Assert.Contains("Status413PayloadTooLarge", endpoint, StringComparison.Ordinal);
        Assert.Contains("Status503ServiceUnavailable", endpoint, StringComparison.Ordinal);
        Assert.Contains("Status400BadRequest", endpoint, StringComparison.Ordinal);
        Assert.Contains("ProducesProblem(404)", endpoint, StringComparison.Ordinal);
        Assert.Contains("ProducesProblem(409)", endpoint, StringComparison.Ordinal);
        Assert.Contains("catalog.Liste(ct)", endpoint, StringComparison.Ordinal);
        Assert.Contains("download.Oeffnen(checkedRequest, maximum, ct)", endpoint, StringComparison.Ordinal);
        Assert.True(endpoint.IndexOf("download.Oeffnen(checkedRequest, maximum, ct)", StringComparison.Ordinal) <
            endpoint.IndexOf("httpContext.Response.ContentLength", StringComparison.Ordinal));
        Assert.Contains("ISicherungsdownload, IDisposable", catalogType, StringComparison.Ordinal);
        Assert.Contains("CopyExact(source.Inhalt, httpContext.Response.Body", endpoint, StringComparison.Ordinal);
        Assert.Contains("class OwnedSicherungsdownloadResult", endpoint, StringComparison.Ordinal);
        Assert.Contains("finally", endpoint, StringComparison.Ordinal);
        Assert.Contains("await source.DisposeAsync()", endpoint, StringComparison.Ordinal);
        Assert.Contains("httpContext.RequestAborted", endpoint, StringComparison.Ordinal);
        Assert.Contains("string.Concat", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("Results.Stream(", endpoint, StringComparison.Ordinal);
        Assert.Contains("IncrementalHash.CreateHash(HashAlgorithmName.SHA256)", catalog, StringComparison.Ordinal);
        Assert.Contains("Mode = FileMode.Open", catalog, StringComparison.Ordinal);
        Assert.Contains("Access = FileAccess.Read", catalog, StringComparison.Ordinal);
        Assert.Contains("ownedStream.Position = 0", catalog, StringComparison.Ordinal);
        Assert.Contains("Guid SicherungsId, string ErwarteterInhaltHash", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("string Pfad", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientAndUiVerifyBeforeAtomicNoOverwriteAndInvalidateLateSelection()
    {
        var client = Source("AndyTours.SicherungsdownloadHttpClient.cs");
        var viewModel = Source("AndyTours.ApiWiederherstellungsvorschauViewModel.Download.cs");
        var fileTransfer = Source("AndyTours.SicherungsdownloadDatei.cs");
        var window = Source("AndyTours.ApiWiederherstellungsvorschauFenster.xaml.cs");
        var markup = Source("AndyTours.ApiWiederherstellungsvorschauFenster.xaml");
        var session = Source("AndyTours.BackendClientSitzung.cs");
        Assert.Contains("ResponseHeadersRead", client, StringComparison.Ordinal);
        Assert.Contains("ContentLength != selected.Byteanzahl", client, StringComparison.Ordinal);
        Assert.Contains("FixedTimeEquals", client, StringComparison.Ordinal);
        Assert.Contains("CreateNew", fileTransfer, StringComparison.Ordinal);
        Assert.Contains("File.Move(temporary, target, overwrite: false)", fileTransfer, StringComparison.Ordinal);
        Assert.Contains("UnixCreateMode", fileTransfer, StringComparison.Ordinal);
        Assert.Contains("File.Delete(temporary)", fileTransfer, StringComparison.Ordinal);
        Assert.Contains("stillCurrent()", fileTransfer, StringComparison.Ordinal);
        Assert.Contains("generation == _downloadGeneration", viewModel, StringComparison.Ordinal);
        Assert.Contains("_activeDownload?.Cancel()", viewModel, StringComparison.Ordinal);
        Assert.Contains("SaveFileDialog", window, StringComparison.Ordinal);
        Assert.Contains("current.Id != selectedId", window, StringComparison.Ordinal);
        Assert.Contains("IsCurrentDownload(selected, selectedHash, generation)", viewModel, StringComparison.Ordinal);
        Assert.Contains("Sicherung herunterladen", markup, StringComparison.Ordinal);
        Assert.Contains("SicherungsdownloadHttpClient(_importHttpClient)", session, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient.Dispose()", client, StringComparison.Ordinal);
    }
}
