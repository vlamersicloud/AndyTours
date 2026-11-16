using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    public ObservableCollection<ApiOverlayGpxDatei> Importdateien { get; } = [];
    public bool HatImportdateien => Importdateien.Count != 0;
    public bool HatUnklareGpxImporte => Importdateien.Any(row => row.Unklar);
    public event EventHandler? GpxDateienAngefordert;

    [RelayCommand(CanExecute = nameof(KannGpxHinzufuegen))]
    private void GpxHinzufuegen()
    {
        if (KannGpxHinzufuegen()) GpxDateienAngefordert?.Invoke(this, EventArgs.Empty);
    }

    public void GpxDateienHinzufuegen(IReadOnlyList<string> dateien)
    {
        ArgumentNullException.ThrowIfNull(dateien);
        if (_beendet || !KannAendern || IstBeschaeftigt || HatUnklareGpxImporte) return;
        long? folderId = Ausgewaehlt switch
        {
            { IstOrdner: true } folder => folder.Id,
            { IstOverlay: true } overlay => overlay.Overlay?.OrdnerId,
            _ => null
        };
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var pending = Importdateien.Where(row => !row.Fertig).ToArray();
        var existing = pending.Select(row => (row.Pfad, row.OrdnerId)).ToHashSet(new PendingComparer(comparer));
        int added = 0, rejected = 0;
        foreach (string? selected in dateien)
        {
            if (string.IsNullOrWhiteSpace(selected) || !Path.IsPathFullyQualified(selected)) { rejected++; continue; }
            string fullPath;
            try { fullPath = Path.GetFullPath(selected); }
            catch (Exception) { rejected++; continue; }
            if (!existing.Add((fullPath, folderId))) continue;
            string basename = Path.GetFileName(fullPath);
            try
            {
                var normalized = OverlayGpxRegeln.Normalisieren(new OverlayGpxImportAnfrage(
                    BestehenderBestand!.Version, Guid.NewGuid(), folderId, basename));
                Importdateien.Add(new ApiOverlayGpxDatei(fullPath, normalized.Dateiname, folderId, normalized.AnfrageId, normalized));
                Importdateien[^1].PropertyChanged += ImportZeileGeaendert;
                added++;
            }
            catch (StammdatenException) { rejected++; }
            catch (ArgumentException) { rejected++; }
        }
        if (!_beendet)
            Hinweis = rejected == 0 ? $"{added} Datei(en) zur Importliste hinzugefügt." : $"{added} Datei(en) hinzugefügt; {rejected} ungültige Auswahl(en) übersprungen.";
        OnPropertyChanged(nameof(HatImportdateien));
        BenachrichtigeBefehle();
    }

    private void ImportZeileGeaendert(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { if (e.PropertyName is nameof(ApiOverlayGpxDatei.Unklar) or nameof(ApiOverlayGpxDatei.Fertig)) { OnPropertyChanged(nameof(HatUnklareGpxImporte)); OnPropertyChanged(nameof(HatImportdateien)); BenachrichtigeBefehle(); } }

    private bool KannGpxHinzufuegen() => !_beendet && KannAendern && !IstBeschaeftigt && !HatUnklareGpxImporte;

    private sealed class PendingComparer(StringComparer pathComparer) : IEqualityComparer<(string Pfad, long? OrdnerId)>
    {
        public bool Equals((string Pfad, long? OrdnerId) x, (string Pfad, long? OrdnerId) y) =>
            x.OrdnerId == y.OrdnerId && pathComparer.Equals(x.Pfad, y.Pfad);
        public int GetHashCode((string Pfad, long? OrdnerId) obj) =>
            HashCode.Combine(pathComparer.GetHashCode(obj.Pfad), obj.OrdnerId);
    }

    private void GpxZeilenBereinigen()
    {
        foreach (var row in Importdateien) row.PropertyChanged -= ImportZeileGeaendert;
        Importdateien.Clear();
        GpxDateienAngefordert = null;
        OnPropertyChanged(nameof(HatUnklareGpxImporte));
        OnPropertyChanged(nameof(HatImportdateien));
    }
}
