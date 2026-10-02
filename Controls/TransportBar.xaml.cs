using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Media.Playback;

namespace Dustan.Controls;

public sealed partial class TransportBar : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _draggingSeek;
    private PlaybackService Playback => AppServices.Instance.Playback;

    public TransportBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        VolumeSlider.Value = Math.Round(Playback.Volume * 100);
        VolumePercentText.Text = $"{(int)VolumeSlider.Value}%";
        Playback.StateChanged += PlaybackOnStateChanged;
        Playback.CurrentTrackChanged += PlaybackOnCurrentTrackChanged;
        _timer.Tick += TimerOnTick;
        _timer.Start();
        RefreshTransport();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Playback.StateChanged -= PlaybackOnStateChanged;
        Playback.CurrentTrackChanged -= PlaybackOnCurrentTrackChanged;
        _timer.Tick -= TimerOnTick;
        _timer.Stop();
    }

    private void PlaybackOnStateChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshTransport);

    private void PlaybackOnCurrentTrackChanged(object? sender, TrackInfo? track) =>
        DispatcherQueue.TryEnqueue(RefreshTransport);

    private void TimerOnTick(object? sender, object e)
    {
        if (_draggingSeek)
        {
            return;
        }

        var session = Playback.Player.PlaybackSession;
        var duration = session.NaturalDuration;
        var position = session.Position;
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
        PlayPauseButton.Content = Playback.Player.CurrentState == MediaPlayerState.Playing ? "\uE769" : "\uE768";
        ShuffleButton.IsChecked = Playback.IsShuffleEnabled;
        RepeatModeButton.Content = Playback.Repeat switch
        {
            RepeatMode.One => "\uE8ED",
            RepeatMode.Album => "\uE8EE",
            _ => "\uE8EE"
        };
        RepeatModeButton.Opacity = Playback.Repeat == RepeatMode.Off ? 0.5 : 1.0;
        ToolTipService.SetToolTip(RepeatModeButton, Playback.Repeat switch
        {
            RepeatMode.Off => "Repeat: Off",
            RepeatMode.Album => "Repeat: Album",
            _ => "Repeat: One"
        });
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => Playback.TogglePlayPause();
    private void PreviousButton_Click(object sender, RoutedEventArgs e) => Playback.Previous();
    private void NextButton_Click(object sender, RoutedEventArgs e) => Playback.Next();
    private void ShuffleButton_Click(object sender, RoutedEventArgs e) => Playback.ToggleShuffle();
    private void RepeatModeButton_Click(object sender, RoutedEventArgs e) => Playback.CycleRepeat();

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var percent = (int)Math.Round(Math.Clamp(e.NewValue, 0, 100));
        VolumePercentText.Text = $"{percent}%";
        Playback.SetVolume(percent / 100.0);
    }

    private void SeekSlider_PointerPressed(object sender, PointerRoutedEventArgs e) => _draggingSeek = true;

    private void SeekSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _draggingSeek = false;
        Playback.Seek(TimeSpan.FromSeconds(SeekSlider.Value));
    }

    private void SeekSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
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
