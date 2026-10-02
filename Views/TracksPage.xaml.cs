using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Dustan.Views;

public sealed partial class TracksPage : Page
{
    private IReadOnlyList<TrackInfo> _tracks = [];
    private string? _search;

    public TracksPage()
    {
        InitializeComponent();
        TrackListInteractions.Attach(TrackList, () => _tracks);
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
        _tracks = AppServices.Instance.Library.GetTracks(_search);
        TrackList.ItemsSource = _tracks;
    }

    private async void EditTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TrackInfo track })
        {
            return;
        }

        var result = await MetadataUi.EditTrackAsync(this, track);
        Reload();
        await MetadataUi.ShowFailuresIfNeeded(this, result);
    }
}
