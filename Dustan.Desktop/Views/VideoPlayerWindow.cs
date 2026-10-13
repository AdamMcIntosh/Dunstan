using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Dustan.Models;
using Dustan.Services;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;

namespace Dustan.Views;

public sealed class VideoPlayerWindow : Window
{
    private readonly LibVLC _vlc = new("--quiet", "--no-video-title-show");
    private readonly MediaPlayer _player;
    private readonly VideoView _view;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _title = new() { FontSize = 19, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _time = new();
    private readonly Slider _seek = new() { Minimum = 0, Maximum = 1, MinWidth = 200 };
    private readonly ComboBox _audio = new() { MinWidth = 140, MaxWidth = 220, PlaceholderText = "Audio" };
    private readonly ComboBox _subtitles = new() { MinWidth = 140, MaxWidth = 220, PlaceholderText = "Subtitles" };
    private readonly Button _pause = new() { Content = "Pause" };
    private readonly TvChannel? _channel;
    private VideoInfo _video;
    private Media? _media;
    private DateTimeOffset? _programmeStart;
    private double _pendingSeek;
    private bool _updating;
    private bool _closed;
    private bool _completed;
    private int _ticks;
    private string _audioSignature = "";
    private string _subtitleSignature = "";
    private sealed record TrackChoice(int Id, string Name) { public override string ToString() => Name; }

    public VideoPlayerWindow(VideoInfo video, bool resume, TvChannel? channel = null)
    {
        _video = video;
        _channel = channel;
        Title = video.Title;
        Width = 1100; Height = 760; MinWidth = 760; MinHeight = 500;
        Background = Brushes.Black;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _player = new MediaPlayer(_vlc) { Volume = 80, EnableHardwareDecoding = true };
        _view = new VideoView { MediaPlayer = _player };
        var root = new DockPanel();
        var controls = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        DockPanel.SetDock(controls, Dock.Bottom);
        controls.Children.Add(_title);
        controls.Children.Add(_status);
        var timeline = new DockPanel();
        _time.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(_time, Dock.Right);
        timeline.Children.Add(_time); timeline.Children.Add(_seek);
        controls.Children.Add(timeline);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        _pause.Click += (_, _) => TogglePause();
        buttons.Children.Add(_pause);
        AddButton(buttons, "−10s", () => SeekRelative(-10));
        AddButton(buttons, "+30s", () => SeekRelative(30));
        AddButton(buttons, "Fullscreen (F)", ToggleFullscreen);
        if (channel is not null) AddButton(buttons, "Go live", () => Tune(force: true));
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = 80, Width = 100, Margin = new Thickness(8, 0) };
        ToolTip.SetTip(volume, "Volume");
        volume.ValueChanged += (_, _) => _player.Volume = (int)volume.Value;
        buttons.Children.Add(new TextBlock { Text = "Volume", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) });
        buttons.Children.Add(volume);
        controls.Children.Add(buttons);
        var tracks = new WrapPanel();
        tracks.Children.Add(_audio); tracks.Children.Add(_subtitles);
        AddButton(tracks, "Load subtitles…", async () => await LoadSubtitlesAsync());
        if (channel is null)
        {
            var speed = new ComboBox { ItemsSource = new[] { "0.5", "0.75", "1", "1.25", "1.5", "2" }, SelectedIndex = 2, MinWidth = 75 };
            ToolTip.SetTip(speed, "Playback speed");
            speed.SelectionChanged += (_, _) => { if (speed.SelectedItem is string s) _player.SetRate(float.Parse(s, CultureInfo.InvariantCulture)); };
            tracks.Children.Add(speed);
        }
        controls.Children.Add(tracks);
        root.Children.Add(controls); root.Children.Add(_view); Content = root;
        _seek.IsEnabled = channel is null;
        _seek.ValueChanged += (_, _) => { if (!_updating && channel is null && _player.IsSeekable) _player.Time = (long)(_seek.Value * 1000); };
        _audio.SelectionChanged += (_, _) => { if (!_updating && _audio.SelectedItem is TrackChoice t) _player.SetAudioTrack(t.Id); };
        _subtitles.SelectionChanged += (_, _) => { if (!_updating && _subtitles.SelectedItem is TrackChoice t) _player.SetSpu(t.Id); };
        _player.Playing += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_closed) return;
            if (_pendingSeek > 0 && _player.IsSeekable) { _player.Time = (long)(_pendingSeek * 1000); _pendingSeek = 0; }
        });
        _player.EncounteredError += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (!_closed) _status.Text = "Playback failed. Check the NAS mount and installed VLC codecs. Close this player to choose another video.";
        });
        _player.EndReached += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_closed) return;
            if (_channel is null) { _completed = true; SaveProgress(); _status.Text = "Finished"; }
            else _status.Text = "Waiting for the next scheduled program…";
        });
        Opened += (_, _) =>
        {
            AppServices.Instance.Playback.Pause();
            if (_channel is null) Start(video, resume ? video.PositionSeconds : 0); else Tune(force: true);
            _timer.Start();
        };
        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) =>
        {
            _timer.Stop(); SaveProgress(); _closed = true;
            _player.Stop(); _view.MediaPlayer = null; _player.Media = null;
            _media?.Dispose(); _player.Dispose(); _vlc.Dispose();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (WindowState == WindowState.FullScreen) WindowState = WindowState.Normal; else Close(); }
            else if (e.Key == Key.F) ToggleFullscreen();
            else if (e.Key == Key.Space) TogglePause();
            else if (e.Key == Key.Left) SeekRelative(-10);
            else if (e.Key == Key.Right) SeekRelative(30);
            else return;
            e.Handled = true;
        };
    }

    private static void AddButton(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 0) };
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }

    private void Start(VideoInfo video, double seconds)
    {
        _player.Stop(); _player.Media = null; _media?.Dispose(); _media = null;
        _video = video; _completed = false; _pendingSeek = seconds;
        _audioSignature = _subtitleSignature = "";
        _title.Text = _channel is null ? video.Title : $"{_channel.Name} · {video.Title}";
        Title = _title.Text;
        _status.Text = _channel is null ? "Space: pause · ←/→: seek · F: fullscreen · Esc: return" : "Live schedule · Go live returns to the current broadcast position";
        if (!File.Exists(video.Path)) { _status.Text = $"Video unavailable: {video.Path}. Check the NAS mount."; return; }
        try
        {
            _media = new Media(_vlc, video.Path, FromType.FromPath);
            if (seconds > 0) _media.AddOption(":start-time=" + seconds.ToString(CultureInfo.InvariantCulture));
            if (!_player.Play(_media)) _status.Text = "Unable to start this video.";
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private void Tune(bool force = false)
    {
        if (_channel is null) return;
        var now = DateTimeOffset.UtcNow;
        var programme = TvSchedule.Now(_channel, now);
        if (programme is null) { _status.Text = "This channel has no playable schedule."; return; }
        if (force || _programmeStart != programme.Start)
        {
            _programmeStart = programme.Start;
            Start(programme.Video, programme.OffsetAt(now).TotalSeconds);
        }
    }

    private void Tick()
    {
        if (_closed) return;
        Tune();
        _updating = true;
        try
        {
            _seek.Maximum = Math.Max(1, _player.Length / 1000.0);
            _seek.Value = Math.Max(0, _player.Time / 1000.0);
            _time.Text = $"{TimeSpan.FromMilliseconds(Math.Max(0, _player.Time)):hh\\:mm\\:ss} / {TimeSpan.FromMilliseconds(Math.Max(0, _player.Length)):hh\\:mm\\:ss}";
            _pause.Content = _player.IsPlaying ? "Pause" : "Play";
            UpdateTracks(_audio, _player.AudioTrackDescription.Select(t => new TrackChoice(t.Id, t.Name)).ToList(), _player.AudioTrack, ref _audioSignature);
            UpdateTracks(_subtitles, _player.SpuDescription.Select(t => new TrackChoice(t.Id, t.Name)).ToList(), _player.Spu, ref _subtitleSignature);
        }
        finally { _updating = false; }
        if (++_ticks % 10 == 0) SaveProgress();
    }

    private static void UpdateTracks(ComboBox combo, List<TrackChoice> tracks, int selected, ref string signature)
    {
        var current = string.Join('|', tracks.Select(t => $"{t.Id}:{t.Name}"));
        if (current == signature) return;
        signature = current; combo.ItemsSource = tracks; combo.SelectedItem = tracks.FirstOrDefault(t => t.Id == selected);
    }

    private void SaveProgress()
    {
        if (_channel is not null || _closed || (_player.Time < 0 && !_completed)) return;
        try { AppServices.Instance.Videos.SaveProgress(_video.Path, Math.Max(0, _player.Time / 1000.0), _completed); }
        catch (Exception ex) { _status.Text = "Could not save progress: " + ex.Message; }
    }

    private void SeekRelative(int seconds)
    {
        if (_channel is null && _player.IsSeekable) _player.Time = Math.Clamp(_player.Time + seconds * 1000, 0, Math.Max(0, _player.Length));
    }

    private void TogglePause()
    {
        if (_completed && _channel is null) Start(_video, 0);
        else if (_player.IsPlaying) _player.Pause();
        else _player.Play();
    }

    private void ToggleFullscreen() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    private async Task LoadSubtitlesAsync()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load subtitle file", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Subtitles") { Patterns = ["*.srt", "*.ass", "*.ssa", "*.vtt", "*.sub"] }]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (!_closed && path is not null && !_player.AddSlave(MediaSlaveType.Subtitle, new Uri(path).AbsoluteUri, true))
                _status.Text = "Unable to load this subtitle file.";
        }
        catch (Exception ex) { if (!_closed) _status.Text = ex.Message; }
    }
}
