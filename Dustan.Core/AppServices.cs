using Dustan.Services;

namespace Dustan;

public sealed class AppServices
{
    public static AppServices Instance { get; private set; } = null!;

    public static AppServices Initialize(IMediaEngine mediaEngine)
    {
        Instance = new AppServices(mediaEngine);
        return Instance;
    }

    public SettingsService Settings { get; } = new();
    public LibraryDb Library { get; }
    public LibraryScanner Scanner { get; }
    public PlaybackService Playback { get; }
    public TagEditor Tags { get; }

    private AppServices(IMediaEngine mediaEngine)
    {
        Library = new LibraryDb(AppPaths.DatabasePath);
        Library.Initialize();
        Scanner = new LibraryScanner(Library);
        Settings.Load();
        Playback = new PlaybackService(mediaEngine);
        Playback.SetLibraryRoot(Settings.Current.LibraryPath);
        Tags = new TagEditor(Library, Playback, () => Settings.Current.LibraryPath);
    }
}
