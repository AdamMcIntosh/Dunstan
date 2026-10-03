using Avalonia.Controls;
using Dustan.Models;
using Dustan.Views;

namespace Dustan.Helpers;

public static class MetadataUi
{
    public static async Task<TagWriteResult?> EditTrackAsync(Window host, TrackInfo track)
        => await ShowAsync(host, TagEditScope.Track, [track]);

    public static async Task<TagWriteResult?> EditAlbumAsync(Window host, AlbumInfo album)
    {
        var tracks = AppServices.Instance.Library.GetTracksForAlbum(album.Id);
        if (tracks.Count == 0)
        {
            return null;
        }

        return await ShowAsync(host, TagEditScope.Album, tracks);
    }

    private static async Task<TagWriteResult?> ShowAsync(
        Window host,
        TagEditScope scope,
        IReadOnlyList<TrackInfo> tracks)
    {
        var dialog = new EditMetadataDialog(scope, tracks);
        await dialog.ShowDialog(host);
        return dialog.Result;
    }

    public static async Task ShowFailuresIfNeeded(Window host, TagWriteResult? result)
    {
        if (result is not { HasFailures: true })
        {
            return;
        }

        var window = new Window
        {
            Title = result.Succeeded == 0 ? "Could not save tags" : "Some files failed",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = string.Join(Environment.NewLine, result.Errors.Take(8)), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                }
            }
        };

        if (window.Content is StackPanel panel && panel.Children[1] is Button ok)
        {
            ok.Click += (_, _) => window.Close();
        }

        await window.ShowDialog(host);
    }
}
