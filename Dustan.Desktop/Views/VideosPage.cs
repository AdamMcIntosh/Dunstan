using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Dustan.Helpers;
using Dustan.Models;
using Dustan.Playback;

namespace Dustan.Views;

public sealed class VideosPage : UserControl, ISearchablePage
{
    private readonly TextBox _path = new() { Watermark = "Mounted video folder, e.g. /mnt/videos", MinWidth = 260 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly WrapPanel _cards = new();
    private readonly ComboBox _filter = new() { ItemsSource = new[] { "All videos", "Continue watching", "Unwatched", "Watched" }, SelectedIndex = 0, Width = 180 };
    private readonly Button _scan = new() { Content = "Save & scan" };
    private readonly Button _cancel = new() { Content = "Cancel", IsEnabled = false };
    private readonly TextBlock _pageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly List<Bitmap> _images = [];
    private CancellationTokenSource? _scanCancellation;
    private string? _search;
    private int _page;
    private int _pageCount = 1;
    private bool _attached;

    public VideosPage(string? search = null)
    {
        _search = search;
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 10 };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock { Text = "Videos", FontSize = 28, FontWeight = FontWeight.SemiBold });
        header.Children.Add(new TextBlock { Text = "Your films and shows, streamed from your NAS. Choose a video folder to get started.", TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        var folder = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(8, 0, 0, 0) };
        var browse = new Button { Content = "Browse…" };
        browse.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose video library", AllowMultiple = false });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) _path.Text = path;
        };
        actions.Children.Add(browse); actions.Children.Add(_scan); actions.Children.Add(_cancel);
        DockPanel.SetDock(actions, Dock.Right); folder.Children.Add(actions); folder.Children.Add(_path);
        header.Children.Add(folder);
        header.Children.Add(_status);
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 12) };
        filters.Children.Add(_filter);
        var previous = new Button { Content = "Previous" };
        var next = new Button { Content = "Next" };
        previous.Click += (_, _) => { _page = Math.Max(0, _page - 1); Reload(); };
        next.Click += (_, _) => { _page = Math.Min(_pageCount - 1, _page + 1); Reload(); };
        filters.Children.Add(previous); filters.Children.Add(next); filters.Children.Add(_pageLabel);
        header.Children.Add(filters);
        root.Children.Add(header); root.Children.Add(new ScrollViewer { Content = _cards }); Content = root;
        _path.Text = AppServices.Instance.Videos.Root;
        _filter.SelectionChanged += (_, _) => { _page = 0; Reload(); };
        _scan.Click += async (_, _) => await ScanAsync();
        _cancel.Click += (_, _) => _scanCancellation?.Cancel();
        AttachedToVisualTree += (_, _) => { _attached = true; Reload(); };
        DetachedFromVisualTree += (_, _) => { _attached = false; _scanCancellation?.Cancel(); ClearCards(); };
    }

    public void ApplySearch(string? query) { _search = query; _page = 0; Reload(); }

    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(_path.Text)) { _status.Text = "Choose a mounted video folder first."; return; }
        _scan.IsEnabled = false; _cancel.IsEnabled = true;
        using var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        try
        {
            using var probe = new VideoProbe();
            var count = await AppServices.Instance.Videos.ScanAsync(_path.Text, probe.GetDurationAsync,
                new Progress<string>(message => _status.Text = "Scanning · " + message), cancellation.Token);
            _page = 0; if (_attached) Reload();
            var unknown = AppServices.Instance.Videos.Videos.Count(v => v.DurationSeconds < 1);
            _status.Text = $"{count} videos indexed." + (unknown > 0 ? $" {unknown} have unknown durations and cannot be scheduled on TV yet." : "");
        }
        catch (OperationCanceledException) { _status.Text = "Scan cancelled. Previous library retained."; }
        catch (Exception ex) { _status.Text = "Scan failed: " + ex.Message; }
        finally { _scanCancellation = null; _scan.IsEnabled = true; _cancel.IsEnabled = false; }
    }

    private void ClearCards()
    {
        _cards.Children.Clear();
        foreach (var image in _images) image.Dispose();
        _images.Clear();
    }

    private void Reload()
    {
        ClearCards();
        var library = AppServices.Instance.Videos;
        var videos = library.Videos.Where(v => string.IsNullOrWhiteSpace(_search) || (v.Title + " " + v.Folder).Contains(_search, StringComparison.OrdinalIgnoreCase));
        videos = _filter.SelectedIndex switch
        {
            1 => videos.Where(v => v.PositionSeconds > 0 && !v.Watched),
            2 => videos.Where(v => !v.Watched),
            3 => videos.Where(v => v.Watched),
            _ => videos
        };
        var all = videos.ToList();
        _pageCount = Math.Max(1, (all.Count + 59) / 60); _page = Math.Min(_page, _pageCount - 1);
        _pageLabel.Text = $"{_page + 1} / {_pageCount} · {all.Count} videos";
        if (_scanCancellation is null) _status.Text = library.LoadError ?? (all.Count == 0 ? "No videos here yet. Scan a video folder or change your search." : $"{library.Videos.Count} videos · Local posters: video-name.jpg, poster.jpg or folder.jpg");
        foreach (var video in all.Skip(_page * 60).Take(60))
        {
            var content = new StackPanel { Width = 180, Spacing = 6, Margin = new Thickness(8) };
            var poster = new Border { Height = 235, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = new SolidColorBrush(Color.Parse("#263449")) };
            poster.Child = new TextBlock { Text = "▶\n" + video.Title, FontSize = 22, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12) };
            if (video.PosterPath is { } imagePath)
            {
                try
                {
                    using var stream = File.OpenRead(imagePath);
                    var bitmap = Bitmap.DecodeToWidth(stream, 360); _images.Add(bitmap);
                    poster.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
                }
                catch { /* A missing or corrupt poster must not prevent playback. */ }
            }
            content.Children.Add(poster);
            content.Children.Add(new TextBlock { Text = video.Title, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = video.Folder, Opacity = 0.6, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = video.DurationLabel + " · " + video.ProgressLabel, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var play = new Button { Content = video.PositionSeconds > 0 && !video.Watched ? "Resume" : "Play", HorizontalAlignment = HorizontalAlignment.Stretch };
            play.Click += async (_, _) => await PlayAsync(video, !video.Watched);
            content.Children.Add(play);
            if (video.PositionSeconds > 0)
            {
                var restart = new Button { Content = "Play from beginning", HorizontalAlignment = HorizontalAlignment.Stretch };
                restart.Click += async (_, _) => await PlayAsync(video, false); content.Children.Add(restart);
            }
            ToolTip.SetTip(content, video.Path);
            _cards.Children.Add(content);
        }
    }

    private async Task PlayAsync(VideoInfo video, bool resume)
    {
        if (TopLevel.GetTopLevel(this) is not Window host) return;
        try { await new VideoPlayerWindow(video, resume).ShowDialog(host); if (_attached) Reload(); }
        catch (Exception ex) { _status.Text = "Unable to open player: " + ex.Message; }
    }
}
