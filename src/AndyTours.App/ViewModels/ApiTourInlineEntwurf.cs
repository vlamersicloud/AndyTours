namespace AndyTours.App.ViewModels;

using System.Globalization;
using AndyTours.Contracts.V1;

/// <summary>Immutable tour edit-start snapshot with a deliberately finite inline column parser.</summary>
public sealed class ApiTourInlineEntwurf
{
    private static readonly string[] Datumsformate = ["d.M.yyyy", "d.M.yy"];
    private static readonly string[] Uhrzeitformate = ["H:mm", "HH:mm"];
    private readonly TourWerte _original;
    private readonly TourWerte _werte;
    public long TourId { get; }
    public string Version { get; }
    public TourWerte Original => Clone(_original);
    public TourWerte Werte => Clone(_werte);
    public bool HatAenderungen => !(_werte with { KategorieIds = _original.KategorieIds }).Equals(_original)
        || !_werte.KategorieIds.SequenceEqual(_original.KategorieIds);

    private static TourWerte Clone(TourWerte value) => value with { KategorieIds = [.. value.KategorieIds] };
    private ApiTourInlineEntwurf(long tourId, string version, TourWerte original, TourWerte werte)
    {
        TourId = tourId;
        Version = version;
        _original = Clone(original);
        _werte = Clone(werte);
    }
    public static ApiTourInlineEntwurf Beginnen(TourAntwort tour)
    {
        ArgumentNullException.ThrowIfNull(tour);
        var snapshot = tour.Werte with { KategorieIds = [.. tour.Werte.KategorieIds] };
        return new(tour.Id, tour.Version, snapshot, snapshot with { KategorieIds = [.. snapshot.KategorieIds] });
    }

    public (bool Valid, ApiTourInlineEntwurf Draft, string Error) Aendere(string column, string? text,
        IReadOnlyList<(long Id, string Name)>? activities = null, IReadOnlyList<(long Id, string Name)>? equipment = null)
    {
        var value = (text ?? string.Empty).Trim(); var culture = CultureInfo.GetCultureInfo("de-DE");
        bool Decimal(out double number) { number = 0; return value.Length == 0 || double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number); }
        bool Integer(out int number) { number = 0; return value.Length == 0 || int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number); }
        TourWerte? next = column switch
        {
            "Name" when value.Length <= 500 => Werte with { Name = value },
            "Datum" when DateOnly.TryParseExact(value, Datumsformate, culture, DateTimeStyles.None, out var date) => Werte with { Datum = date },
            "Startzeit" when value.Length == 0 => Werte with { Startzeit = null },
            "Startzeit" when TimeOnly.TryParseExact(value, Uhrzeitformate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) => Werte with { Startzeit = time },
            "Distanz" when Decimal(out var distance) && distance >= 0 => Werte with { DistanzKm = distance },
            "Hoehenmeter" when Integer(out var meters) && meters >= 0 => Werte with { Hoehenmeter = meters },
            "Fahrzeit" when ParseDuration(value, out var seconds) => Werte with { FahrzeitSekunden = seconds },
            "MaxKmh" when Decimal(out var speed) && speed >= 0 => Werte with { MaxGeschwindigkeit = speed },
            "PulsDurchschnitt" when Integer(out var average) && average >= 0 => Werte with { HfDurchschnitt = average },
            "PulsMax" when Integer(out var maximum) && maximum >= 0 => Werte with { HfMax = maximum },
            "Trittfrequenz" when Integer(out var cadence) && cadence >= 0 => Werte with { Trittfrequenz = cadence },
            "Temperatur" when Integer(out var temperature) => Werte with { Temperatur = temperature },
            "MaxHoehe" when Integer(out var height) && height >= 0 => Werte with { MaxHoehe = height },
            "Trainingsbereich" when value.Length <= 200 => Werte with { Trainingsbereich = value },
            "Aktivitaet" when activities is not null && activities.Count(x => x.Name == text) == 1 && activities.First(x => x.Name == text) is var activity && activity.Id > 0 => Werte with { AktivitaetId = activity.Id },
            "Equipment" when value.Length == 0 || value == "(kein)" => Werte with { EquipmentId = null },
            "Equipment" when equipment is not null && equipment.Count(x => x.Name == text) == 1 && equipment.First(x => x.Name == text) is var item && item.Id > 0 => Werte with { EquipmentId = item.Id },
            _ => null
        };
        if (next is null) return (false, this, "Wert ungültig oder Spalte nicht bearbeitbar.");
        return (true, new ApiTourInlineEntwurf(TourId, Version, _original, next with { KategorieIds = [.. _original.KategorieIds] }), string.Empty);
    }

    private static bool ParseDuration(string value, out long seconds)
    {
        seconds = 0; if (value.Length == 0) return true;
        var match = System.Text.RegularExpressions.Regex.Match(value, @"^(\d{1,6}):([0-5]\d)(?::([0-5]\d))?$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hours)) return false;
        var minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var secs = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        seconds = checked((long)hours * 3600 + minutes * 60 + secs); return true;
    }
}
