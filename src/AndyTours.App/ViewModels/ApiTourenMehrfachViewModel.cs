using System.Collections.ObjectModel;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndyTours.App.ViewModels;

public enum ApiTourenMehrfachStatus
{
    Bereit,
    Speichert,
    Abgelehnt,
    Unklar,
    Bestaetigt
}

/// <summary>Pure state foundation for a confirmed, idempotent bulk-tour UI action.</summary>
public sealed partial class ApiTourenMehrfachViewModel : ObservableObject, IDisposable
{
    private readonly Guid _anfrageId;
    private readonly ReadOnlyCollection<long> _tourIds;
    private readonly bool _loeschen;
    private readonly CancellationToken _lebensdauer;
    private Func<CancellationToken, Task<TourenMehrfachAntwort>>? _senden;
    private CancellationTokenSource? _operation;
    private ApiTourenMehrfachStatus _status = ApiTourenMehrfachStatus.Bereit;
    private bool _disposed;

    private ApiTourenMehrfachViewModel(string titel, string beschreibung, Guid anfrageId,
        IEnumerable<long> tourIds, bool loeschen,
        Func<CancellationToken, Task<TourenMehrfachAntwort>> senden, CancellationToken lebensdauer)
    {
        Titel = titel;
        Beschreibung = beschreibung;
        _anfrageId = anfrageId;
        _tourIds = Array.AsReadOnly(tourIds.ToArray());
        _loeschen = loeschen;
        _senden = senden;
        _lebensdauer = lebensdauer;
    }

    public string Titel { get; }
    public string Beschreibung { get; }
    public Guid AnfrageId => _anfrageId;
    public IReadOnlyList<long> TourIds => _tourIds;
    public int Anzahl => _tourIds.Count;
    public bool IstLoeschen => _loeschen;
    public ApiTourenMehrfachStatus Status => _status;
    public bool IstBestaetigt => _status == ApiTourenMehrfachStatus.Bestaetigt;
    public bool IstUnklar => _status == ApiTourenMehrfachStatus.Unklar;
    public bool IstBeschaeftigt => !_disposed && _status == ApiTourenMehrfachStatus.Speichert;
    public bool KannAbbrechen => !_disposed && IstBeschaeftigt && _operation is { IsCancellationRequested: false };
    public bool KannAusfuehren => !_disposed && _senden is not null && !_lebensdauer.IsCancellationRequested &&
        _status is ApiTourenMehrfachStatus.Bereit or ApiTourenMehrfachStatus.Unklar;
    public string? Hinweis { get; private set; }
    public TourenMehrfachAntwort? Antwort { get; private set; }

    private void SetzeStatus(ApiTourenMehrfachStatus status)
    {
        if (_status == status) return;
        _status = status;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IstBestaetigt));
        OnPropertyChanged(nameof(IstUnklar));
        OnPropertyChanged(nameof(IstBeschaeftigt));
        OnPropertyChanged(nameof(KannAbbrechen));
        OnPropertyChanged(nameof(KannAusfuehren));
        OnStatusGeaendert();
    }

    partial void OnStatusGeaendert();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _senden = null;
        if (_operation is { IsCancellationRequested: false } operation)
        {
            try { operation.Cancel(); }
            catch (ObjectDisposedException) { }
            catch (AggregateException) { }
        }
        OnPropertyChanged(nameof(KannAusfuehren));
        OnPropertyChanged(nameof(IstBeschaeftigt));
        OnPropertyChanged(nameof(KannAbbrechen));
        AusfuehrenCommand.NotifyCanExecuteChanged();
        AbbrechenCommand.NotifyCanExecuteChanged();
        GC.SuppressFinalize(this);
    }
}
