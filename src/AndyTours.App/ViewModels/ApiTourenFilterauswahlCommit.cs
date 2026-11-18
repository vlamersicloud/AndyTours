using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>Prüft ausschließlich die Identität einer Filterantwort und ersetzt einen Tourensnapshot atomar.</summary>
internal static class ApiTourenFilterauswahlCommit
{
    internal static bool TryCommit(
        ApiTourenauswahl selection,
        TourenFilterauswahlAntwort? response,
        TourenFilterauswahlAnfrage expectedFilter,
        string? expectedQueryKey,
        string? currentQueryKey,
        long expectedGeneration,
        long currentGeneration,
        bool closed)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(expectedFilter);
        if (closed || expectedQueryKey is null || expectedQueryKey != currentQueryKey ||
            expectedGeneration != currentGeneration || response is null ||
            response.Filter is null || response.Filter != expectedFilter || response.Touren is null)
            return false;

        try
        {
            selection.SetzeAlle(response.Touren);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
