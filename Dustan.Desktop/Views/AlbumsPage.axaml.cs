using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dustan.Helpers;
using Dustan.Services;

namespace Dustan.Views;

public partial class AlbumsPage : UserControl, ISearchablePage
{
    private string? _search;

    public AlbumsPage() : this(null)
    {
    }

    public AlbumsPage(string? search)
    {
        InitializeComponent();
        _search = search;
        AttachedToVisualTree += (_, _) =>
        {
            LibraryEvents.Changed -= OnLibraryChanged;
            LibraryEvents.Changed += OnLibraryChanged;
            Reload();
        };
        DetachedFromVisualTree += (_, _) => LibraryEvents.Changed -= OnLibraryChanged;
        AlbumGrid.Tapped += AlbumGrid_Tapped;
    }

    public void ApplySearch(string? query)
    {
        _search = query;
        Reload();
    }

    private void OnLibraryChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(Reload);

    private void Reload()
    {
        var albums = AppServices.Instance.Library.GetAlbums(_search);
        AlbumGrid.ItemsSource = albums.Select(a => new AlbumCard
        {
            Album = a,
            Artwork = PlaybackHelpers.CreateArtworkImage(a.ArtworkPath)
        }).ToList();
    }

    private void AlbumGrid_Tapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void AlbumGrid_DoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
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

        var result = await MetadataUi.EditAlbumAsync(host, card.Album);
        Reload();
        await MetadataUi.ShowFailuresIfNeeded(host, result);
    }
}
