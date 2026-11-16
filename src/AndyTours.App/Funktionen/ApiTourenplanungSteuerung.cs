using System.IO;
using System.Windows;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Win32;

namespace AndyTours.App.Funktionen;

/// <summary>Owns API-only planning commands for one modeless planning window.</summary>
public sealed class ApiTourenplanungSteuerung : IDisposable
{
    private readonly TourenplanungFenster _window;
    private readonly TourenplanungViewModel _vm;
    private readonly IPlanungsDateien _files;
    private readonly IPlanungsCourseExport _courses;
    private readonly ApiPlanungsProfilSitzung _profile;
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _lifetimeToken;
    private bool _disposed;
    private int _profileOperations;

    public ApiTourenplanungSteuerung(TourenplanungFenster window, TourenplanungViewModel vm, IPlanungsDateien files,
        IPlanungsCourseExport courses, ApiPlanungsProfilSitzung profile, CancellationToken parentToken = default)
    {
        _window = window; _vm = vm; _files = files; _courses = courses; _profile = profile;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        _lifetimeToken = _lifetime.Token;
        _vm.ApiOeffnen = Oeffnen; _vm.ApiSpeichern = Speichern; _vm.ApiGarminUebertragen = Garmin;
        _vm.ApiProfilGeaendert = ProfilGeaendert;
        _vm.ApiProfilSynchronisieren = ProfilSynchronisieren;
        _profile.Geaendert += ProfilAktualisiert;
        _window.Closed += Closed;
        _vm.ApiProfilHinweis = profile.Hinweis;
    }

    private async Task ProfilSynchronisieren()
    {
        try { await MitProfilstatus(() => _profile.Setzen(ToContract(_vm.Profil), _lifetimeToken)); }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
        catch { if (!_disposed) _vm.ApiProfilHinweis = "Planungsprofil konnte nicht synchronisiert werden."; }
    }

    private async void ProfilGeaendert(Fahrprofil profil)
    {
        try { await MitProfilstatus(() => _profile.Setzen(ToContract(profil), _lifetimeToken)); }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
        catch { if (!_disposed) _vm.ApiProfilHinweis = "Planungsprofil konnte nicht gespeichert werden."; }
    }
    private async Task MitProfilstatus(Func<Task> operation)
    {
        if (_disposed) return;
        if (Interlocked.Increment(ref _profileOperations) == 1) _vm.ApiProfilSpeichert = true;
        try { await operation(); }
        finally
        {
            if (Interlocked.Decrement(ref _profileOperations) == 0 && !_disposed) _vm.ApiProfilSpeichert = false;
        }
    }

    private void ProfilAktualisiert(object? sender, EventArgs e) { if (!_disposed) _vm.ApiProfilHinweis = _profile.Hinweis; }

    private async Task Oeffnen() => await Datei(async ct =>
    {
        if (_vm.IstGeaendert && MessageBox.Show(_window, "Die Planung hat ungespeicherte Änderungen. Änderungen verwerfen?", "Tour planen", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var dialog = new OpenFileDialog { Title = "Planung öffnen", Filter = "GPX-Dateien (*.gpx)|*.gpx", CheckFileExists = true, InitialDirectory = _vm.ApiStartordner };
        if (dialog.ShowDialog(_window) != true) return;
        ct.ThrowIfCancellationRequested();
        await using var input = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var dto = await _files.Lesen(input, ct);
        ct.ThrowIfCancellationRequested();
        if (_disposed || !_window.IsVisible) return;
        var core = PlanungsVertragMapper.ToCore(dto, ct);
        if (!_disposed) _vm.UebernimmApiPlanung(core, dialog.FileName);
    });

    private async Task Speichern() => await Datei(async ct =>
    {
        var dialog = new SaveFileDialog { Title = "Planung speichern", Filter = "GPX-Dateien (*.gpx)|*.gpx", DefaultExt = ".gpx", AddExtension = true, OverwritePrompt = true,
            FileName = _vm.Dateipfad is null ? BildePlanungsdateiname.Ausfuehren(DateOnly.FromDateTime(DateTime.Today)) : Path.GetFileName(_vm.Dateipfad), InitialDirectory = _vm.ApiStartordner };
        if (dialog.ShowDialog(_window) != true) return;
        ct.ThrowIfCancellationRequested();
        Tourenplanung snapshot = _vm.Planung;
        var dto = PlanungsVertragMapper.ToDto(snapshot, ct);
        var response = await _files.Schreiben(new(Path.GetFileNameWithoutExtension(dialog.FileName), dto), ct);
        ct.ThrowIfCancellationRequested();
        if (_disposed || !_window.IsVisible) return;
        if (response.Inhalt is null || response.Inhalt.Length is 0 || response.Inhalt.LongLength > PlanungsDateiRegeln.MaxDateiBytes) throw Invalid();
        string target = Path.GetFullPath(dialog.FileName);
        string directory = Path.GetDirectoryName(target) ?? throw Invalid();
        string temp = Path.Combine(directory, ".andytours-planung-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            { await output.WriteAsync(response.Inhalt, ct); await output.FlushAsync(ct); }
            ct.ThrowIfCancellationRequested();
            if (_disposed) return;
            if (File.Exists(target)) File.Move(temp, target, true); else File.Move(temp, target);
            if (!_disposed) _vm.BestaetigeApiSpeichern(snapshot, target);
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    });

    private async Task Garmin() => await Datei(ct =>
    {
        ct.ThrowIfCancellationRequested();
        if (!_disposed) ApiPlanungsCourseUebertragung.Zeige(_window, _vm.Planung, BildeCoursename.FuerPlanung(_vm.Dateipfad, DateOnly.FromDateTime(DateTime.Today)), _courses, ct);
        return Task.CompletedTask;
    });

    private async Task Datei(Func<CancellationToken, Task> action)
    {
        if (_disposed || !_window.IsVisible || _vm.ApiDateiBeschaeftigt) return;
        _vm.ApiDateiBeschaeftigt = true;
        try { await action(_lifetimeToken); }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
        catch { if (!_disposed) _vm.Hinweis = "Der API-Dateivorgang konnte nicht abgeschlossen werden."; }
        finally { if (!_disposed) _vm.ApiDateiBeschaeftigt = false; }
    }

    private void Closed(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _lifetime.Cancel();
        _window.Closed -= Closed;
        _profile.Geaendert -= ProfilAktualisiert;
        if (_vm.ApiOeffnen == Oeffnen) _vm.ApiOeffnen = null;
        if (_vm.ApiSpeichern == Speichern) _vm.ApiSpeichern = null;
        if (_vm.ApiGarminUebertragen == Garmin) _vm.ApiGarminUebertragen = null;
        _vm.ApiProfilGeaendert = null;
        _vm.ApiProfilSynchronisieren = null;
        _vm.ApiProfilSpeichert = false;
        _profile.Dispose(); _lifetime.Dispose();
    }
    private static CourseFahrprofil ToContract(Fahrprofil p) => p switch { Fahrprofil.Rennrad => CourseFahrprofil.Rennrad, Fahrprofil.Trekking => CourseFahrprofil.Trekking, Fahrprofil.Mountainbike => CourseFahrprofil.Mountainbike, _ => throw Invalid() };
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungsdatei ungültig.");
}
