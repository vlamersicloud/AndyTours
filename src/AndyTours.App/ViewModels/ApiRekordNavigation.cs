using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public static class ApiRekordNavigation
{
    public static long? TourId(
        BestleistungszeileAntwort? row,
        IEnumerable<BestleistungszeileAntwort> jahres,
        IEnumerable<BestleistungszeileAntwort> einzel)
    {
        if (row?.TourId is not { } id || id <= 0) return null;
        return jahres.Any(current => ReferenceEquals(current, row)) ||
               einzel.Any(current => ReferenceEquals(current, row))
            ? id
            : null;
    }
}
