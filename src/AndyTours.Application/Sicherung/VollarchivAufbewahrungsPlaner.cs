namespace AndyTours.Application.Sicherung;

using System.Collections.ObjectModel;
using AndyTours.Contracts.V1;

/// <summary>Pure retention preview. A plan does not grant deletion authority.</summary>
public sealed record VollarchivAufbewahrungsplan
{
    internal VollarchivAufbewahrungsplan(
        IReadOnlyList<SicherungAntwort> behalten,
        IReadOnlyList<SicherungAntwort> vorgeschlageneKandidaten)
    {
        Behalten = ReadOnly(behalten);
        VorgeschlageneKandidaten = ReadOnly(vorgeschlageneKandidaten);
    }

    /// <summary>Validated full-archive entries retained by the configured count.</summary>
    public IReadOnlyList<SicherungAntwort> Behalten { get; }

    /// <summary>Older validated full archives suggested for review only; never deletion authority.</summary>
    public IReadOnlyList<SicherungAntwort> VorgeschlageneKandidaten { get; }

    private static ReadOnlyCollection<SicherungAntwort> ReadOnly(IReadOnlyList<SicherungAntwort> source) =>
        new ReadOnlyCollection<SicherungAntwort>(source.ToArray());
}

/// <summary>Builds deterministic retention previews for validated full-archive metadata only.</summary>
public static class VollarchivAufbewahrungsPlaner
{
    /// <summary>
    /// Returns null for inconsistent snapshots. Legacy and invalid entries are never retention candidates.
    /// The fresh archive must be present and valid, and is always among the retained entries.
    /// </summary>
    public static VollarchivAufbewahrungsplan? Erstelle(
        IReadOnlyList<SicherungAntwort>? snapshot,
        int anzahlBehalten,
        SicherungAntwort? frischErstellt)
    {
        if (snapshot is null || frischErstellt is null || anzahlBehalten is < 1 or > 99 ||
            frischErstellt.Art != Sicherungsart.Vollarchiv ||
            !WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(frischErstellt) ||
            snapshot.Any(item => item is null) ||
            snapshot.Select(item => item.Id).Distinct().Count() != snapshot.Count)
            return null;

        var copied = snapshot.ToArray();
        if (!copied.Any(item => Same(item, frischErstellt))) return null;

        var validFull = copied
            .Where(item => item.Art == Sicherungsart.Vollarchiv &&
                           WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(item))
            .OrderByDescending(item => item.ErstelltUtc)
            .ThenBy(item => item.Id)
            .ToArray();
        var freshIndex = Array.FindIndex(validFull, item => Same(item, frischErstellt));
        if (freshIndex < 0) return null;

        var retained = validFull.Take(anzahlBehalten).ToList();
        if (freshIndex >= anzahlBehalten)
        {
            retained[^1] = validFull[freshIndex];
            retained = retained.OrderByDescending(item => item.ErstelltUtc).ThenBy(item => item.Id).ToList();
        }
        var retainedIds = retained.Select(item => item.Id).ToHashSet();
        var candidates = validFull.Where(item => !retainedIds.Contains(item.Id)).ToArray();
        return new VollarchivAufbewahrungsplan(retained, candidates);
    }

    private static bool Same(SicherungAntwort left, SicherungAntwort right) =>
        left.Id == right.Id && left.Art == right.Art && left.ErstelltUtc == right.ErstelltUtc &&
        left.Byteanzahl == right.Byteanzahl &&
        string.Equals(left.InhaltHash, right.InhaltHash, StringComparison.OrdinalIgnoreCase) &&
        left.LegacyZeitstempel == right.LegacyZeitstempel;
}
