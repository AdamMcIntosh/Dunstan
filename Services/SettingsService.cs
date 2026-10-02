using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Dustan.Services;

public sealed class AppSettings
{
    public string LibraryPath { get; set; } = "";
}

public static class AppPaths
{
    public static string AppDirectory { get; } = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string SettingsPath => Path.Combine(AppDirectory, "settings.json");
    public static string DatabasePath => Path.Combine(AppDirectory, "library.db");
    public static string ArtworkDirectory => Path.Combine(AppDirectory, "artwork");
}

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _lock = new();
    private AppSettings _settings = new();

    public AppSettings Current
    {
        get
        {
            lock (_lock)
            {
                return new AppSettings { LibraryPath = _settings.LibraryPath };
            }
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            if (!File.Exists(AppPaths.SettingsPath))
            {
                _settings = new AppSettings();
                return;
            }

            try
            {
                var json = File.ReadAllText(AppPaths.SettingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                _settings = new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_lock)
        {
            _settings = new AppSettings { LibraryPath = settings.LibraryPath };
            File.WriteAllText(AppPaths.SettingsPath, JsonSerializer.Serialize(_settings, JsonOptions));
        }
    }

    public void SetLibraryPath(string path)
    {
        var normalized = PathNormalizer.PreferUnc(path);
        Save(new AppSettings { LibraryPath = normalized });
    }
}

public static class PathNormalizer
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);

    public static string PreferUnc(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        path = path.Trim().TrimEnd('\\', '/');

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return path;
        }

        if (path.Length >= 2 && path[1] == ':')
        {
            var drive = path[..2];
            var sb = new StringBuilder(512);
            var len = sb.Capacity;
            if (WNetGetConnection(drive, sb, ref len) == 0)
            {
                var uncRoot = sb.ToString().TrimEnd('\\');
                var remainder = path.Length > 2 ? path[2..].TrimStart('\\', '/') : "";
                return string.IsNullOrEmpty(remainder) ? uncRoot : Path.Combine(uncRoot, remainder);
            }
        }

        try
        {
            return Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch
        {
            return path;
        }
    }
}

public static class PathSafety
{
    public static bool IsUnderLibraryRoot(string candidatePath, string libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(libraryRoot))
        {
            return false;
        }

        try
        {
            var fullFile = NormalizeForCompare(candidatePath);
            var fullRoot = NormalizeForCompare(libraryRoot).TrimEnd('\\') + '\\';
            return fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(fullFile.TrimEnd('\\'), fullRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizeForCompare(string path)
    {
        // Preserve UNC; GetFullPath can still canonicalize segments.
        return Path.GetFullPath(path);
    }
}
