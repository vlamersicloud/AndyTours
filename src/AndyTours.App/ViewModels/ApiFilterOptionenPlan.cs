using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>Unveränderlicher, noch nicht auf die Oberfläche angewandter Plan für Filter-Stammdaten.</summary>
public sealed class ApiFilterOptionenPlan
{
    private ApiFilterOptionenPlan(IReadOnlyList<ApiFilterOption> aktivitaeten,
        IReadOnlyList<ApiFilterOption> kategorien, ApiFilterOption aktivitaet,
        ApiFilterOption kategorie, string? hinweis, bool auswahlGeaendert)
    {
        Aktivitaeten = aktivitaeten;
        Kategorien = kategorien;
        Aktivitaet = aktivitaet;
        Kategorie = kategorie;
        Hinweis = hinweis;
        AuswahlGeaendert = auswahlGeaendert;
    }

    public IReadOnlyList<ApiFilterOption> Aktivitaeten { get; }
    public IReadOnlyList<ApiFilterOption> Kategorien { get; }
    public ApiFilterOption Aktivitaet { get; }
    public ApiFilterOption Kategorie { get; }
    public string? Hinweis { get; }
    public bool AuswahlGeaendert { get; }

    public static ApiFilterOptionenPlan Erstellen(StammdatenAntwort raw, long? priorActivity,
        long? priorCategory, bool initial)
    {
        if (raw is null || raw.Aktivitaeten is null || raw.Kategorien is null ||
            raw.Aktivitaeten.Any(x => x is null || x.Id <= 0 || x.Name is null) ||
            raw.Kategorien.Any(x => x is null || x.Id <= 0 || x.Name is null) ||
            raw.Aktivitaeten.Select(x => x.Id).Distinct().Count() != raw.Aktivitaeten.Count ||
            raw.Kategorien.Select(x => x.Id).Distinct().Count() != raw.Kategorien.Count)
            throw new InvalidOperationException("Filterstammdaten sind ungültig.");

        var activityOptions = new List<ApiFilterOption> { new(null, "Alle Aktivitäten") };
        activityOptions.AddRange(raw.Aktivitaeten.Select(x => new ApiFilterOption(x.Id, x.Name)));
        var categoryOptions = new List<ApiFilterOption> { new(null, "Alle Kategorien") };
        categoryOptions.AddRange(raw.Kategorien
            .OrderBy(x => x.Name, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: false))
            .Select(x => new ApiFilterOption(x.Id, x.Name)));

        var activity = priorActivity is { } aid
            ? activityOptions.FirstOrDefault(x => x.Id == aid)
            : initial ? activityOptions.Skip(1).FirstOrDefault(x =>
                raw.Aktivitaeten.First(source => source.Id == x.Id).IstHaupt) : null;
        var category = priorCategory is { } cid ? categoryOptions.FirstOrDefault(x => x.Id == cid) : null;

        string? hint = null;
        if (priorActivity.HasValue && activity is null)
            hint = "Die zuvor gewählte Aktivität ist nicht mehr vorhanden; es wird jetzt nach allen Aktivitäten gefiltert.";
        else if (priorCategory.HasValue && category is null)
            hint = "Die zuvor gewählte Kategorie ist nicht mehr vorhanden; es wird jetzt nach allen Kategorien gefiltert.";
        else if (initial && !priorActivity.HasValue && raw.Aktivitaeten.Count > 0 && activity is null)
            hint = "Keine Hauptaktivität vorhanden; es wird nach allen Aktivitäten gefiltert.";

        activity ??= activityOptions[0];
        category ??= categoryOptions[0];
        bool changed = priorActivity != activity.Id || priorCategory != category.Id;
        return new ApiFilterOptionenPlan(
            Array.AsReadOnly(activityOptions.ToArray()),
            Array.AsReadOnly(categoryOptions.ToArray()),
            activity, category, hint, changed);
    }
}
/// <summary>UI-Auswahlwert mit optionaler Ressourcen-ID für „alle“.</summary>
public sealed record ApiFilterOption(long? Id, string Name);
