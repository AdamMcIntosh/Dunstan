using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Dustan.Helpers;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Views;

public partial class EditMetadataDialog : Window
{
    private readonly TagEditScope _scope;
    private readonly IReadOnlyList<TrackInfo> _tracks;
    private byte[]? _artworkBytes;
    private bool _artworkChanged;

    public TagWriteResult? Result { get; private set; }

    public EditMetadataDialog() : this(TagEditScope.Track, [new TrackInfo()])
    {
    }

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
            TitleBox.IsVisible = false;
            TrackNumberBox.IsVisible = false;
            DiscNumberBox.IsVisible = false;
            ArtistBox.IsVisible = false;
            AlbumBox.Text = first.Album;
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
        RemoveArtBox.IsCheckedChanged += (_, _) =>
        {
            if (RemoveArtBox.IsChecked == true)
            {
                ArtPreview.Source = PlaybackHelpers.CreateArtworkImage(null);
            }
        };
    }

    private void SetPreviewFromLibrary(TrackInfo track)
    {
        if (track.AlbumId is long albumId)
        {
            var album = AppServices.Instance.Library.GetAlbum(albumId);
            ArtPreview.Source = PlaybackHelpers.CreateArtworkImage(album?.ArtworkPath);
        }
        else
        {
            ArtPreview.Source = PlaybackHelpers.CreateArtworkImage(null);
        }
    }

    private async void ChangeArt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose artwork",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images") { Patterns = ["*.jpg", "*.jpeg", "*.png"] }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _artworkBytes = await File.ReadAllBytesAsync(path);
            _artworkChanged = true;
            RemoveArtBox.IsChecked = false;
            ArtPreview.Source = new Bitmap(path);
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.IsVisible = true;
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        SaveProgress.IsVisible = true;
        ErrorText.IsVisible = false;

        try
        {
            var edit = BuildEdit();
            Result = _scope == TagEditScope.Album
                ? await AppServices.Instance.Tags.ApplyAlbumAsync(_tracks, edit)
                : await AppServices.Instance.Tags.ApplyTrackAsync(_tracks[0], edit);

            if (Result.HasFailures && Result.Succeeded == 0)
            {
                ErrorText.Text = string.Join(Environment.NewLine, Result.Errors.Take(5));
                ErrorText.IsVisible = true;
                return;
            }

            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.IsVisible = true;
        }
        finally
        {
            SaveProgress.IsVisible = false;
            SaveButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
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

    private static int ToInt(decimal? value) =>
        value is null ? 0 : (int)Math.Clamp(Math.Round(value.Value), 0, 9999);
}
