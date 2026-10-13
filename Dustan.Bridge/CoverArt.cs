using Dustan.Models;
using Dustan.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Dustan.Bridge;

/// <summary>
/// Large artwork for Now Playing and album pages. The library keeps 256 px thumbnails, which look soft
/// at hero sizes, so this caches a 1200 px copy from folder art or the first track's embedded picture.
/// </summary>
internal static class CoverArt
{
    private const int MaxSize = 1200;

    public static string? HighResolution(AlbumInfo album, Func<string?> firstTrackPath)
    {
        var thumbnail = album.ArtworkPath is { } t && File.Exists(t) ? t : null;
        var directory = Path.Combine(AppPaths.DataDirectory, "artwork-large");
        var key = ArtworkCache.AlbumKey(album.AlbumArtist, album.Name, album.FolderPath);
        var target = Path.Combine(directory, key + ".jpg");
        // Tag edits replace the thumbnail; regenerate when it is newer than the cached copy.
        if (File.Exists(target) && (thumbnail is null || File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(thumbnail)))
            return target;
        try
        {
            var bytes = (string.IsNullOrEmpty(album.FolderPath) ? null : ArtworkCache.TryReadFolderArt(album.FolderPath))
                ?? (firstTrackPath() is { } track ? TagReader.ReadArtwork(track) : null);
            if (bytes is null) return thumbnail;
            using var image = Image.Load(bytes);
            if (image.Width > MaxSize || image.Height > MaxSize)
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(MaxSize, MaxSize), Mode = ResizeMode.Max }));
            Directory.CreateDirectory(directory);
            var temp = target + ".tmp";
            image.SaveAsJpeg(temp, new JpegEncoder { Quality = 90 });
            File.Move(temp, target, overwrite: true);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageFormatException or NotSupportedException or UnknownImageFormatException)
        {
            return thumbnail;
        }
    }
}
