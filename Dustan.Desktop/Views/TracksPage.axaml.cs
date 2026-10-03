using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Views;

public partial class TracksPage : UserControl, ISearchablePage
{
    private IReadOnlyList<TrackInfo> _tracks = [];
    private string? _search;

    public TracksPage() : this(null)
    {
    }

    public TracksPage(string? search)
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
        _tracks = AppServices.Instance.Library.GetTracks(_search);
        TrackList.ItemsSource = _tracks;
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

    private async void EditTrack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var track = (sender as MenuItem)?.Tag as TrackInfo ?? TrackList.SelectedItem as TrackInfo;
        if (track is null || TopLevel.GetTopLevel(this) is not Window host)
        {
            return;
        }

        var result = await MetadataUi.EditTrackAsync(host, track);
        Reload();
        await MetadataUi.ShowFailuresIfNeeded(host, result);
    }
}
