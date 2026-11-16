using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    public ObservableCollection<ApiOverlayGpxDatei> Importdateien { get; } = [];
    public bool HatUnklareGpxImporte => Importdateien.Any(row => row.Unklar);
    public event EventHandler? GpxDateienAngefordert;

    [RelayCommand(CanExecute = nameof(KannGpxHinzufuegen))]
    private void GpxHinzufuegen() => GpxDateienAngefordert?.Invoke(this, EventArgs.Empty);

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
        var existing = Importdateien.Select(row => row.Pfad).ToHashSet(comparer);
        int added = 0, rejected = 0;
        foreach (string? selected in dateien)
        {
            if (string.IsNullOrWhiteSpace(selected)) { rejected++; continue; }
            string fullPath;
            try { fullPath = Path.GetFullPath(selected); }
            catch (Exception) { rejected++; continue; }
            if (!existing.Add(fullPath)) continue;
            string basename = Path.GetFileName(fullPath);
            try
            {
                string normalized = OverlayGpxRegeln.Anzeigename(basename);
                _ = normalized;
                // Keep the validated normalized filename, while the row displays the source basename.
                Importdateien.Add(new ApiOverlayGpxDatei(fullPath, basename, folderId, Guid.NewGuid()));
                added++;
            }
            catch (StammdatenException) { rejected++; }
            catch (ArgumentException) { rejected++; }
        }
        if (!_beendet)
            Hinweis = rejected == 0 ? $"{added} Datei(en) zur Importliste hinzugefügt." : $"{added} Datei(en) hinzugefügt; {rejected} ungültige Auswahl(en) übersprungen.";
    }

    private bool KannGpxHinzufuegen() => !_beendet && KannAendern && !IstBeschaeftigt && !HatUnklareGpxImporte;

    partial void BenachrichtigeGpxBefehle();

    private void GpxZeilenBereinigen()
    {
        Importdateien.Clear();
        GpxDateienAngefordert = null;
        OnPropertyChanged(nameof(HatUnklareGpxImporte));
    }
}
