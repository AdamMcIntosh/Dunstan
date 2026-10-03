using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Views;

public partial class ArtistsPage : UserControl, ISearchablePage
{
    private string? _search;

    public ArtistsPage() : this(null)
    {
    }

    public ArtistsPage(string? search)
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
        ArtistList.Tapped += (_, _) => OpenSelected();
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
        ArtistList.ItemsSource = AppServices.Instance.Library.GetArtists(_search);
    }

    private void ArtistList_DoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (ArtistList.SelectedItem is ArtistInfo artist && TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.NavigatePage(new ArtistDetailPage(artist.Id), addToBackStack: true);
        }
    }
}
