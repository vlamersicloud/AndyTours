using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    private TourenSortierschluessel _tourenSortierung = TourenSortierschluessel.Datum;
    private bool _tourenAbsteigend = true;

    public TourenSortierschluessel TourenSortierung => _tourenSortierung;
    public bool TourenAbsteigend => _tourenAbsteigend;

    public async Task<bool> SortiereTourenAsync(TourenSortierschluessel schluessel, bool absteigend, CancellationToken ct = default)
    {
        if (Laedt || EditorOffen || VonDatum is null || BisDatum is null || VonDatum > BisDatum ||
            !Enum.IsDefined(schluessel) || schluessel == TourenSortierschluessel.Bestand)
        {
            StatusText = "Sortierung kann derzeit nicht geändert werden. Filter und Eingaben prüfen.";
            return false;
        }

        ct.ThrowIfCancellationRequested();
        var von = DateOnly.FromDateTime(VonDatum.Value);
        var bis = DateOnly.FromDateTime(BisDatum.Value);
        var aktivitaetId = AktivitaetAuswahl?.Id;
        var kategorieId = KategorieAuswahl?.Id;
        var tabellenfilterSnapshot = Tabellenfilter.ErstelleAnfrage();
        var filter = new TourenListenfilter(von, bis, aktivitaetId, kategorieId)
        { Sortierung = schluessel, Absteigend = absteigend, Tabellenfilter = tabellenfilterSnapshot };
        var generation = Interlocked.Increment(ref _generation);
        Laedt = true;
        try
        {
            var page = await _touren.ListeGefiltert(0, 50, filter, ct);
            ct.ThrowIfCancellationRequested();
            if (generation != Interlocked.Read(ref _generation)) return false;
            ValidiereSortierantwort(page);
            var tablePlan = ApiTabellenfilterOptionenPlan.Erstellen(page.TabellenfilterOptionen,
                tabellenfilterSnapshot?.EquipmentName, tabellenfilterSnapshot?.Trainingsbereich);

            _tourenSortierung = schluessel;
            OnPropertyChanged(nameof(TourenSortierung));
            _tourenAbsteigend = absteigend;
            OnPropertyChanged(nameof(TourenAbsteigend));
            Tabellenfilter.Uebernehmen(tablePlan);
            _start = 0;
            SetzeSeite(page);
            StatusText = page.Gesamt == 0 ? "Keine Touren im gewählten Zeitraum und Filter." : string.Empty;
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (ApiTourSeitenException)
        {
            if (generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested)
                StatusText = "Tourenseite ist unvollständig oder ungültig. Bitte Backend-Version und Verbindung prüfen.";
            return false;
        }
        catch (Exception)
        {
            if (generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested) StatusText = "Sortierte Tourenseite konnte nicht geladen werden; bisherige Sortierung bleibt aktiv.";
            return false;
        }
        finally { Laedt = false; }
    }

    private static void ValidiereSortierantwort(TourListeAntwort? page) =>
        ApiTourSeitenRegeln.Validieren(page, 0, 50);
}
