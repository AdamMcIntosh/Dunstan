using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Views;

public partial class AlbumDetailPage : UserControl
{
    private IReadOnlyList<TrackInfo> _tracks = [];
    private AlbumInfo? _album;
    private long _albumId;

    public AlbumDetailPage() : this(0)
    {
    }

    public AlbumDetailPage(long albumId)
    {
        InitializeComponent();
        _albumId = albumId;
        AttachedToVisualTree += (_, _) =>
        {
            LibraryEvents.Changed -= OnLibraryChanged;
            LibraryEvents.Changed += OnLibraryChanged;
            if (_albumId != 0)
            {
                Reload(_albumId);
            }
        };
        DetachedFromVisualTree += (_, _) => LibraryEvents.Changed -= OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_albumId == 0)
            {
                return;
            }

            if (AppServices.Instance.Library.GetAlbum(_albumId) is not null)
            {
                Reload(_albumId);
                return;
            }

            if (_tracks.FirstOrDefault()?.Path is string path)
            {
                var refreshed = AppServices.Instance.Library.GetTracks()
                    .FirstOrDefault(t => string.Equals(t.Path, path, PathSafety.Comparison));
                if (refreshed?.AlbumId is long newId)
                {
                    _albumId = newId;
                    Reload(newId);
                }
            }
        });

    private void PlayAlbum_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        PlaybackHelpers.PlayTracks(_tracks);

    private async void EditAlbum_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_album is null || TopLevel.GetTopLevel(this) is not Window host)
        {
            return;
        }

        var result = await MetadataUi.EditAlbumAsync(host, _album);
        if (result is null || result.Succeeded == 0)
        {
            await MetadataUi.ShowFailuresIfNeeded(host, result);
            return;
        }

        _albumId = result.AlbumId ?? _albumId;
        Reload(_albumId);
        await MetadataUi.ShowFailuresIfNeeded(host, result);
    }

    private async void EditTrack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var track = (sender as MenuItem)?.Tag as TrackInfo ?? TrackList.SelectedItem as TrackInfo;
        if (track is null || TopLevel.GetTopLevel(this) is not Window host)
        {
            return;
        }

        var result = await MetadataUi.EditTrackAsync(host, track);
        if (result is null || result.Succeeded == 0)
        {
            await MetadataUi.ShowFailuresIfNeeded(host, result);
            return;
        }

        _albumId = result.AlbumId ?? _albumId;
        Reload(_albumId);
        await MetadataUi.ShowFailuresIfNeeded(host, result);
    }

    private void TrackList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TrackList.SelectedItem is TrackInfo track)
        {
            PlaybackHelpers.PlayTracks(_tracks, track);
        }
    }

    private void TrackList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && TrackList.SelectedItem is TrackInfo track)
        {
            PlaybackHelpers.PlayTracks(_tracks, track);
            e.Handled = true;
        }
    }

    private void Reload(long albumId)
    {
        _albumId = albumId;
        _album = AppServices.Instance.Library.GetAlbum(albumId);
        if (_album is null)
        {
            return;
        }

        AlbumTitle.Text = _album.Name;
        AlbumArtist.Text = _album.AlbumArtist;
        AlbumMeta.Text = _album.Year > 0
            ? $"{_album.Year} · {_album.TrackCount} tracks"
            : $"{_album.TrackCount} tracks";
        ArtImage.Source = PlaybackHelpers.CreateArtworkImage(_album.ArtworkPath);
        _tracks = AppServices.Instance.Library.GetTracksForAlbum(albumId);
        TrackList.ItemsSource = _tracks;
    }
}
