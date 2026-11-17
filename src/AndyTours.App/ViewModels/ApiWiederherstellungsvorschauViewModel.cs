namespace AndyTours.App.ViewModels;

using System.Collections.ObjectModel;
using System.IO;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>Non-destructive restore-preview state; exposes no restore/apply operation.</summary>
public sealed partial class ApiWiederherstellungsvorschauViewModel : ObservableObject, IDisposable
{
    private readonly ISicherungskatalog _katalog;
    private readonly IWiederherstellungsvorschau _vorschauDienst;
    private readonly CancellationTokenSource _lifetime;
    private bool _disposed;
    private int _revision;

    [ObservableProperty] private bool _istBeschaeftigt;
    [ObservableProperty] private string _status = "Sicherungen laden, um eine Vorschau anzufordern.";
    [ObservableProperty] private ReadOnlyCollection<SicherungAntwort> _eintraege = Array.AsReadOnly(Array.Empty<SicherungAntwort>());
    [ObservableProperty] private SicherungAntwort? _auswahl;
    [ObservableProperty] private WiederherstellungsvorschauAntwort? _vorschau;
    [ObservableProperty] private bool _mussNeuLaden = true;

    public ApiWiederherstellungsvorschauViewModel(ISicherungskatalog katalog,
        IWiederherstellungsvorschau vorschauDienst, CancellationToken lifetime = default)
    {
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _vorschauDienst = vorschauDienst ?? throw new ArgumentNullException(nameof(vorschauDienst));
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
    }

    private bool CanLaden() => !_disposed && !IstBeschaeftigt;
    private bool CanVorschau() => !_disposed && !IstBeschaeftigt && !MussNeuLaden && Auswahl is not null && Eintraege.Contains(Auswahl);

    partial void OnIstBeschaeftigtChanged(bool value) => NotifyCommands();
    partial void OnMussNeuLadenChanged(bool value) => VorschauCommand.NotifyCanExecuteChanged();
    partial void OnAuswahlChanged(SicherungAntwort? value)
    {
        if (_disposed) return;
        _revision++;
        Vorschau = null;
        if (!_disposed && value is not null) Status = "Auswahl geändert; Vorschau erneut anfordern.";
        VorschauCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLaden))]
    public async Task LadenAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLaden()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        IstBeschaeftigt = true;
        _revision++;
        Auswahl = null;
        Vorschau = null;
        MussNeuLaden = true;
        Status = "Sicherungskatalog wird geladen.";
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var response = await _katalog.Liste(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            if (response?.Eintraege is not { } source) throw new InvalidDataException();
            var items = source.ToArray();
            if (items.Any(x => !WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(x)) ||
                items.Select(x => x.Id).Distinct().Count() != items.Length)
                throw new InvalidDataException();
            Eintraege = Array.AsReadOnly(items);
            MussNeuLaden = false;
            Status = Eintraege.Count == 0 ? "Keine Sicherungen verfügbar." : "Sicherung auswählen und Vorschau anfordern.";
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) Status = "Laden abgebrochen; bitte erneut laden.";
        }
        catch (Exception)
        {
            if (!_disposed) Status = "Sicherungskatalog ist nicht verfügbar oder ungültig.";
        }
        finally { if (!_disposed) IstBeschaeftigt = false; }
    }

    [RelayCommand(CanExecute = nameof(CanVorschau))]
    public async Task VorschauAsync(CancellationToken cancellationToken = default)
    {
        if (!CanVorschau() || Auswahl is not { } selected) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        IstBeschaeftigt = true;
        Vorschau = null;
        var revision = _revision;
        var request = new WiederherstellungsvorschauAnfrage(selected.Id, selected.InhaltHash);
        Status = "Metadatenvorschau wird geladen.";
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var result = await _vorschauDienst.Pruefen(request, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (_disposed || revision != _revision || Auswahl?.Id != selected.Id) return;
            if (!WiederherstellungsvorschauRegeln.IstMetadatenAntwortGueltig(result, request))
            {
                Status = "Vorschau-Antwort ist ungültig; keine Freigabe.";
                return;
            }
            Vorschau = result with { Hinweise = Array.AsReadOnly(result.Hinweise.ToArray()) };
            Status = "Nichtdestruktive Metadatenvorschau; keine Anwendungsfreigabe.";
        }
        catch (AndyTours.Application.Stammdaten.StammdatenException ex)
        {
            if (_disposed || revision != _revision) return;
            if (ex.Art is AndyTours.Application.Stammdaten.StammdatenFehlerart.NichtGefunden or AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt)
            {
                MussNeuLaden = true;
                Status = "Sicherungsstand hat sich geändert; Katalog neu laden.";
            }
            else Status = "Vorschauanforderung ungültig.";
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && revision == _revision) Status = "Vorschau abgebrochen.";
        }
        catch (Exception)
        {
            if (!_disposed && revision == _revision) Status = "Vorschau derzeit nicht verfügbar.";
        }
        finally { if (!_disposed) IstBeschaeftigt = false; }
    }

    private void NotifyCommands()
    {
        LadenCommand.NotifyCanExecuteChanged();
        VorschauCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        NotifyCommands();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
