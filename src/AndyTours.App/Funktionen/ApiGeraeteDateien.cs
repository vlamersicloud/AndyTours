using System.IO;
using AndyTours.Application.Import;
using AndyTours.Import.Garmin;

namespace AndyTours.App.Funktionen;

/// <summary>Copies device recordings to owned temporary files without parsing or modifying device contents.</summary>
public static class ApiGeraeteDateien
{
    public static Task<ApiGeraeteDateiErgebnis> Kopieren(
        CancellationToken cancellationToken,
        IProgress<(int Fertig, int Gesamt)>? progress = null) =>
        Kopieren(() => [.. FindeLaufwerksgeraete.Ausfuehren(), .. FindeMtpGeraete.Ausfuehren()], cancellationToken, progress);

    public static Task<ApiGeraeteDateiErgebnis> Kopieren(
        IEnumerable<Geraeteaufzeichnung> aufzeichnungen,
        CancellationToken cancellationToken,
        IProgress<(int Fertig, int Gesamt)>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(aufzeichnungen);
        var snapshot = aufzeichnungen.ToArray();
        return Kopieren(() => snapshot, cancellationToken, progress);
    }

    private static Task<ApiGeraeteDateiErgebnis> Kopieren(
        Func<IEnumerable<Geraeteaufzeichnung>> enumerate,
        CancellationToken cancellationToken,
        IProgress<(int Fertig, int Gesamt)>? progress) =>
        Task.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "AndyToursApiDevice-" + Guid.NewGuid().ToString("N"));
            var copied = new List<ApiGeraeteDatei>();
            var errors = new List<string>();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(root);
                var items = enumerate().ToArray();
                var total = items.Length;
                if (total == 0) return new ApiGeraeteDateiErgebnis(root, copied, errors, false);
                var done = 0;
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var safeName = FitUploadRegeln.Dateiname(item.Dateiname);
                        var itemRoot = Path.Combine(root, Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(itemRoot);
                        var destination = Path.Combine(itemRoot, safeName);
                        item.KopiereNach(destination);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!File.Exists(destination)) throw new IOException();
                        if (new FileInfo(destination).Length > FitUploadRegeln.MaxDateiBytes)
                        {
                            File.Delete(destination);
                            errors.Add($"{safeName}: Datei überschreitet die zulässige Größe.");
                        }
                        else copied.Add(new ApiGeraeteDatei(destination, safeName, item.Geraet));
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or AndyTours.Application.Stammdaten.StammdatenException)
                    {
                        errors.Add("Eine Geräte-Datei konnte nicht kopiert werden.");
                    }
                    done++;
                    progress?.Report((done, total));
                }
                return new ApiGeraeteDateiErgebnis(root, copied, errors, true);
            }
            catch
            {
                TryDelete(root);
                throw;
            }
        }, cancellationToken);

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed record ApiGeraeteDatei(string Pfad, string Dateiname, string Geraet);

public sealed class ApiGeraeteDateiErgebnis : IDisposable
{
    private readonly string _root;
    private bool _disposed;
    public IReadOnlyList<ApiGeraeteDatei> Dateien { get; }
    public IReadOnlyList<string> Fehler { get; }
    public bool GeraeteGefunden { get; }

    internal ApiGeraeteDateiErgebnis(string root, IReadOnlyList<ApiGeraeteDatei> dateien, IReadOnlyList<string> fehler, bool geraeteGefunden)
    {
        _root = root;
        Dateien = dateien;
        Fehler = fehler;
        GeraeteGefunden = geraeteGefunden;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        TryDelete(_root);
        GC.SuppressFinalize(this);
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
