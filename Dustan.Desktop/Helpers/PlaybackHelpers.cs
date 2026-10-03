using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dustan.Models;

namespace Dustan.Helpers;

public sealed class AlbumCard
{
    public AlbumInfo Album { get; init; } = new();
    public Bitmap? Artwork { get; init; }
}

public static class PlaybackHelpers
{
    private static Bitmap? _placeholder;

    public static void PlayTracks(IReadOnlyList<TrackInfo> tracks, TrackInfo? start = null)
    {
        if (tracks.Count == 0)
        {
            return;
        }

        var index = 0;
        if (start is not null)
        {
            for (var i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Id == start.Id)
                {
                    index = i;
                    break;
                }
            }
        }

        AppServices.Instance.Playback.PlayList(tracks, index);
    }

    public static Bitmap CreateArtworkImage(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return new Bitmap(path);
            }
        }
        catch
        {
            // fall through
        }

        return Placeholder();
    }

    public static Bitmap Placeholder()
    {
        if (_placeholder is not null)
        {
            return _placeholder;
        }

        using var stream = AssetLoader.Open(new Uri("avares://Dustan/Assets/PlaceholderAlbum.png"));
        _placeholder = new Bitmap(stream);
        return _placeholder;
    }
}

public interface ISearchablePage
{
    void ApplySearch(string? query);
}
