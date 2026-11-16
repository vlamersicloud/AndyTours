using System.Globalization;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>Pure API metadata tree construction and structural/state comparison helpers.</summary>
public static class ApiOverlayBaum
{
    private static readonly StringComparer Alphabetisch = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true);

    public static IReadOnlyList<ApiOverlayknoten> Erstellen(
        OverlayBestandAntwort bestand,
        Action<ApiOverlayknoten, bool> umschalten,
        IReadOnlyDictionary<long, bool>? aufgeklappt = null,
        long? ausgewaehlteOrdnerId = null,
        long? ausgewaehltesOverlayId = null)
    {
        if (bestand is null || bestand.Ordner is null || bestand.Overlays is null || umschalten is null) throw Invalid();
        var folders = bestand.Ordner.ToDictionary(x => x.Id);
        var items = bestand.Overlays.ToDictionary(x => x.Id);
        if (folders.Keys.Any(id => id <= 0) || items.Keys.Any(id => id <= 0) ||
            folders.Values.Any(x => string.IsNullOrWhiteSpace(x.Name)) || items.Values.Any(x => string.IsNullOrWhiteSpace(x.Name)) ||
            items.Values.Any(x => x.OrdnerId is { } parent && !folders.ContainsKey(parent))) throw Invalid();

        foreach (var folder in folders.Values)
        {
            var seen = new HashSet<long>();
            var current = folder;
            while (current.ElternId is { } parent)
            {
                if (!seen.Add(current.Id) || !folders.TryGetValue(parent, out current!)) throw Invalid();
            }
            if (!seen.Add(current.Id)) throw Invalid();
        }

        var nodes = folders.Values.ToDictionary(f => f.Id, f => new ApiOverlayknoten(f, umschalten));
        foreach (var meta in items.Values)
        {
            var color = meta.Farbe is { } own ? ApiOverlayFarboptionen.FarbeHex(own) : ApiOverlayFarboptionen.FarbeHex(bestand.GemeinsameFarbe);
            var node = new ApiOverlayknoten(meta, color, umschalten);
            if (meta.OrdnerId is { } parent) nodes[parent].Kinder.Add(node);
        }
        foreach (var folder in folders.Values)
            if (folder.ElternId is { } parent) nodes[parent].Kinder.Add(nodes[folder.Id]);

        foreach (var node in nodes.Values)
        {
            Sortiere(node.Kinder);
            if (aufgeklappt?.TryGetValue(node.Id, out bool expanded) == true) node.IstAufgeklappt = expanded;
        }
        var roots = nodes.Values.Where(n => n.Ordner!.ElternId is null).ToList();
        roots.AddRange(items.Values.Where(i => i.OrdnerId is null).Select(i =>
        {
            var color = i.Farbe is { } own ? ApiOverlayFarboptionen.FarbeHex(own) : ApiOverlayFarboptionen.FarbeHex(bestand.GemeinsameFarbe);
            return new ApiOverlayknoten(i, color, umschalten);
        }));
        Sortiere(roots);
        foreach (var root in roots) SetStates(root);
        var selected = ausgewaehltesOverlayId is { } overlayId
            ? Find(roots, overlayId, true) : ausgewaehlteOrdnerId is { } folderId ? Find(roots, folderId, false) : null;
        if (selected is not null) selected.IstAusgewaehlt = true;
        return roots.AsReadOnly();
    }

    public static IReadOnlyList<long> OverlayIds(ApiOverlayknoten node)
    {
        if (node is null) throw Invalid();
        var ids = new List<long>();
        Collect(node, ids);
        return ids.AsReadOnly();
    }

    public static bool Strukturgleich(IReadOnlyList<ApiOverlayknoten> roots, OverlayBestandAntwort bestand)
    {
        if (roots is null || bestand?.Ordner is null || bestand.Overlays is null) return false;
        var existingFolders = Flatten(roots).Where(n => n.IstOrdner).ToDictionary(n => n.Id);
        var existingItems = Flatten(roots).Where(n => n.IstOverlay).ToDictionary(n => n.Id);
        return existingFolders.Count == bestand.Ordner.Count && existingItems.Count == bestand.Overlays.Count &&
            bestand.Ordner.All(f => existingFolders.TryGetValue(f.Id, out var n) && n.Ordner!.ElternId == f.ElternId && n.Ordner.Name == f.Name) &&
            bestand.Overlays.All(i => existingItems.TryGetValue(i.Id, out var n) && n.Overlay!.OrdnerId == i.OrdnerId && n.Overlay.Name == i.Name);
    }

    public static void AktualisiereZustaende(IReadOnlyList<ApiOverlayknoten> roots, OverlayBestandAntwort bestand)
    {
        var map = Flatten(roots).ToDictionary(n => (n.Id, n.IstOverlay));
        foreach (var meta in bestand.Overlays)
        {
            var node = map[(meta.Id, true)];
            var hex = ApiOverlayFarboptionen.FarbeHex(meta.Farbe ?? bestand.GemeinsameFarbe);
            node.Aktualisiere(meta, hex);
        }
        foreach (var node in Flatten(roots).Where(n => n.IstOrdner)) node.ZeigeZustand(Berechne(node));
    }

    private static bool? Berechne(ApiOverlayknoten node)
    {
        var ids = OverlayIds(node);
        if (ids.Count == 0) return false;
        var values = Flatten(new[] { node }).Where(n => n.IstOverlay).Select(n => n.IstEingeschaltet == true).Distinct().ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    private static void SetStates(ApiOverlayknoten folder)
    {
        foreach (var child in folder.Kinder.Where(c => c.IstOrdner)) SetStates(child);
        folder.ZeigeZustand(Berechne(folder));
    }

    private static void Collect(ApiOverlayknoten node, List<long> ids)
    {
        if (node.IstOverlay) ids.Add(node.Id);
        foreach (var child in node.Kinder) Collect(child, ids);
    }

    private static IEnumerable<ApiOverlayknoten> Flatten(IEnumerable<ApiOverlayknoten> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Kinder)) yield return child;
        }
    }

    private static ApiOverlayknoten? Find(IEnumerable<ApiOverlayknoten> roots, long id, bool overlay) =>
        Flatten(roots).FirstOrDefault(n => n.Id == id && n.IstOverlay == overlay);

    private static void Sortiere(IList<ApiOverlayknoten> nodes)
    {
        var sorted = nodes.OrderByDescending(n => n.IstOrdner).ThenBy(n => n.Name, Alphabetisch).ToArray();
        nodes.Clear();
        foreach (var node in sorted) nodes.Add(node);
    }

    private static InvalidOperationException Invalid() => new("Overlay-Bestand ungültig.");
}
