namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SqliteBasispruefungAdapterTests
{
    [Fact]
    public async Task MissingConfigurationReturnsUnavailableWithoutListingOrStartingChecker()
    {
        var catalog = new FakeCatalog(); var service = new SqliteBasispruefungDienst(new ConfigurationBuilder().Build(), "/data", catalog);
        await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() =>
            service.Pruefen(new SqliteBasispruefungAnfrage(Guid.NewGuid(), new string('a', 64)), CancellationToken.None));
        Assert.Equal(0, catalog.Calls);
    }
    [Fact]
    public async Task PreCancelledRequestHasNoSideEffect()
    {
        var catalog = new FakeCatalog(); var service = new SqliteBasispruefungDienst(new ConfigurationBuilder().Build(), "/data", catalog);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.Pruefen(new SqliteBasispruefungAnfrage(Guid.NewGuid(), new string('a', 64)), cts.Token));
        Assert.Equal(0, catalog.Calls);
    }
    private sealed class FakeCatalog : ISicherungskatalog
    {
        public int Calls;
        public Task<SicherungListeAntwort> Liste(CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new SicherungListeAntwort(Array.Empty<SicherungAntwort>())); }
    }
}
