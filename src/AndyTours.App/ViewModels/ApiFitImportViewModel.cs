using System.Collections.ObjectModel;
using System.IO;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>FIT upload queue and transport state; review is handled by a later partial.</summary>
public partial class ApiFitImportViewModel : ObservableObject, IDisposable
{
    private readonly IFitUploads _uploads;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _editorOffen;
    [ObservableProperty] private bool _hatAenderungen;

    public ObservableCollection<ApiFitImportDatei> Dateien { get; } = [];

    public ApiFitImportViewModel(IFitUploads uploads) =>
        _uploads = uploads ?? throw new ArgumentNullException(nameof(uploads));

    private bool Idle() => !_disposed && !Laedt && !Aendert && !EditorOffen;
    private bool KannHochladen() => Idle() && Dateien.Any(x => !x.Hochgeladen);

    public void Hinzufuegen(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (!Idle()) return;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var fullPath = Path.GetFullPath(path);
            if (Dateien.Any(x => string.Equals(x.LokalerPfad, fullPath, comparison))) continue;
            var name = Path.GetFileName(fullPath);
            Dateien.Add(new ApiFitImportDatei(fullPath, name));
        }
        NotifyCommands();
    }

    [RelayCommand(CanExecute = nameof(KannHochladen))]
    private async Task HochladenAsync()
    {
        if (!KannHochladen()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        Laedt = true;
        try
        {
            foreach (var file in Dateien.Where(x => !x.Hochgeladen).ToArray())
            {
                if (!IsAlive(linked.Token)) return;
                try
                {
                    await using var stream = new FileStream(file.LokalerPfad, FileMode.Open, FileAccess.Read, FileShare.Read,
                        81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    var uploaded = await _uploads.Hochladen(file.UploadId, file.Dateiname, stream, linked.Token);
                    if (!IsAlive(linked.Token)) return;
                    file.Uploaded = uploaded;
                    file.Status = "Hochgeladen";
                    Status = $"{Dateien.Count(x => x.Hochgeladen)} von {Dateien.Count} Dateien hochgeladen.";
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested) { return; }
                catch (StammdatenException ex)
                {
                    if (!IsAlive(linked.Token)) return;
                    file.Status = ex.Art == StammdatenFehlerart.Konflikt ? "Konflikt" : "Datei abgelehnt";
                    Status = file.Status + "; übrige Dateien werden weiterverarbeitet.";
                }
                catch (FitUploadZuGrossException)
                {
                    if (!IsAlive(linked.Token)) return;
                    file.Status = "Datei zu groß";
                    Status = "Datei zu groß; übrige Dateien werden weiterverarbeitet.";
                }
                catch
                {
                    if (!IsAlive(linked.Token)) return;
                    file.Status = "Upload fehlgeschlagen; manueller Wiederholungsversuch möglich.";
                    Status = file.Status;
                }
            }
        }
        finally
        {
            if (IsAlive(CancellationToken.None)) Laedt = false;
        }
    }

    private bool IsAlive(CancellationToken token) => !_disposed && !token.IsCancellationRequested;

    private void NotifyCommands()
    {
        HochladenCommand.NotifyCanExecuteChanged();
        NotifyPruefCommands();
    }
    partial void NotifyPruefCommands();
    partial void OnLaedtChanged(bool value) => NotifyCommands();
    partial void OnAendertChanged(bool value) => NotifyCommands();
    partial void OnEditorOffenChanged(bool value) => NotifyCommands();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        NotifyCommands();
        GC.SuppressFinalize(this);
    }
}

/// <summary>A queued local file; its source path is internal and never part of an API DTO.</summary>
public sealed partial class ApiFitImportDatei : ObservableObject
{
    internal string LokalerPfad { get; }
    public Guid UploadId { get; } = Guid.NewGuid();
    public string Dateiname { get; }
    public bool Hochgeladen => Uploaded is not null;

    [ObservableProperty] private string _status = "Bereit";
    [ObservableProperty] private bool _abgeschlossen;
    private FitUploadAntwort? _uploaded;
    internal FitUploadAntwort? Uploaded
    {
        get => _uploaded;
        set { if (SetProperty(ref _uploaded, value)) OnPropertyChanged(nameof(Hochgeladen)); }
    }

    internal ApiFitImportDatei(string lokalerPfad, string dateiname)
    {
        LokalerPfad = lokalerPfad;
        Dateiname = dateiname;
    }
}
