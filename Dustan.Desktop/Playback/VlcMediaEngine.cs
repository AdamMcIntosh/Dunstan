using Dustan.Services;
using LibVLCSharp.Shared;

namespace Dustan.Playback;

public sealed class VlcMediaEngine : IMediaEngine
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private Media? _media;

    static VlcMediaEngine()
    {
        Core.Initialize();
    }

    public VlcMediaEngine()
    {
        _libVlc = new LibVLC("--no-video", "--quiet");
        _player = new MediaPlayer(_libVlc);
        _player.Volume = 80;
        _player.EndReached += (_, _) => Queue(Ended);
        _player.EncounteredError += (_, _) => Queue(Failed);
        _player.Playing += (_, _) => Queue(StateChanged);
        _player.Paused += (_, _) => Queue(StateChanged);
        _player.Stopped += (_, _) => Queue(StateChanged);
    }

    public event EventHandler? StateChanged;
    public event EventHandler? Ended;
    public event EventHandler? Failed;

    public double Volume
    {
        get => Math.Clamp(_player.Volume / 100.0, 0, 1);
        set => _player.Volume = (int)Math.Round(Math.Clamp(value, 0, 1) * 100);
    }

    public bool IsPlaying => _player.IsPlaying;

    public TimeSpan Position
    {
        get
        {
            var ms = _player.Time;
            return ms < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ms);
        }
        set => _player.Time = (long)Math.Max(0, value.TotalMilliseconds);
    }

    public TimeSpan Duration
    {
        get
        {
            var ms = _player.Length;
            return ms <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ms);
        }
    }

    public Task PlayFileAsync(string path)
    {
        StopAndRelease();
        var media = CreateMedia(path);
        _media = media;
        if (!_player.Play(media))
        {
            throw new InvalidOperationException("Unable to start playback.");
        }

        return Task.CompletedTask;
    }

    public void Play() => _player.Play();

    public void Pause() => _player.Pause();

    public void StopAndRelease()
    {
        _player.Stop();
        var old = _media;
        _media = null;
        _player.Media = null;
        old?.Dispose();
    }

    public void Dispose()
    {
        StopAndRelease();
        _player.Dispose();
        _libVlc.Dispose();
    }

    private Media CreateMedia(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var uri = new Uri("file:" + path.Replace('\\', '/'));
            return new Media(_libVlc, uri);
        }

        return new Media(_libVlc, path, FromType.FromPath);
    }

    private static void Queue(EventHandler? handler)
    {
        // LibVLC callbacks must not re-enter native code on the same thread.
        ThreadPool.QueueUserWorkItem(_ => handler?.Invoke(null, EventArgs.Empty));
    }
}
