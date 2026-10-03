using Dustan.Models;

namespace Dustan.Services;

public interface IPlaybackService
{
    event EventHandler? StateChanged;
    event EventHandler<TrackInfo?>? CurrentTrackChanged;
    event EventHandler<TrackInfo>? TrackFailed;

    bool IsShuffleEnabled { get; }
    RepeatMode Repeat { get; }
    TrackInfo? CurrentTrack { get; }
    IReadOnlyList<TrackInfo> Queue { get; }
    double Volume { get; }
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    void SetLibraryRoot(string root);
    void SetVolume(double volume);
    void ToggleShuffle();
    void CycleRepeat();
    void PlayList(IReadOnlyList<TrackInfo> tracks, int startIndex = 0);
    void Play();
    void Pause();
    void TogglePlayPause();
    void Next();
    void Previous();
    void Seek(TimeSpan position);
    void ReleaseIfPlaying(IEnumerable<string> paths);
}

public interface IMediaEngine : IDisposable
{
    event EventHandler? StateChanged;
    event EventHandler? Ended;
    event EventHandler? Failed;

    double Volume { get; set; }
    bool IsPlaying { get; }
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; }

    Task PlayFileAsync(string path);
    void Play();
    void Pause();
    void StopAndRelease();
}
