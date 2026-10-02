using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Dustan.Views;

public sealed partial class ArtistsPage : Page
{
    private string? _search;

    public ArtistsPage()
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
        ArtistList.ItemsSource = AppServices.Instance.Library.GetArtists(_search);
    }

    private void ArtistList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ArtistInfo artist)
        {
            Frame.Navigate(typeof(ArtistDetailPage), artist.Id);
        }
    }
}
