using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Dustan.Views;

public sealed partial class AlbumDetailPage : Page
{
    private IReadOnlyList<TrackInfo> _tracks = [];
    private AlbumInfo? _album;
    private long _albumId;

    public AlbumDetailPage()
    {
        InitializeComponent();
        TrackListInteractions.Attach(TrackList, () => _tracks);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        LibraryEvents.Changed -= OnLibraryChanged;
        LibraryEvents.Changed += OnLibraryChanged;

        if (e.Parameter is not long albumId)
        {
            return;
        }

        _albumId = albumId;
        Reload(albumId);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        LibraryEvents.Changed -= OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_albumId != 0)
            {
                // Prefer current album id if it still exists; otherwise fall back to first track's album.
                if (AppServices.Instance.Library.GetAlbum(_albumId) is not null)
                {
                    Reload(_albumId);
                    return;
                }

                if (_tracks.FirstOrDefault()?.Path is string path)
                {
                    var refreshed = AppServices.Instance.Library.GetTracks()
                        .FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
                    if (refreshed?.AlbumId is long newId)
                    {
                        _albumId = newId;
                        Reload(newId);
                    }
                }
            }
        });

    private void PlayAlbum_Click(object sender, RoutedEventArgs e) =>
        PlaybackHelpers.PlayTracks(_tracks);

    private async void EditAlbum_Click(object sender, RoutedEventArgs e)
    {
        if (_album is null)
        {
            return;
        }

        var result = await MetadataUi.EditAlbumAsync(this, _album);
        if (result is null || result.Succeeded == 0)
        {
            await MetadataUi.ShowFailuresIfNeeded(this, result);
            return;
        }

        if (result.AlbumId is long newId && newId != _albumId)
        {
            _albumId = newId;
            // Replace this detail page in the stack so Back still returns to the list,
            // but forward state points at the renamed album.
            Frame.Navigate(typeof(AlbumDetailPage), newId);
            if (Frame.BackStack.Count > 0)
            {
                Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
            }
        }
        else
        {
            Reload(result.AlbumId ?? _albumId);
        }

        await MetadataUi.ShowFailuresIfNeeded(this, result);
    }

    private async void EditTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TrackInfo track })
        {
            return;
        }

        var result = await MetadataUi.EditTrackAsync(this, track);
        if (result is null || result.Succeeded == 0)
        {
            await MetadataUi.ShowFailuresIfNeeded(this, result);
            return;
        }

        _albumId = result.AlbumId ?? _albumId;
        Reload(_albumId);
        await MetadataUi.ShowFailuresIfNeeded(this, result);
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
        ArtBrush.ImageSource = PlaybackHelpers.CreateArtworkImage(_album.ArtworkPath);
        _tracks = AppServices.Instance.Library.GetTracksForAlbum(albumId);
        TrackList.ItemsSource = _tracks;
    }
}
