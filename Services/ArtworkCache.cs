using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

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
                // Fall back to raw bytes if decode fails but data looks like jpeg.
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
        uint maxEdge,
        CancellationToken cancellationToken)
    {
        using var input = new InMemoryRandomAccessStream();
        await input.WriteAsync(imageBytes.AsBuffer());
        input.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(input);
        cancellationToken.ThrowIfCancellationRequested();

        var width = decoder.PixelWidth;
        var height = decoder.PixelHeight;
        var scale = Math.Min(1.0, maxEdge / (double)Math.Max(width, height));
        var scaledWidth = Math.Max(1u, (uint)Math.Round(width * scale));
        var scaledHeight = Math.Max(1u, (uint)Math.Round(height * scale));

        var transform = new BitmapTransform
        {
            ScaledWidth = scaledWidth,
            ScaledHeight = scaledHeight,
            InterpolationMode = BitmapInterpolationMode.Fant
        };

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        var pixels = pixelData.DetachPixelData();

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            scaledWidth,
            scaledHeight,
            96,
            96,
            pixels);
        await encoder.FlushAsync();

        output.Seek(0);
        using var reader = new DataReader(output);
        await reader.LoadAsync((uint)output.Size);
        var bytes = new byte[output.Size];
        reader.ReadBytes(bytes);
        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
    }
}
