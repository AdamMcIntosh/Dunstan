using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Dustan.Views;

public sealed partial class ArtistDetailPage : Page
{
    private IReadOnlyList<TrackInfo> _tracks = [];
    private long _artistId;

    public ArtistDetailPage()
    {
        InitializeComponent();
        TrackListInteractions.Attach(TrackList, () => _tracks);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        LibraryEvents.Changed -= OnLibraryChanged;
        LibraryEvents.Changed += OnLibraryChanged;

        if (e.Parameter is not long artistId)
        {
            return;
        }

        Reload(artistId);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        LibraryEvents.Changed -= OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
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

    private void PlayAll_Click(object sender, RoutedEventArgs e) =>
        PlaybackHelpers.PlayTracks(_tracks);

    private void AlbumGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AlbumCard card)
        {
            Frame.Navigate(typeof(AlbumDetailPage), card.Album.Id);
        }
    }

    private async void EditAlbum_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AlbumCard card })
        {
            return;
        }

        var previousArtistId = _artistId;
        var result = await MetadataUi.EditAlbumAsync(this, card.Album);

        if (result is { Succeeded: > 0, AlbumId: long newAlbumId })
        {
            var updated = AppServices.Instance.Library.GetAlbum(newAlbumId);
            var destination = updated is null
                ? null
                : AppServices.Instance.Library.GetArtistByName(updated.AlbumArtist);

            if (destination is not null && destination.Id != previousArtistId)
            {
                Frame.Navigate(typeof(ArtistDetailPage), destination.Id);
                await MetadataUi.ShowFailuresIfNeeded(this, result);
                return;
            }
        }

        if (_artistId != 0)
        {
            Reload(_artistId);
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
        Reload(_artistId);
        await MetadataUi.ShowFailuresIfNeeded(this, result);
    }
}
