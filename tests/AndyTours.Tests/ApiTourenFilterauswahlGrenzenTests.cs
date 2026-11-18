using System.Text.Json;
using AndyTours.Api;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests;

public sealed class ApiTourenFilterauswahlGrenzenTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BothLimitsAreRequiredPositiveInvariantInt32AndOverflowFailsClosed()
    {
        Assert.False(TourenFilterauswahlGrenzen.TryParse(null, "100", out _, out _));
        Assert.False(TourenFilterauswahlGrenzen.TryParse("10", null, out _, out _));
        Assert.False(TourenFilterauswahlGrenzen.TryParse("0", "100", out _, out _));
        Assert.False(TourenFilterauswahlGrenzen.TryParse("10", "0", out _, out _));
        Assert.False(TourenFilterauswahlGrenzen.TryParse("-1", "100", out _, out _));
        Assert.False(TourenFilterauswahlGrenzen.TryParse("1,000", "100", out _, out _));
        Assert.False(TourenFilterauswahlGrenzen.TryParse("2147483648", "100", out _, out _));
        Assert.False(TourenFilterauswahlGrenzen.TryParse("10", "2147483648", out _, out _));
        Assert.True(TourenFilterauswahlGrenzen.TryParse("2147483647", "2147483647", out var count, out var bytes));
        Assert.Equal(int.MaxValue, count);
        Assert.Equal(int.MaxValue, bytes);
    }

    [Fact]
    public void PreCancelledSerializationThrowsCancellationInsteadOfReturningBudgetExceeded()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var filter = new TourenFilterauswahlAnfrage(null, null, null, null, null);
        var response = new TourenFilterauswahlAntwort(filter, []);

        Assert.Throws<OperationCanceledException>(() =>
            TourenFilterauswahlAntwortSerialisierer.TrySerialisieren(
                response, Json, 100, out _, cts.Token));
    }

    [Fact]
    public void ResponseSerializationAcceptsExactBudgetAndRejectsOneByteLessWithoutPartialPayload()
    {
        var filter = new TourenFilterauswahlAnfrage(null, null, null, null, null);
        var tour = new TourAntwort(1, new string('A', 64),
            new TourWerte(1, new DateOnly(2026, 1, 1), null, "Tour", 1, 1, 0, 1, 0,
                1, 1, 1, 1, 1, 1, 0, 1, 0, "", "", 1, null, []));
        var response = new TourenFilterauswahlAntwort(filter, [tour]);
        var expected = JsonSerializer.SerializeToUtf8Bytes(response, Json);

        Assert.True(TourenFilterauswahlAntwortSerialisierer.TrySerialisieren(
            response, Json, expected.Length, out var payload));
        Assert.Equal(expected, payload);
        Assert.False(TourenFilterauswahlAntwortSerialisierer.TrySerialisieren(
            response, Json, expected.Length - 1, out var rejected));
        Assert.Empty(rejected);
        Assert.False(TourenFilterauswahlAntwortSerialisierer.TrySerialisieren(
            response, Json, 0, out rejected));
        Assert.Empty(rejected);
        Assert.True(TourenFilterauswahlAntwortSerialisierer.TrySerialisieren(
            response, Json, int.MaxValue, out payload));
    }
}
