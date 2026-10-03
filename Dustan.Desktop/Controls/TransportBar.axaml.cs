using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Controls;

public partial class TransportBar : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _draggingSeek;
    private bool _loaded;
    private PlaybackService Playback => AppServices.Instance.Playback;

    public TransportBar()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => OnLoaded();
        DetachedFromVisualTree += (_, _) => OnUnloaded();
    }

    private void OnLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        VolumeSlider.Value = Math.Round(Playback.Volume * 100);
        VolumePercentText.Text = $"{(int)VolumeSlider.Value}%";
        Playback.StateChanged += PlaybackOnStateChanged;
        Playback.CurrentTrackChanged += PlaybackOnCurrentTrackChanged;
        _timer.Tick += TimerOnTick;
        _timer.Start();
        RefreshTransport();
    }

    private void OnUnloaded()
    {
        if (!_loaded)
        {
            return;
        }

        _loaded = false;
        Playback.StateChanged -= PlaybackOnStateChanged;
        Playback.CurrentTrackChanged -= PlaybackOnCurrentTrackChanged;
        _timer.Tick -= TimerOnTick;
        _timer.Stop();
    }

    private void PlaybackOnStateChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RefreshTransport);

    private void PlaybackOnCurrentTrackChanged(object? sender, TrackInfo? track) =>
        Dispatcher.UIThread.Post(RefreshTransport);

    private void TimerOnTick(object? sender, EventArgs e)
    {
        if (_draggingSeek)
        {
            return;
        }

        var duration = Playback.Duration;
        var position = Playback.Position;
        if (duration > TimeSpan.Zero)
        {
            SeekSlider.Maximum = duration.TotalSeconds;
            SeekSlider.Value = Math.Clamp(position.TotalSeconds, 0, duration.TotalSeconds);
            DurationText.Text = FormatTime(duration);
        }
        else
        {
            SeekSlider.Value = 0;
            DurationText.Text = "0:00";
        }

        PositionText.Text = FormatTime(position);
    }

    private void RefreshTransport()
    {
        var track = Playback.CurrentTrack;
        TitleText.Text = track?.Title ?? "Nothing playing";
        ArtistText.Text = track is null ? "" : $"{track.Artist} — {track.Album}";
        PlayPauseButton.Content = Playback.IsPlaying ? "Pause" : "Play";
        ShuffleButton.IsChecked = Playback.IsShuffleEnabled;
        RepeatModeButton.Content = Playback.Repeat switch
        {
            RepeatMode.One => "Repeat 1",
            RepeatMode.Album => "Repeat",
            _ => "Repeat"
        };
        RepeatModeButton.Opacity = Playback.Repeat == RepeatMode.Off ? 0.5 : 1.0;
        ToolTip.SetTip(RepeatModeButton, Playback.Repeat switch
        {
            RepeatMode.Off => "Repeat: Off",
            RepeatMode.Album => "Repeat: Album",
            _ => "Repeat: One"
        });
    }

    private void PlayPauseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Playback.TogglePlayPause();
    private void PreviousButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Playback.Previous();
    private void NextButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Playback.Next();
    private void ShuffleButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Playback.ToggleShuffle();
    private void RepeatModeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Playback.CycleRepeat();

    private void VolumeSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        var percent = (int)Math.Round(Math.Clamp(e.NewValue, 0, 100));
        VolumePercentText.Text = $"{percent}%";
        Playback.SetVolume(percent / 100.0);
    }

    private void SeekSlider_PointerPressed(object? sender, PointerPressedEventArgs e) => _draggingSeek = true;

    private void SeekSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggingSeek = false;
        Playback.Seek(TimeSpan.FromSeconds(SeekSlider.Value));
    }

    private void SeekSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_draggingSeek)
        {
            PositionText.Text = FormatTime(TimeSpan.FromSeconds(e.NewValue));
        }
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");
    }
}
