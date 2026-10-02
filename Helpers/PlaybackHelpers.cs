using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Dustan.Helpers;

public static class PlaybackHelpers
{
    public static void PlayTracks(IReadOnlyList<TrackInfo> tracks, TrackInfo? start = null)
    {
        if (tracks.Count == 0)
        {
            return;
        }

        var index = 0;
        if (start is not null)
        {
            var found = -1;
            for (var i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Id == start.Id)
                {
                    found = i;
                    break;
                }
            }

            if (found >= 0)
            {
                index = found;
            }
        }

        AppServices.Instance.Playback.PlayList(tracks, index);
    }

    public static BitmapImage? CreateArtworkImage(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                var image = new BitmapImage
                {
                    CreateOptions = BitmapCreateOptions.IgnoreImageCache,
                    UriSource = new Uri(path)
                };
                return image;
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            return new BitmapImage(new Uri("ms-appx:///Assets/PlaceholderAlbum.png"));
        }
        catch
        {
            return null;
        }
    }
}

public sealed class AlbumCard
{
    public AlbumInfo Album { get; init; } = new();
    public BitmapImage? Artwork { get; init; }
}

public static class TrackListInteractions
{
    public static void Attach(ListView listView, Func<IReadOnlyList<TrackInfo>> getTracks)
    {
        listView.DoubleTapped += (_, _) =>
        {
            if (listView.SelectedItem is TrackInfo track)
            {
                PlaybackHelpers.PlayTracks(getTracks(), track);
            }
        };

        listView.KeyDown += (_, e) =>
        {
            if (e.Key is Windows.System.VirtualKey.Enter && listView.SelectedItem is TrackInfo track)
            {
                PlaybackHelpers.PlayTracks(getTracks(), track);
                e.Handled = true;
            }
        };
    }
}
