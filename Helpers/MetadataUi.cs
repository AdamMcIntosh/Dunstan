using Dustan.Models;
using Dustan.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dustan.Helpers;

public static class MetadataUi
{
    public static async Task<TagWriteResult?> EditTrackAsync(FrameworkElement host, TrackInfo track)
        => await ShowAsync(host, TagEditScope.Track, [track]);

    public static async Task<TagWriteResult?> EditAlbumAsync(FrameworkElement host, AlbumInfo album)
    {
        var tracks = AppServices.Instance.Library.GetTracksForAlbum(album.Id);
        if (tracks.Count == 0)
        {
            return null;
        }

        return await ShowAsync(host, TagEditScope.Album, tracks);
    }

    private static async Task<TagWriteResult?> ShowAsync(
        FrameworkElement host,
        TagEditScope scope,
        IReadOnlyList<TrackInfo> tracks)
    {
        var dialog = new EditMetadataDialog(scope, tracks)
        {
            XamlRoot = host.XamlRoot
        };
        await dialog.ShowAsync();
        return dialog.Result;
    }

    public static async Task ShowFailuresIfNeeded(FrameworkElement host, TagWriteResult? result)
    {
        if (result is not { HasFailures: true })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = host.XamlRoot,
            Title = result.Succeeded == 0 ? "Could not save tags" : "Some files failed",
            Content = string.Join(Environment.NewLine, result.Errors.Take(8)),
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
