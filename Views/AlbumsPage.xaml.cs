using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Dustan.Views;

public sealed partial class AlbumsPage : Page
{
    private string? _search;

    public AlbumsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        LibraryEvents.Changed -= OnLibraryChanged;
        LibraryEvents.Changed += OnLibraryChanged;
        _search = e.Parameter as string;
        Reload();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        LibraryEvents.Changed -= OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(Reload);

    public void ApplySearch(string? query)
    {
        _search = query;
        Reload();
    }

    private void Reload()
    {
        var albums = AppServices.Instance.Library.GetAlbums(_search);
        AlbumGrid.ItemsSource = albums.Select(a => new AlbumCard
        {
            Album = a,
            Artwork = PlaybackHelpers.CreateArtworkImage(a.ArtworkPath)
        }).ToList();
    }

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

        var result = await MetadataUi.EditAlbumAsync(this, card.Album);
        Reload();
        await MetadataUi.ShowFailuresIfNeeded(this, result);
    }
}
