using AndyTours.App.Funktionen;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public partial class ApiFitImportViewModel
{
    private readonly List<ApiGeraeteDateiErgebnis> _geraetePuffer = [];
    private Task? _pufferCleanup;

    private bool KannGeraeteLesen() => Idle();
    partial void NotifyGeraeteCommands() => GeraeteLesenCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(KannGeraeteLesen))]
    private async Task GeraeteLesenAsync()
    {
        if (!KannGeraeteLesen()) return;
        Aendert = true;
        try
        {
            var progress = new Progress<(int Fertig, int Gesamt)>(p =>
            {
                if (IsAlive(_lifetime.Token)) Status = $"Geräte-Dateien werden kopiert: {p.Fertig} von {p.Gesamt}.";
            });
            var result = await ApiGeraeteDateien.Kopieren(_lifetime.Token, progress);
            if (!IsAlive(_lifetime.Token))
            {
                result.Dispose();
                return;
            }
            _geraetePuffer.Add(result);
            Aendert = false;
            Hinzufuegen(result.Dateien.Select(x => x.Pfad));
            var summary = result.GeraeteGefunden
                ? $"{result.Dateien.Count} Geräte-Dateien kopiert."
                : "Kein unterstütztes Gerät gefunden.";
            if (result.Fehler.Count > 0)
            {
                var shown = result.Fehler.Take(20).ToList();
                summary += " " + string.Join(" ", shown);
                if (result.Fehler.Count > shown.Count)
                    summary += $" Weitere {result.Fehler.Count - shown.Count} Geräte-Dateien mit Fehlern.";
            }
            Status = summary;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch
        {
            if (IsAlive(CancellationToken.None)) Status = "Geräte-Dateien konnten nicht kopiert werden.";
        }
        finally
        {
            if (IsAlive(CancellationToken.None)) Aendert = false;
        }
    }

    partial void DisposeGeraete()
    {
        var buffers = _geraetePuffer.ToArray();
        _geraetePuffer.Clear();
        var uploadTask = HochladenCommand.ExecutionTask;
        var deviceTask = GeraeteLesenCommand.ExecutionTask;
        _pufferCleanup = CleanupBuffersAsync(buffers, uploadTask, deviceTask);
    }

    private static async Task CleanupBuffersAsync(
        ApiGeraeteDateiErgebnis[] buffers,
        Task? uploadTask,
        Task? deviceTask)
    {
        try { if (uploadTask is not null) await uploadTask; } catch { }
        try { if (deviceTask is not null) await deviceTask; } catch { }
        foreach (var buffer in buffers) buffer.Dispose();
    }
}
