using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Dustan.Views;

public sealed partial class EditMetadataDialog : ContentDialog
{
    private readonly TagEditScope _scope;
    private readonly IReadOnlyList<TrackInfo> _tracks;
    private byte[]? _artworkBytes;
    private bool _artworkChanged;

    public TagWriteResult? Result { get; private set; }

    public EditMetadataDialog(TagEditScope scope, IReadOnlyList<TrackInfo> tracks)
    {
        InitializeComponent();
        _scope = scope;
        _tracks = tracks;

        var first = tracks[0];
        if (scope == TagEditScope.Album)
        {
            Title = "Edit album";
            ScopeText.Text = $"Changes write to all {tracks.Count} tracks in this album on the library share. Album artist is also written as each track’s artist (except Various Artists compilations).";
            TitleBox.Visibility = Visibility.Collapsed;
            TrackNumberBox.Visibility = Visibility.Collapsed;
            DiscNumberBox.Visibility = Visibility.Collapsed;
            ArtistBox.Visibility = Visibility.Collapsed;
            AlbumBox.Text = first.Album;
            AlbumArtistBox.Header = "Album artist";
            AlbumArtistBox.Text = first.AlbumArtist;
            YearBox.Value = first.Year;
            GenreBox.Text = first.Genre;
        }
        else
        {
            Title = "Edit track";
            ScopeText.Text = Path.GetFileName(first.Path);
            TitleBox.Text = first.Title;
            TrackNumberBox.Value = first.TrackNumber;
            DiscNumberBox.Value = first.DiscNumber;
            ArtistBox.Text = first.Artist;
            AlbumBox.Text = first.Album;
            AlbumArtistBox.Text = first.AlbumArtist;
            YearBox.Value = first.Year;
            GenreBox.Text = first.Genre;
        }

        SetPreviewFromLibrary(first);
        RemoveArtBox.Checked += (_, _) =>
        {
            ArtPreview.ImageSource = PlaybackHelpers.CreateArtworkImage(null);
        };
    }

    private void SetPreviewFromLibrary(TrackInfo track)
    {
        if (track.AlbumId is long albumId)
        {
            var album = AppServices.Instance.Library.GetAlbum(albumId);
            ArtPreview.ImageSource = PlaybackHelpers.CreateArtworkImage(album?.ArtworkPath);
        }
        else
        {
            ArtPreview.ImageSource = PlaybackHelpers.CreateArtworkImage(null);
        }
    }

    private async void ChangeArt_Click(object sender, RoutedEventArgs e)
    {
        var window = App.MainWindow;
        if (window is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            _artworkBytes = await File.ReadAllBytesAsync(file.Path);
            _artworkChanged = true;
            RemoveArtBox.IsChecked = false;
            var image = new BitmapImage();
            image.SetSource(await file.OpenReadAsync());
            ArtPreview.ImageSource = image;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        IsPrimaryButtonEnabled = false;
        IsEnabled = false;
        SaveProgress.Visibility = Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var edit = BuildEdit();
            Result = _scope == TagEditScope.Album
                ? await AppServices.Instance.Tags.ApplyAlbumAsync(_tracks, edit)
                : await AppServices.Instance.Tags.ApplyTrackAsync(_tracks[0], edit);

            if (Result.HasFailures && Result.Succeeded == 0)
            {
                args.Cancel = true;
                ErrorText.Text = string.Join(Environment.NewLine, Result.Errors.Take(5));
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            SaveProgress.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = true;
            IsEnabled = true;
            deferral.Complete();
        }
    }

    private TagEdit BuildEdit() => new()
    {
        Title = TitleBox.Text ?? "",
        Artist = ArtistBox.Text ?? "",
        Album = AlbumBox.Text ?? "",
        AlbumArtist = AlbumArtistBox.Text ?? "",
        TrackNumber = ToInt(TrackNumberBox.Value),
        DiscNumber = ToInt(DiscNumberBox.Value),
        Year = ToInt(YearBox.Value),
        Genre = GenreBox.Text ?? "",
        ArtworkBytes = _artworkBytes,
        ArtworkChanged = _artworkChanged,
        RemoveArtwork = RemoveArtBox.IsChecked == true
    };

    private static int ToInt(double value) =>
        double.IsNaN(value) ? 0 : (int)Math.Clamp(Math.Round(value), 0, 9999);
}
