using LibVLCSharp.Shared;

namespace Dustan.Playback;

public sealed class VideoProbe : IDisposable
{
    private readonly LibVLC _vlc = new("--quiet", "--no-video-title-show");

    public async Task<double> GetDurationAsync(string path, CancellationToken token)
    {
        using var media = new Media(_vlc, path, FromType.FromPath);
        var status = await media.Parse(MediaParseOptions.ParseLocal, 10000, token);
        token.ThrowIfCancellationRequested();
        return status == MediaParsedStatus.Done && media.Duration > 0 ? media.Duration / 1000.0 : 0;
    }

    public void Dispose() => _vlc.Dispose();
}
