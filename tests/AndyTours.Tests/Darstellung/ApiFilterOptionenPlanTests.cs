using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiFilterOptionenPlanTests
{
    [Fact]
    public void Selects_initial_primary_activity_and_first_all_categories()
    {
        var plan = ApiFilterOptionenPlan.Erstellen(Data(), null, null, true);
        Assert.Equal(2, plan.Aktivitaet.Id);
        Assert.Equal("Alle Kategorien", plan.Kategorie.Name);
        Assert.Contains(plan.Aktivitaet, plan.Aktivitaeten);
    }

    [Fact]
    public void Initial_without_primary_keeps_all_and_reports_fixed_hint()
    {
        var plan = ApiFilterOptionenPlan.Erstellen(Data(false), null, null, true);
        Assert.Null(plan.Aktivitaet.Id);
        Assert.Equal("Keine Hauptaktivität vorhanden; es wird nach allen Aktivitäten gefiltert.", plan.Hinweis);
    }

    [Fact]
    public void Later_null_selection_stays_all_and_missing_ids_fall_back_with_hint()
    {
        var later = ApiFilterOptionenPlan.Erstellen(Data(), null, null, false);
        Assert.Null(later.Aktivitaet.Id);
        Assert.Null(later.Hinweis);
        var missing = ApiFilterOptionenPlan.Erstellen(Data(), 99, 98, false);
        Assert.Null(missing.Aktivitaet.Id);
        Assert.Equal("Die zuvor gewählte Aktivität ist nicht mehr vorhanden; es wird jetzt nach allen Aktivitäten gefiltert.", missing.Hinweis);
    }

    [Fact]
    public void Preserves_selection_by_id_and_uses_updated_names_in_copied_arrays()
    {
        var plan = ApiFilterOptionenPlan.Erstellen(Data(), 2, 8, false);
        Assert.Equal("Updated", plan.Aktivitaet.Name);
        Assert.Equal("Selected", plan.Kategorie.Name);
        Assert.Contains(plan.Aktivitaet, plan.Aktivitaeten);
        Assert.Contains(plan.Kategorie, plan.Kategorien);
        Assert.Equal(["Alle Kategorien", "Selected"], plan.Kategorien.Select(x => x.Name));
    }

    [Fact]
    public void Plan_owns_read_only_copies_of_mutable_source_lists()
    {
        var activities = new List<AktivitaetAntwort>
        {
            new(1, "Second", false, "km/h", 0, 0, 0),
            new(2, "Updated", true, "km/h", 0, 0, 0)
        };
        var categories = new List<KategorieAntwort> { new(8, "Selected", false) };
        var data = new StammdatenAntwort([], activities, categories, [], []);
        var plan = ApiFilterOptionenPlan.Erstellen(data, 2, 8, false);

        activities.Clear();
        categories.Clear();

        Assert.Equal(3, plan.Aktivitaeten.Count);
        Assert.Equal(2, plan.Kategorien.Count);
        Assert.Contains(plan.Aktivitaet, plan.Aktivitaeten);
        Assert.Contains(plan.Kategorie, plan.Kategorien);
        Assert.Same(plan.Aktivitaet, plan.Aktivitaeten.Single(x => x.Id == 2));
        Assert.Same(plan.Kategorie, plan.Kategorien.Single(x => x.Id == 8));
        Assert.Throws<NotSupportedException>(() => ((IList<ApiFilterOption>)plan.Aktivitaeten).Clear());
    }
    private static StammdatenAntwort Data(bool primary = true) => new([], [
        new AktivitaetAntwort(1, "Second", false, "km/h", 0, 0, 0),
        new AktivitaetAntwort(2, primary ? "Updated" : "Other", primary, "km/h", 0, 0, 0)
    ], [new KategorieAntwort(8, "Selected", false)], [], []);
}
