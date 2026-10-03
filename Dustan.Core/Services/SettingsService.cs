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
    public static string DataDirectory { get; }

    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string DatabasePath => Path.Combine(DataDirectory, "library.db");
    public static string ArtworkDirectory => Path.Combine(DataDirectory, "artwork");

    static AppPaths()
    {
        var data = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dustan");
        Directory.CreateDirectory(data);
        TryMigrateLegacySidecar(AppContext.BaseDirectory, data);
        DataDirectory = data;
    }

    private static void TryMigrateLegacySidecar(string oldDirectory, string newDirectory)
    {
        if (string.IsNullOrWhiteSpace(oldDirectory)
            || string.Equals(
                Path.GetFullPath(oldDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                Path.GetFullPath(newDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var name in new[] { "settings.json", "library.db", "library.db-wal", "library.db-shm" })
        {
            CopyIfMissing(Path.Combine(oldDirectory, name), Path.Combine(newDirectory, name));
        }

        var oldArt = Path.Combine(oldDirectory, "artwork");
        var newArt = Path.Combine(newDirectory, "artwork");
        if (!Directory.Exists(oldArt) || Directory.Exists(newArt))
        {
            return;
        }

        try
        {
            CopyDirectory(oldArt, newArt);
        }
        catch
        {
            // keep going with an empty cache
        }
    }

    private static void CopyIfMissing(string source, string dest)
    {
        try
        {
            if (File.Exists(source) && !File.Exists(dest))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(source, dest);
            }
        }
        catch
        {
            // ignore migration failures
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: false);
        }
    }
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
        var normalized = PathNormalizer.Canonicalize(path);
        Save(new AppSettings { LibraryPath = normalized });
    }
}

public static class PathNormalizer
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);

    public static string Canonicalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        path = path.Trim().TrimEnd('\\', '/');

        if (OperatingSystem.IsWindows())
        {
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
    public static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static StringComparer Comparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool IsUnderLibraryRoot(string candidatePath, string libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(libraryRoot))
        {
            return false;
        }

        try
        {
            var fullFile = NormalizeForCompare(candidatePath);
            var fullRoot = NormalizeForCompare(libraryRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rootWithSep = fullRoot + Path.DirectorySeparatorChar;
            return fullFile.StartsWith(rootWithSep, Comparison)
                   || string.Equals(
                       fullFile.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                       fullRoot,
                       Comparison);
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizeForCompare(string path)
    {
        return Path.GetFullPath(path);
    }
}
