using Dustan.Services;

namespace Dustan;

public sealed class AppServices
{
    public static AppServices Instance { get; } = new();

    public SettingsService Settings { get; } = new();
    public LibraryDb Library { get; }
    public LibraryScanner Scanner { get; }
    public PlaybackService Playback { get; } = new();
    public TagEditor Tags { get; }

    private AppServices()
    {
        Library = new LibraryDb(AppPaths.DatabasePath);
        Library.Initialize();
        Scanner = new LibraryScanner(Library);
        Settings.Load();
        Playback.SetLibraryRoot(Settings.Current.LibraryPath);
        Tags = new TagEditor(Library, Playback, () => Settings.Current.LibraryPath);
    }
}
