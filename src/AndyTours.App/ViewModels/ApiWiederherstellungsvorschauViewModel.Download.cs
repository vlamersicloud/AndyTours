namespace AndyTours.App.ViewModels;

using System.IO;
using AndyTours.App.Funktionen;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;

public sealed partial class ApiWiederherstellungsvorschauViewModel
{
    private readonly ISicherungsdownloadClient? _downloadClient;
    private CancellationTokenSource? _activeDownload;
    private int _downloadGeneration;

    public bool KannDownloaden => !_disposed && !IstBeschaeftigt && !MussNeuLaden && _downloadClient is not null &&
        Auswahl is { } selected && Eintraege.Contains(selected) &&
        WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(selected);

    public string DownloadStatus { get; private set; } = "Download nicht gestartet.";

    public async Task<bool> DownloadNachAsync(string zielpfad, CancellationToken cancellationToken = default)
    {
        if (!KannDownloaden || Auswahl is not { } selected || _downloadClient is null) return false;
        var generation = ++_downloadGeneration;
        var selectedHash = selected.InhaltHash;
        using var active = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        _activeDownload?.Cancel();
        _activeDownload = active;
        IstBeschaeftigt = true;
        DownloadStatus = "Sicherung wird geladen und geprüft.";
        OnPropertyChanged(nameof(DownloadStatus));
        try
        {
            if (string.IsNullOrWhiteSpace(zielpfad) || !Path.IsPathFullyQualified(zielpfad))
                throw new IOException("Downloadziel ist ungültig.");
            var target = Path.GetFullPath(zielpfad);
            if (File.Exists(target))
                throw new IOException("Die Zieldatei existiert bereits und wird nicht überschrieben.");

            await SicherungsdownloadDatei.Schreiben(
                _downloadClient, selected, target,
                () => IsCurrentDownload(selected, selectedHash, generation), active.Token).ConfigureAwait(true);
            if (!IsCurrentDownload(selected, selectedHash, generation)) return false;
            DownloadStatus = "Sicherung vollständig heruntergeladen und SHA-256 geprüft.";
            OnPropertyChanged(nameof(DownloadStatus));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentDownload(selected, selectedHash, generation))
            {
                DownloadStatus = "Download abgebrochen; vorhandene Zieldateien blieben unverändert. Temporäre Bereinigung ist best effort.";
                OnPropertyChanged(nameof(DownloadStatus));
            }
            return false;
        }
        catch (Exception)
        {
            if (IsCurrentDownload(selected, selectedHash, generation))
            {
                DownloadStatus = "Download fehlgeschlagen; Ziel wurde nicht überschrieben. Temporäre Bereinigung ist best effort.";
                OnPropertyChanged(nameof(DownloadStatus));
            }
            return false;
        }
        finally
        {
            if (ReferenceEquals(_activeDownload, active)) _activeDownload = null;
            if (!_disposed) IstBeschaeftigt = false;
        }
    }

    private bool IsCurrentDownload(SicherungAntwort selected, string hash, int generation) =>
        !_disposed && generation == _downloadGeneration && Auswahl is { } current &&
        current.Id == selected.Id && string.Equals(current.InhaltHash, hash, StringComparison.Ordinal);
}
