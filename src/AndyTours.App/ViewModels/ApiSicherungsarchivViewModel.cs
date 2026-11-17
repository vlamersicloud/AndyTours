namespace AndyTours.App.ViewModels;

using System.IO;
using System.Net;
using System.Net.Http;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Client.Sicherung;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class ApiSicherungsarchivViewModel : ObservableObject, IDisposable
{
    private readonly ISicherungsarchivErstellung _creator;
    private readonly ISicherungsjournal _journal;
    private readonly CancellationTokenSource _lifetime;
    private bool _disposed;

    [ObservableProperty] private bool _istBeschaeftigt;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private Guid? _vorgangsId;
    [ObservableProperty] private SicherungAntwort? _ergebnis;

    public ApiSicherungsarchivViewModel(
        ISicherungsarchivErstellung creator,
        ISicherungsjournal journal,
        CancellationToken lifetime = default)
    {
        _creator = creator ?? throw new ArgumentNullException(nameof(creator));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
    }

    private bool CanErstellen() => !_disposed && !IstBeschaeftigt;

    partial void OnIstBeschaeftigtChanged(bool value) => ErstellenCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanErstellen))]
    public async Task ErstellenAsync(CancellationToken cancellationToken = default)
    {
        if (!CanErstellen()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        IstBeschaeftigt = true;
        Ergebnis = null;
        ErstellenCommand.NotifyCanExecuteChanged();
        Guid? activeId = null;

        try
        {
            using var lease = await _journal.Oeffnen(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            var id = lease.VorgangsId;
            if (id == Guid.Empty) throw new IOException("Sicherungsjournal ist ungültig.");
            activeId = id;
            VorgangsId = id;
            Status = "Vollarchiv wird erstellt oder mit derselben Vorgangs-ID erneut versucht.";
            var response = await _creator.Erstellen(new SicherungsarchivErstellungsanfrage(id), linked.Token);

            if (!Valid(response, id))
            {
                if (!_disposed) Status = "Antwort ungültig; Vorgangs-ID bleibt für einen sicheren Retry gespeichert.";
                return;
            }

            try
            {
                await lease.Bestaetigen(id, CancellationToken.None);
            }
            catch (Exception)
            {
                if (!_disposed)
                {
                    Ergebnis = response.Sicherung;
                    Status = "Archiv erstellt und geprüft; lokaler Vorgangsabschluss fehlgeschlagen. Erneut mit derselben Vorgangs-ID versuchen.";
                }
                return;
            }

            if (_disposed) return;
            Ergebnis = response.Sicherung;
            VorgangsId = null;
            Status = response.BereitsVorhanden
                ? "Vorhandenes Vollarchiv für diesen Vorgang geprüft."
                : "Vollarchiv erstellt und geprüft.";
        }
        catch (OperationCanceledException)
        {
            if (!_disposed)
                Status = "Vorgang abgebrochen oder Ergebnis unklar; dieselbe Vorgangs-ID für einen Retry beibehalten.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            if (!_disposed)
            {
                VorgangsId = activeId ?? VorgangsId;
                Status = "Vollarchiv-Erstellung ist serverseitig nicht verfügbar; Vorgangs-ID bleibt für einen Retry gespeichert.";
            }
        }
        catch (StammdatenException ex)
        {
            if (!_disposed)
            {
                VorgangsId = activeId ?? VorgangsId;
                Status = ex.Art == AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt
                    ? "Sicherungsvorgang steht im Konflikt; Vorgangs-ID bleibt gespeichert."
                    : "Sicherungsanfrage ungültig; Vorgangs-ID bleibt gespeichert.";
            }
        }
        catch (Exception)
        {
            if (!_disposed)
            {
                VorgangsId = activeId ?? VorgangsId;
                Status = "Ergebnis unklar oder Sicherungsjournal nicht verfügbar; keine neue Vorgangs-ID erzeugen.";
            }
        }
        finally
        {
            if (!_disposed) IstBeschaeftigt = false;
        }
    }

    private static bool Valid(SicherungsarchivErstellungsantwort? response, Guid id)
    {
        var backup = response?.Sicherung;
        return backup is not null && backup.Id == id &&
               backup.Art == Sicherungsart.Vollarchiv &&
               backup.ErstelltUtc is { Offset: var offset } && offset == TimeSpan.Zero &&
               backup.LegacyZeitstempel is null && backup.Byteanzahl > 0 &&
               backup.InhaltHash is { Length: 64 } && backup.InhaltHash.All(Uri.IsHexDigit);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        ErstellenCommand.NotifyCanExecuteChanged();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
