using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiRekordNavigationTests
{
    [Fact]
    public void Only_current_reference_with_positive_id_is_navigable()
    {
        var row = Make(42);
        Assert.Equal(42, ApiRekordNavigation.TourId(row, [row], []));
        Assert.Equal(42, ApiRekordNavigation.TourId(row, [], [row]));
        Assert.Null(ApiRekordNavigation.TourId(null, [row], []));
        var noId = Make(null);
        var zeroId = Make(0);
        var negativeId = Make(-1);
        Assert.Null(ApiRekordNavigation.TourId(noId, [noId], []));
        Assert.Null(ApiRekordNavigation.TourId(zeroId, [], [zeroId]));
        Assert.Null(ApiRekordNavigation.TourId(negativeId, [negativeId], []));
        Assert.Null(ApiRekordNavigation.TourId(Make(42), [Make(42)], []));
        var current = new List<BestleistungszeileAntwort> { row };
        current.Clear();
        Assert.Null(ApiRekordNavigation.TourId(row, current, []));
    }

    [Fact]
    public void Clicked_record_identity_is_independent_of_duplicate_display_text()
    {
        var first = Make(11);
        var second = Make(22);
        Assert.Equal(22, ApiRekordNavigation.TourId(second, [first, second], []));
    }

    private static BestleistungszeileAntwort Make(long? id) =>
        new("same label", "same value", "same origin", false, id);
}
