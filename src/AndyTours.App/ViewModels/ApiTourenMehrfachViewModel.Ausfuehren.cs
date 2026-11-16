using AndyTours.Application.Touren;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiTourenMehrfachViewModel
{
    [RelayCommand(CanExecute = nameof(KannAusfuehren))]
    private async Task AusfuehrenAsync(CancellationToken cancellationToken)
    {
        if (!KannAusfuehren || _senden is null) return;
        if (cancellationToken.IsCancellationRequested || _lebensdauer.IsCancellationRequested)
            return;

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauer);
        _operation = operation;
        var send = _senden;
        var token = operation.Token;
        Hinweis = null;
        Antwort = null;
        OnPropertyChanged(nameof(Hinweis));
        OnPropertyChanged(nameof(Antwort));
        SetzeStatus(ApiTourenMehrfachStatus.Speichert);
        try
        {
            var answer = await send(token);
            if (_disposed) return;
            if (!GueltigeAntwort(answer))
            {
                Unklar();
            }
            else
            {
                Antwort = answer;
                OnPropertyChanged(nameof(Antwort));
                Hinweis = $"Bestätigt: {_tourIds.Count} Tour(en) verarbeitet.";
                OnPropertyChanged(nameof(Hinweis));
                SetzeStatus(ApiTourenMehrfachStatus.Bestaetigt);
            }
        }
        catch (UngueltigeTourException)
        {
            Abgelehnt();
        }
        catch (TourNichtGefundenException)
        {
            Abgelehnt();
        }
        catch (TourVersionskonfliktException)
        {
            Abgelehnt();
        }
        catch (OperationCanceledException)
        {
            Unklar();
        }
        catch (Exception)
        {
            Unklar();
        }
        finally
        {
            if (ReferenceEquals(_operation, operation))
            {
                _operation = null;
                if (!_disposed)
                {
                    OnPropertyChanged(nameof(KannAbbrechen));
                    AbbrechenCommand.NotifyCanExecuteChanged();
                }
            }
            if (!_disposed && _status == ApiTourenMehrfachStatus.Speichert)
                Unklar();
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen()
    {
        if (!KannAbbrechen) return;
        try { _operation?.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { }
        OnPropertyChanged(nameof(KannAbbrechen));
        AbbrechenCommand.NotifyCanExecuteChanged();
    }

    private void Abgelehnt()
    {
        if (_disposed) return;
        Hinweis = "Die Anfrage wurde abgelehnt. Bitte Auswahl neu laden und prüfen.";
        OnPropertyChanged(nameof(Hinweis));
        SetzeStatus(ApiTourenMehrfachStatus.Abgelehnt);
    }

    private void Unklar()
    {
        if (_disposed) return;
        Hinweis = "Der Ausgang ist ungewiss. Nur dieselbe Anfrage erneut senden oder neu laden.";
        OnPropertyChanged(nameof(Hinweis));
        SetzeStatus(ApiTourenMehrfachStatus.Unklar);
    }

    private bool GueltigeAntwort(Contracts.V1.TourenMehrfachAntwort? answer)
    {
        if (answer is null || answer.AnfrageId != _anfrageId ||
            answer.Eintraege is null || answer.GeloeschteIds is null)
            return false;
        var expected = _tourIds.Order().ToArray();
        if (_loeschen)
            return answer.Eintraege.Count == 0 &&
                answer.GeloeschteIds.Order().SequenceEqual(expected) &&
                answer.GeloeschteIds.All(id => id > 0) &&
                answer.GeloeschteIds.Distinct().Count() == answer.GeloeschteIds.Count;
        var ids = answer.Eintraege.Select(x => x?.Id ?? 0).Order().ToArray();
        return answer.GeloeschteIds.Count == 0 &&
            ids.SequenceEqual(expected) && ids.All(id => id > 0) &&
            ids.Distinct().Count() == ids.Length &&
            answer.Eintraege.All(x => x is not null && x.Werte is not null &&
                x.Version is { Length: 64 } && x.Version.All(Uri.IsHexDigit));
    }

    partial void OnStatusGeaendert()
    {
        AusfuehrenCommand.NotifyCanExecuteChanged();
        AbbrechenCommand.NotifyCanExecuteChanged();
    }
}
