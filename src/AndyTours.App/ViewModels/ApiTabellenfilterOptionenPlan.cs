using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>Eine reine Textauswahl; null ist ausschließlich die explizite Alle-Auswahl.</summary>
public sealed record ApiTabellenfilterOption(string? Wert, string Name);

/// <summary>Unveränderlicher UI-Plan für Equipment- und Trainingsbereichsfilter.</summary>
public sealed class ApiTabellenfilterOptionenPlan
{
    private ApiTabellenfilterOptionenPlan(
        IReadOnlyList<ApiTabellenfilterOption> equipments,
        IReadOnlyList<ApiTabellenfilterOption> trainingsbereiche,
        ApiTabellenfilterOption ausgewaehltesEquipment,
        ApiTabellenfilterOption ausgewaehlterBereich)
    {
        Equipments = equipments;
        Trainingsbereiche = trainingsbereiche;
        AusgewaehltesEquipment = ausgewaehltesEquipment;
        AusgewaehlterBereich = ausgewaehlterBereich;
    }

    public IReadOnlyList<ApiTabellenfilterOption> Equipments { get; }
    public IReadOnlyList<ApiTabellenfilterOption> Trainingsbereiche { get; }
    public ApiTabellenfilterOption AusgewaehltesEquipment { get; }
    public ApiTabellenfilterOption AusgewaehlterBereich { get; }

    public static ApiTabellenfilterOptionenPlan Erstellen(
        TourenTabellenfilterOptionenAntwort? raw, string? equipment, string? bereich)
    {
        if (raw is null || !IstGueltig(raw.Equipments) || !IstGueltig(raw.Trainingsbereiche))
            throw new ApiTourSeitenException();

        var equipmentOptions = Erzeuge(raw.Equipments, "Alle Equipments");
        var bereichOptions = Erzeuge(raw.Trainingsbereiche, "Alle Trainingsbereiche");
        var ausgewaehltesEquipment = Waehlen(equipmentOptions, equipment);
        var ausgewaehlterBereich = Waehlen(bereichOptions, bereich);
        return new ApiTabellenfilterOptionenPlan(
            Array.AsReadOnly(equipmentOptions.ToArray()),
            Array.AsReadOnly(bereichOptions.ToArray()),
            ausgewaehltesEquipment,
            ausgewaehlterBereich);
    }

    private static bool IstGueltig(IReadOnlyList<string>? values) =>
        values is not null && values.All(value => !string.IsNullOrWhiteSpace(value)) &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Count;

    private static List<ApiTabellenfilterOption> Erzeuge(IReadOnlyList<string> values, string alleLabel)
    {
        var options = new List<ApiTabellenfilterOption>(values.Count + 1)
        {
            new ApiTabellenfilterOption(null, alleLabel)
        };
        for (var i = 0; i < values.Count; i++)
            options.Add(new ApiTabellenfilterOption(values[i], values[i]));
        return options;
    }

    private static ApiTabellenfilterOption Waehlen(List<ApiTabellenfilterOption> options, string? value)
    {
        if (value is null) return options[0];
        var selected = options.FirstOrDefault(option => string.Equals(option.Wert, value, StringComparison.Ordinal));
        if (selected is not null) return selected;
        selected = new ApiTabellenfilterOption(value, value);
        options.Add(selected);
        return selected;
    }
}
