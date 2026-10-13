using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Views;

public sealed class TvPage : UserControl
{
    private readonly StackPanel _guide = new() { Spacing = 8 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _range = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private int _hoursAhead;
    private const double PixelsPerMinute = 6;

    public TvPage()
    {
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock { Text = "TV guide", FontSize = 28, FontWeight = FontWeight.SemiBold });
        header.Children.Add(new TextBlock { Text = "Your NAS, on the air. Channels loop their lineup continuously; tune in at the current broadcast time.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var create = new Button { Content = "+ Create channel" };
        create.Click += async (_, _) => await CreateChannelAsync(); toolbar.Children.Add(create);
        var previous = new Button { Content = "← Earlier" };
        previous.Click += (_, _) => { _hoursAhead = Math.Max(0, _hoursAhead - 3); Reload(); }; toolbar.Children.Add(previous);
        var now = new Button { Content = "Now" };
        now.Click += (_, _) => { _hoursAhead = 0; Reload(); }; toolbar.Children.Add(now);
        var next = new Button { Content = "Later →" };
        next.Click += (_, _) => { _hoursAhead = Math.Min(165, _hoursAhead + 3); Reload(); }; toolbar.Children.Add(next);
        toolbar.Children.Add(_range); header.Children.Add(toolbar); header.Children.Add(_status);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = _guide, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = root;
        _timer.Tick += (_, _) => Reload();
        AttachedToVisualTree += (_, _) => { Reload(); _timer.Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void Reload()
    {
        _guide.Children.Clear();
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(_hoursAhead);
        var until = from.AddHours(3);
        _range.Text = $"{from.ToLocalTime():ddd, MMM d · HH:mm} – {until.ToLocalTime():HH:mm}";
        var channels = AppServices.Instance.Videos.Channels;
        _status.Text = AppServices.Instance.Videos.LoadError ?? (channels.Count == 0 ? "No channels yet. Scan your Videos library, then create a channel from a selection of videos." : "Guide times are local. Click a program for details, or Watch live to tune in.");
        var ruler = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(190, 0, 0, 0) };
        for (var i = 0; i < 6; i++) ruler.Children.Add(new TextBlock { Text = from.AddMinutes(i * 30).ToLocalTime().ToString("HH:mm"), Width = 30 * PixelsPerMinute, Opacity = 0.6 });
        _guide.Children.Add(ruler);
        foreach (var channel in channels)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var label = new StackPanel { Width = 180, Margin = new Thickness(0, 0, 10, 0), Spacing = 6 };
            label.Children.Add(new TextBlock { Text = channel.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            var watch = new Button { Content = "▶ Watch live" };
            watch.Click += async (_, _) => await WatchAsync(channel); label.Children.Add(watch);
            var remove = new Button { Content = "Remove channel", FontSize = 11 };
            remove.Click += async (_, _) => await RemoveAsync(channel); label.Children.Add(remove);
            row.Children.Add(label);
            foreach (var programme in TvSchedule.GetGuide(channel, from, TimeSpan.FromHours(3)))
            {
                var start = programme.Start < from ? from : programme.Start;
                var end = programme.End > until ? until : programme.End;
                var live = programme.Start <= now && programme.End > now;
                var text = new StackPanel { Spacing = 6 };
                text.Children.Add(new TextBlock { Text = live ? "ON NOW" : programme.Start.ToLocalTime().ToString("HH:mm"), FontSize = 11, Opacity = 0.8 });
                text.Children.Add(new TextBlock { Text = programme.Video.Title, TextWrapping = TextWrapping.Wrap, MaxHeight = 50, FontWeight = FontWeight.SemiBold });
                text.Children.Add(new TextBlock { Text = $"{programme.Start.ToLocalTime():HH:mm} – {programme.End.ToLocalTime():HH:mm}", FontSize = 11 });
                var cell = new Button { Content = text, Width = Math.Max(1, (end - start).TotalMinutes * PixelsPerMinute), Height = 125,
                    Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush(Color.Parse(live ? "#294D70" : "#263449")), Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.Parse("#43546B")), BorderThickness = new Thickness(1) };
                ToolTip.SetTip(cell, $"{programme.Video.Title}\n{programme.Start.ToLocalTime():f} – {programme.End.ToLocalTime():t}");
                cell.Click += (_, _) => _status.Text = $"{channel.Name} · {programme.Video.Title} · {programme.Start.ToLocalTime():f} – {programme.End.ToLocalTime():t}. Watch live joins the currently airing program.";
                row.Children.Add(cell);
            }
            _guide.Children.Add(row);
        }
    }

    private async Task WatchAsync(TvChannel channel)
    {
        if (TopLevel.GetTopLevel(this) is not Window host) return;
        var programme = TvSchedule.Now(channel, DateTimeOffset.UtcNow);
        if (programme is null) { _status.Text = "This channel has no playable videos."; return; }
        try { await new VideoPlayerWindow(programme.Video, false, channel).ShowDialog(host); }
        catch (Exception ex) { _status.Text = "Unable to open channel: " + ex.Message; }
    }

    private async Task CreateChannelAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window host) return;
        var videos = AppServices.Instance.Videos.Videos.Where(v => v.DurationSeconds >= 1).ToList();
        if (videos.Count == 0) { _status.Text = "Scan a video folder first. Channels need videos with known durations."; return; }
        var dialog = new Window { Title = "Create TV channel", Width = 650, Height = 640, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 12) };
        var name = new TextBox { Watermark = "Channel name, e.g. Movie Night" };
        header.Children.Add(name);
        header.Children.Add(new TextBlock { Text = "Choose videos with Ctrl/Shift. The lineup plays in the displayed path order and repeats. Broadcasting starts when you create the channel.", TextWrapping = TextWrapping.Wrap });
        var selectAll = new Button { Content = "Select all" }; header.Children.Add(selectAll);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var bottom = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "Create channel" }; bottom.Children.Add(error); bottom.Children.Add(save);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var list = new ListBox { ItemsSource = videos, SelectionMode = SelectionMode.Multiple,
            ItemTemplate = new FuncDataTemplate<VideoInfo>((v, _) => new TextBlock { Text = $"{v?.Title} · {v?.DurationLabel}\n{v?.Folder}", TextWrapping = TextWrapping.Wrap }) };
        selectAll.Click += (_, _) => list.SelectAll(); root.Children.Add(list); dialog.Content = root;
        save.Click += (_, _) =>
        {
            try
            {
                var selected = list.SelectedItems?.Cast<VideoInfo>().ToHashSet() ?? [];
                AppServices.Instance.Videos.AddChannel(name.Text ?? "", videos.Where(selected.Contains), DateTimeOffset.UtcNow);
                dialog.Close();
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        await dialog.ShowDialog(host); Reload();
    }

    private async Task RemoveAsync(TvChannel channel)
    {
        if (TopLevel.GetTopLevel(this) is not Window host) return;
        var dialog = new Window { Title = "Remove channel", Width = 400, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = $"Remove {channel.Name}? Video files will be kept.", TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = "Remove" };
        button.Click += (_, _) => dialog.Close(true); panel.Children.Add(button); dialog.Content = panel;
        if (!await dialog.ShowDialog<bool>(host)) return;
        try { AppServices.Instance.Videos.RemoveChannel(channel.Id); Reload(); }
        catch (Exception ex) { _status.Text = ex.Message; }
    }
}
