using Dustan.Models;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace Dustan.Services;

public sealed class PlaybackService : IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly object _queueLock = new();
    private readonly List<TrackInfo> _queue = [];
    private readonly List<int> _order = [];
    private int _currentIndex = -1;
    private bool _shuffle;
    private RepeatMode _repeat = RepeatMode.Off;
    private string _libraryRoot = "";
    private bool _suppressEnded;

    public event EventHandler? StateChanged;
    public event EventHandler<TrackInfo?>? CurrentTrackChanged;
    public event EventHandler<TrackInfo>? TrackFailed;

    public MediaPlayer Player => _player;
    public bool IsShuffleEnabled => _shuffle;
    public RepeatMode Repeat => _repeat;
    public TrackInfo? CurrentTrack { get; private set; }
    public IReadOnlyList<TrackInfo> Queue
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.ToList();
            }
        }
    }

    public PlaybackService()
    {
        _player.Volume = 0.8;
        _player.MediaEnded += PlayerOnMediaEnded;
        _player.MediaFailed += PlayerOnMediaFailed;
        _player.CurrentStateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLibraryRoot(string root) => _libraryRoot = root ?? "";

    public void SetVolume(double volume)
    {
        _player.Volume = Math.Clamp(volume, 0, 1);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public double Volume => _player.Volume;

    public void ToggleShuffle()
    {
        lock (_queueLock)
        {
            _shuffle = !_shuffle;
            RebuildOrder(keepCurrent: true);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CycleRepeat()
    {
        _repeat = _repeat switch
        {
            RepeatMode.Off => RepeatMode.Album,
            RepeatMode.Album => RepeatMode.One,
            _ => RepeatMode.Off
        };
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PlayList(IReadOnlyList<TrackInfo> tracks, int startIndex = 0)
    {
        if (tracks.Count == 0)
        {
            return;
        }

        startIndex = Math.Clamp(startIndex, 0, tracks.Count - 1);
        lock (_queueLock)
        {
            _queue.Clear();
            _queue.AddRange(tracks);
            RebuildOrder(keepCurrent: false);
            _currentIndex = _shuffle ? _order.IndexOf(startIndex) : startIndex;
            if (_currentIndex < 0)
            {
                _currentIndex = 0;
            }
        }

        _ = PlayCurrentAsync();
    }

    public void Play()
    {
        if (CurrentTrack is null)
        {
            return;
        }

        _player.Play();
    }

    public void Pause() => _player.Pause();

    public void ReleaseIfPlaying(IEnumerable<string> paths)
    {
        var current = CurrentTrack?.Path;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        if (!paths.Any(p => string.Equals(p, current, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _suppressEnded = true;
        try
        {
            _player.Pause();
            _player.Source = null;
        }
        finally
        {
            _suppressEnded = false;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void TogglePlayPause()
    {
        if (_player.CurrentState == MediaPlayerState.Playing)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void Next()
    {
        lock (_queueLock)
        {
            if (_queue.Count == 0)
            {
                return;
            }

            if (_repeat == RepeatMode.One)
            {
                // Next still advances when explicitly requested.
            }

            if (_currentIndex + 1 >= _order.Count)
            {
                if (_repeat == RepeatMode.Album)
                {
                    _currentIndex = 0;
                }
                else
                {
                    return;
                }
            }
            else
            {
                _currentIndex++;
            }
        }

        _ = PlayCurrentAsync();
    }

    public void Previous()
    {
        var position = _player.PlaybackSession.Position;
        if (position > TimeSpan.FromSeconds(3))
        {
            _player.PlaybackSession.Position = TimeSpan.Zero;
            return;
        }

        lock (_queueLock)
        {
            if (_queue.Count == 0)
            {
                return;
            }

            if (_currentIndex <= 0)
            {
                if (_repeat == RepeatMode.Album)
                {
                    _currentIndex = _order.Count - 1;
                }
                else
                {
                    _player.PlaybackSession.Position = TimeSpan.Zero;
                    return;
                }
            }
            else
            {
                _currentIndex--;
            }
        }

        _ = PlayCurrentAsync();
    }

    public void Seek(TimeSpan position)
    {
        var duration = _player.PlaybackSession.NaturalDuration;
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        if (position > duration)
        {
            position = duration;
        }

        _player.PlaybackSession.Position = position;
    }

    private void RebuildOrder(bool keepCurrent)
    {
        TrackInfo? current = null;
        if (keepCurrent && _currentIndex >= 0 && _currentIndex < _order.Count)
        {
            var qi = _order[_currentIndex];
            if (qi >= 0 && qi < _queue.Count)
            {
                current = _queue[qi];
            }
        }

        _order.Clear();
        _order.AddRange(Enumerable.Range(0, _queue.Count));
        if (_shuffle && _order.Count > 1)
        {
            var rng = Random.Shared;
            for (var i = _order.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
        }

        if (current is not null)
        {
            var queueIndex = _queue.FindIndex(t => t.Id == current.Id && t.Path == current.Path);
            _currentIndex = queueIndex >= 0 ? _order.IndexOf(queueIndex) : 0;
            if (_currentIndex < 0)
            {
                _currentIndex = 0;
            }
        }
        else if (_order.Count > 0 && _currentIndex < 0)
        {
            _currentIndex = 0;
        }
    }

    private async Task PlayCurrentAsync()
    {
        TrackInfo? track;
        lock (_queueLock)
        {
            if (_currentIndex < 0 || _currentIndex >= _order.Count)
            {
                CurrentTrack = null;
                CurrentTrackChanged?.Invoke(this, null);
                return;
            }

            track = _queue[_order[_currentIndex]];
        }

        if (!PathSafety.IsUnderLibraryRoot(track.Path, _libraryRoot))
        {
            track.OpenFailed = true;
            TrackFailed?.Invoke(this, track);
            Next();
            return;
        }

        CurrentTrack = track;
        CurrentTrackChanged?.Invoke(this, track);

        try
        {
            _suppressEnded = true;
            _player.Pause();
            StorageFile file;
            try
            {
                file = await StorageFile.GetFileFromPathAsync(track.Path);
            }
            catch
            {
                // UNC / network fallback via URI
                var uri = new Uri(track.Path.StartsWith(@"\\", StringComparison.Ordinal)
                    ? "file:" + track.Path.Replace('\\', '/')
                    : track.Path);
                _player.Source = MediaSource.CreateFromUri(uri);
                _suppressEnded = false;
                _player.Play();
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            _player.Source = MediaSource.CreateFromStorageFile(file);
            _suppressEnded = false;
            _player.Play();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _suppressEnded = false;
            track.OpenFailed = true;
            TrackFailed?.Invoke(this, track);
            NextSkippingFailed(track.Id);
        }
    }

    private void NextSkippingFailed(long failedId)
    {
        lock (_queueLock)
        {
            if (_queue.Count == 0)
            {
                return;
            }

            var attempts = 0;
            while (attempts < _queue.Count)
            {
                if (_currentIndex + 1 >= _order.Count)
                {
                    if (_repeat == RepeatMode.Album)
                    {
                        _currentIndex = 0;
                    }
                    else
                    {
                        CurrentTrack = null;
                        CurrentTrackChanged?.Invoke(this, null);
                        return;
                    }
                }
                else
                {
                    _currentIndex++;
                }

                var next = _queue[_order[_currentIndex]];
                if (next.Id != failedId && !next.OpenFailed)
                {
                    break;
                }

                attempts++;
            }

            if (attempts >= _queue.Count)
            {
                CurrentTrack = null;
                CurrentTrackChanged?.Invoke(this, null);
                return;
            }
        }

        _ = PlayCurrentAsync();
    }

    private void PlayerOnMediaEnded(MediaPlayer sender, object args)
    {
        if (_suppressEnded)
        {
            return;
        }

        if (_repeat == RepeatMode.One)
        {
            _player.PlaybackSession.Position = TimeSpan.Zero;
            _player.Play();
            return;
        }

        lock (_queueLock)
        {
            if (_currentIndex + 1 >= _order.Count)
            {
                if (_repeat == RepeatMode.Album && _order.Count > 0)
                {
                    _currentIndex = 0;
                }
                else
                {
                    return;
                }
            }
            else
            {
                _currentIndex++;
            }
        }

        _ = PlayCurrentAsync();
    }

    private void PlayerOnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        var track = CurrentTrack;
        if (track is not null)
        {
            track.OpenFailed = true;
            TrackFailed?.Invoke(this, track);
            NextSkippingFailed(track.Id);
        }
    }

    public void Dispose()
    {
        _player.MediaEnded -= PlayerOnMediaEnded;
        _player.MediaFailed -= PlayerOnMediaFailed;
        _player.Dispose();
    }
}
