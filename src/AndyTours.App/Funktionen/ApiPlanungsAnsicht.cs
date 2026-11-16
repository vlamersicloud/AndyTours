using System.IO;
using System.Text.Json;
using AndyTours.Core.Modell;

namespace AndyTours.App.Funktionen;

public sealed record ApiPlanungsAnsicht(Geoposition? SuedWest, Geoposition? NordOst, string? Ordner);

/// <summary>Local-only viewport and planning-folder preferences for API mode; never persists the profile.</summary>
public static class ApiPlanungsAnsichtSpeicher
{
    private const int MaxBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static string? Pfad
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) ? null : Path.Combine(root, "AndyTours", "api-planung.json");
        }
    }

    public static ApiPlanungsAnsicht Laden()
    {
        try
        {
            string? path = Pfad;
            if (path is null || !File.Exists(path)) return new(null, null, null);
            using var stream = File.OpenRead(path);
            using var bounded = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                int read = stream.Read(chunk, 0, chunk.Length);
                if (read == 0) break;
                if (bounded.Length + read > MaxBytes) return new(null, null, null);
                bounded.Write(chunk, 0, read);
            }
            bounded.Position = 0;
            var state = JsonSerializer.Deserialize<ApiPlanungsAnsicht>(bounded, JsonOptions);
            if (state is null) return new(null, null, null);
            Geoposition? sw = Valid(state.SuedWest) ? state.SuedWest : null;
            Geoposition? ne = Valid(state.NordOst) ? state.NordOst : null;
            if (sw is null || ne is null || sw.Breite >= ne.Breite || sw.Laenge >= ne.Laenge) { sw = null; ne = null; }
            string? folder = ValidFolder(state.Ordner) ? Path.GetFullPath(state.Ordner!) : null;
            return new(sw, ne, folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        { return new(null, null, null); }
    }

    public static void Speichern(Planungseinstellungen settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? temp = null;
        try
        {
            string? path = Pfad;
            if (path is null) return;
            Geoposition? sw = Valid(settings.SuedWest) ? settings.SuedWest : null;
            Geoposition? ne = Valid(settings.NordOst) ? settings.NordOst : null;
            if (sw is null || ne is null || sw.Breite >= ne.Breite || sw.Laenge >= ne.Laenge) { sw = null; ne = null; }
            string? folder = ValidFolder(settings.Ordner) ? Path.GetFullPath(settings.Ordner!) : null;
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(new ApiPlanungsAnsicht(sw, ne, folder), JsonOptions);
            if (data.Length > MaxBytes) return;
            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            temp = Path.Combine(directory, ".api-planung-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { output.Write(data); output.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or System.Security.SecurityException) { }
        finally { if (temp is not null) try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    private static bool Valid(Geoposition? p) => p is not null && double.IsFinite(p.Breite) && p.Breite is >= -90 and <= 90 && double.IsFinite(p.Laenge) && p.Laenge is >= -180 and <= 180;
    private static bool ValidFolder(string? folder) => folder is not null && Path.IsPathFullyQualified(folder) && Directory.Exists(folder);
}
