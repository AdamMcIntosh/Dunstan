using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Views;

public partial class ArtistDetailPage : UserControl
{
    private IReadOnlyList<TrackInfo> _tracks = [];
    private long _artistId;

    public ArtistDetailPage() : this(0)
    {
    }

    public ArtistDetailPage(long artistId)
    {
        InitializeComponent();
        _artistId = artistId;
        AttachedToVisualTree += (_, _) =>
        {
            LibraryEvents.Changed -= OnLibraryChanged;
            LibraryEvents.Changed += OnLibraryChanged;
            if (_artistId != 0)
            {
                Reload(_artistId);
            }
        };
        DetachedFromVisualTree += (_, _) => LibraryEvents.Changed -= OnLibraryChanged;
        AlbumGrid.Tapped += (_, _) => OpenAlbum();
    }

    private void OnLibraryChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_artistId != 0)
            {
                Reload(_artistId);
            }
        });

    private void Reload(long artistId)
    {
        _artistId = artistId;
        var artist = AppServices.Instance.Library.GetArtist(artistId);
        if (artist is null)
        {
            return;
        }

        ArtistTitle.Text = artist.Name;
        ArtistMeta.Text = $"{artist.AlbumCount} albums · {artist.TrackCount} tracks";

        var albums = AppServices.Instance.Library.GetAlbumsForArtist(artistId);
        AlbumGrid.ItemsSource = albums.Select(a => new AlbumCard
        {
            Album = a,
            Artwork = PlaybackHelpers.CreateArtworkImage(a.ArtworkPath)
        }).ToList();

        _tracks = AppServices.Instance.Library.GetTracksForArtist(artistId);
        TrackList.ItemsSource = _tracks;
    }

    private void PlayAll_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        PlaybackHelpers.PlayTracks(_tracks);

    private void AlbumGrid_DoubleTapped(object? sender, TappedEventArgs e) => OpenAlbum();

    private void OpenAlbum()
    {
        if (AlbumGrid.SelectedItem is AlbumCard card && TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.NavigatePage(new AlbumDetailPage(card.Album.Id), addToBackStack: true);
        }
    }

    private async void EditAlbum_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var card = (sender as MenuItem)?.Tag as AlbumCard ?? AlbumGrid.SelectedItem as AlbumCard;
        if (card is null || TopLevel.GetTopLevel(this) is not Window host)
        {
            return;
        }

        var previousArtistId = _artistId;
        var result = await MetadataUi.EditAlbumAsync(host, card.Album);

        if (result is { Succeeded: > 0, AlbumId: long newAlbumId } && host is MainWindow window)
        {
            var updated = AppServices.Instance.Library.GetAlbum(newAlbumId);
            var destination = updated is null
                ? null
                : AppServices.Instance.Library.GetArtistByName(updated.AlbumArtist);

            if (destination is not null && destination.Id != previousArtistId)
            {
                window.NavigatePage(new ArtistDetailPage(destination.Id), addToBackStack: true);
                await MetadataUi.ShowFailuresIfNeeded(host, result);
                return;
            }
        }

        if (_artistId != 0)
        {
            Reload(_artistId);
        }

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
        Reload(_artistId);
        await MetadataUi.ShowFailuresIfNeeded(host, result);
    }

    private void TrackList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TrackList.SelectedItem is TrackInfo track)
        {
            PlaybackHelpers.PlayTracks(_tracks, track);
        }
    }
}
