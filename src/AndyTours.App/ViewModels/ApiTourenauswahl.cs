using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>WPF-unabhängiger, expliziter Snapshot manuell ausgewählter Touren.</summary>
public sealed class ApiTourenauswahl
{
    private readonly Dictionary<long, TourAntwort> _items = new();
    public int Count => _items.Count;
    public IReadOnlyList<TourAntwort> Snapshot => Array.AsReadOnly(_items.Values.OrderBy(x => x.Id).Select(Copy).ToArray());

    public bool Setze(TourAntwort tour, bool ausgewaehlt)
    {
        ArgumentNullException.ThrowIfNull(tour);
        Pruefe(tour);
        if (!ausgewaehlt) return _items.Remove(tour.Id);
        if (_items.ContainsKey(tour.Id)) return false;
        _items.Add(tour.Id, Copy(tour));
        return true;
    }

    public void SetzeSichtbare(IEnumerable<TourAntwort> seite, IEnumerable<long> ausgewaehlteIds)
    {
        ArgumentNullException.ThrowIfNull(seite);
        ArgumentNullException.ThrowIfNull(ausgewaehlteIds);
        var rows = seite.ToArray();
        foreach (var row in rows) { ArgumentNullException.ThrowIfNull(row); Pruefe(row); }
        if (rows.Select(row => row.Id).Distinct().Count() != rows.Length)
            throw new ArgumentException("Sichtbare Touren enthalten doppelte IDs.", nameof(seite));
        var requested = ausgewaehlteIds.ToArray();
        if (requested.Distinct().Count() != requested.Length)
            throw new ArgumentException("Auswahl enthält doppelte IDs.", nameof(ausgewaehlteIds));
        var ids = requested.ToHashSet();
        if (ids.Any(id => rows.All(row => row.Id != id))) throw new ArgumentException("Auswahl enthält keine sichtbare Tour.", nameof(ausgewaehlteIds));
        foreach (var row in rows)
        {
            if (ids.Contains(row.Id))
            {
                if (!_items.ContainsKey(row.Id)) _items.Add(row.Id, Copy(row));
            }
            else _items.Remove(row.Id);
        }
    }

    public void EntferneBestaetigte(IEnumerable<long> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var id in ids) _items.Remove(id);
    }

    public void Clear() => _items.Clear();

    private static void Pruefe(TourAntwort tour)
    {
        if (tour.Id <= 0 || tour.Werte is null || tour.Version is null || tour.Version.Length != 64 || !tour.Version.All(Uri.IsHexDigit) ||
            tour.Werte.KategorieIds is null || tour.Werte.KategorieIds.Any(id => id <= 0) || tour.Werte.KategorieIds.Distinct().Count() != tour.Werte.KategorieIds.Length)
            throw new ArgumentException("Tour-Snapshot ist ungültig.", nameof(tour));
    }

    private static TourAntwort Copy(TourAntwort tour) =>
        tour with { Werte = tour.Werte with { KategorieIds = (long[])tour.Werte.KategorieIds.Clone() } };
}
