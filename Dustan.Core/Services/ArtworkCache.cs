using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Dustan.Services;

public static class ArtworkCache
{
    private static readonly string[] FolderArtNames =
    [
        "cover.jpg", "cover.jpeg", "cover.png",
        "folder.jpg", "folder.jpeg", "folder.png",
        "front.jpg", "front.jpeg", "front.png"
    ];

    public static string AlbumKey(string albumArtist, string album, string? folderPath = null)
    {
        var folder = NormalizeFolder(folderPath);
        var raw = $"{albumArtist?.Trim() ?? ""}\n{album?.Trim() ?? ""}\n{folder}".ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public static string NormalizeFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(folderPath.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return folderPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    public static string GetThumbnailPath(string albumKey) =>
        Path.Combine(AppPaths.ArtworkDirectory, $"{albumKey}.jpg");

    public static async Task<string?> EnsureThumbnailAsync(
        string albumKey,
        byte[]? embeddedImageBytes,
        string trackDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.ArtworkDirectory);
        var dest = GetThumbnailPath(albumKey);
        if (File.Exists(dest) && new FileInfo(dest).Length > 0)
        {
            return dest;
        }

        return await WriteThumbnailAsync(dest, embeddedImageBytes, trackDirectory, cancellationToken).ConfigureAwait(false);
    }

    public static Task<string?> ReplaceThumbnailAsync(
        string albumKey,
        byte[]? embeddedImageBytes,
        string trackDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.ArtworkDirectory);
        var dest = GetThumbnailPath(albumKey);
        try
        {
            if (File.Exists(dest))
            {
                File.Delete(dest);
            }
        }
        catch
        {
            // overwrite below if delete fails
        }

        return WriteThumbnailAsync(dest, embeddedImageBytes, trackDirectory, cancellationToken);
    }

    private static async Task<string?> WriteThumbnailAsync(
        string dest,
        byte[]? embeddedImageBytes,
        string trackDirectory,
        CancellationToken cancellationToken)
    {
        byte[]? source = embeddedImageBytes;
        if (source is null || source.Length == 0)
        {
            source = TryReadFolderArt(trackDirectory);
        }

        if (source is null || source.Length == 0)
        {
            return null;
        }

        try
        {
            await WriteJpegThumbnailAsync(source, dest, 256, cancellationToken).ConfigureAwait(false);
            return File.Exists(dest) ? dest : null;
        }
        catch
        {
            try
            {
                await File.WriteAllBytesAsync(dest, source, cancellationToken).ConfigureAwait(false);
                return dest;
            }
            catch
            {
                return null;
            }
        }
    }

    public static byte[]? TryReadFolderArt(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        foreach (var name in FolderArtNames)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                return File.ReadAllBytes(path);
            }
            catch
            {
                // ignore unreadable folder art
            }
        }

        return null;
    }

    private static async Task WriteJpegThumbnailAsync(
        byte[] imageBytes,
        string destinationPath,
        int maxEdge,
        CancellationToken cancellationToken)
    {
        using var image = Image.Load(imageBytes);
        var longest = Math.Max(image.Width, image.Height);
        if (longest > maxEdge)
        {
            var scale = maxEdge / (double)longest;
            var width = Math.Max(1, (int)Math.Round(image.Width * scale));
            var height = Math.Max(1, (int)Math.Round(image.Height * scale));
            image.Mutate(ctx => ctx.Resize(width, height));
        }

        await image.SaveAsJpegAsync(destinationPath, new JpegEncoder { Quality = 85 }, cancellationToken)
            .ConfigureAwait(false);
    }
}
