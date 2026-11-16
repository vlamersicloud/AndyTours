// WPF projects do not implicitly import System.IO.
using System.IO;
namespace AndyTours.App.Funktionen;

/// <summary>Persists only the user's local GPX export folder, independently of backend settings.</summary>
public static class ApiGpxOrdner
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AndyTours", "api-gpx-export.txt");

    public static string? Laden()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var value = File.ReadAllText(SettingsPath).Trim();
            return Path.IsPathFullyQualified(value) && Directory.Exists(value) ? value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public static void Speichern(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return;
        string? temporary = null;
        try
        {
            var fullPath = Path.GetFullPath(path);
            var target = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, fullPath);
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        finally
        {
            if (temporary is not null)
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
        }
    }
}