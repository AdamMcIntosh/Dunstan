using Dustan.Models;
using TagLib;

namespace Dustan.Services;

public static class TagReader
{
    public static TrackInfo Read(string path, long size, long mtime)
    {
        var fallbackTitle = Path.GetFileNameWithoutExtension(path);
        try
        {
            using var file = TagLib.File.Create(path);
            var tag = file.Tag;
            var title = FirstNonEmpty(tag.Title, fallbackTitle);
            var artist = FirstNonEmpty(tag.FirstPerformer, tag.FirstAlbumArtist, "Unknown Artist");
            var albumArtist = FirstNonEmpty(tag.FirstAlbumArtist, tag.FirstPerformer, "Unknown Artist");
            var album = FirstNonEmpty(tag.Album, "Unknown Album");
            var genre = tag.FirstGenre ?? "";
            var year = (int)tag.Year;
            var trackNumber = (int)tag.Track;
            var discNumber = (int)tag.Disc;
            var duration = file.Properties?.Duration.TotalSeconds ?? 0;

            return new TrackInfo
            {
                Path = path,
                FileSize = size,
                ModifiedUtcTicks = mtime,
                Title = title,
                Artist = artist,
                Album = album,
                AlbumArtist = albumArtist,
                TrackNumber = trackNumber,
                DiscNumber = discNumber,
                Year = year,
                Genre = genre,
                DurationSeconds = duration
            };
        }
        catch
        {
            return new TrackInfo
            {
                Path = path,
                FileSize = size,
                ModifiedUtcTicks = mtime,
                Title = fallbackTitle,
                Artist = "Unknown Artist",
                Album = "Unknown Album",
                AlbumArtist = "Unknown Artist"
            };
        }
    }

    public static byte[]? ReadArtwork(string path)
    {
        try
        {
            using var file = TagLib.File.Create(path);
            var picture = file.Tag.Pictures?
                .OrderByDescending(p => p.Type == PictureType.FrontCover)
                .FirstOrDefault();
            return picture?.Data?.Data is { Length: > 0 } data ? data : null;
        }
        catch
        {
            return null;
        }
    }

    public static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }
}
