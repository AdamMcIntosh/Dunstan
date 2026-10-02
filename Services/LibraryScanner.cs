using Dustan.Models;
using File = System.IO.File;

namespace Dustan.Services;

public sealed class LibraryScanner
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac"
    };

    private readonly LibraryDb _db;
    private readonly object _progressLock = new();
    private CancellationTokenSource? _cts;
    private Task? _scanTask;

    public event EventHandler<ScanProgress>? ProgressChanged;

    public bool IsScanning => _scanTask is { IsCompleted: false };

    public LibraryScanner(LibraryDb db)
    {
        _db = db;
    }

    public Task StartScanAsync(string libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot))
        {
            throw new ArgumentException("Library path is required.", nameof(libraryRoot));
        }

        if (!Directory.Exists(libraryRoot))
        {
            throw new DirectoryNotFoundException($"Library path not found: {libraryRoot}");
        }

        Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _scanTask = Task.Run(() => Scan(libraryRoot, token), token);
        return _scanTask;
    }

    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignored
        }
    }

    private void Scan(string libraryRoot, CancellationToken token)
    {
        var filesSeen = 0;
        var filesTagged = 0;
        var filesSkipped = 0;
        var filesRemoved = 0;

        void Report(string current, bool complete = false, string? error = null)
        {
            lock (_progressLock)
            {
                ProgressChanged?.Invoke(this, new ScanProgress
                {
                    FilesSeen = filesSeen,
                    FilesTagged = filesTagged,
                    FilesSkipped = filesSkipped,
                    FilesRemoved = filesRemoved,
                    CurrentPath = current,
                    IsComplete = complete,
                    Error = error
                });
            }
        }

        try
        {
            var existing = _db.GetTrackFingerprints();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in EnumerateAudioFiles(libraryRoot, token))
            {
                token.ThrowIfCancellationRequested();
                filesSeen++;
                seenPaths.Add(filePath);
                Report(filePath);

                try
                {
                    var info = new FileInfo(filePath);
                    var mtime = info.LastWriteTimeUtc.Ticks;
                    var size = info.Length;

                    if (existing.TryGetValue(filePath, out var fingerprint)
                        && fingerprint.Size == size
                        && fingerprint.Mtime == mtime)
                    {
                        filesSkipped++;
                        continue;
                    }

                    var track = TagReader.Read(filePath, size, mtime);
                    var artistName = string.IsNullOrWhiteSpace(track.Artist) ? "Unknown Artist" : track.Artist;
                    var albumArtist = string.IsNullOrWhiteSpace(track.AlbumArtist) ? artistName : track.AlbumArtist;
                    var albumName = string.IsNullOrWhiteSpace(track.Album) ? "Unknown Album" : track.Album;

                    var embeddedArt = TagReader.ReadArtwork(filePath);

                    var trackFolder = ArtworkCache.NormalizeFolder(Path.GetDirectoryName(filePath));
                    var albumKey = ArtworkCache.AlbumKey(albumArtist, albumName, trackFolder);
                    string? artworkPath = null;
                    try
                    {
                        artworkPath = ArtworkCache.EnsureThumbnailAsync(
                            albumKey,
                            embeddedArt,
                            trackFolder,
                            token).GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // keep scanning without art
                    }

                    var artistId = _db.UpsertArtist(artistName);
                    var albumId = _db.UpsertAlbum(albumName, albumArtist, track.Year, artworkPath, trackFolder);
                    track = new TrackInfo
                    {
                        Path = track.Path,
                        FileSize = track.FileSize,
                        ModifiedUtcTicks = track.ModifiedUtcTicks,
                        Title = track.Title,
                        Artist = artistName,
                        Album = albumName,
                        AlbumArtist = albumArtist,
                        TrackNumber = track.TrackNumber,
                        DiscNumber = track.DiscNumber,
                        Year = track.Year,
                        Genre = track.Genre,
                        DurationSeconds = track.DurationSeconds,
                        AlbumId = albumId,
                        ArtistId = artistId
                    };
                    _db.UpsertTrack(track, artistId, albumId);
                    filesTagged++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Bad file: skip without aborting the scan.
                    Report($"{filePath} (failed: {ex.Message})");
                }

                if (filesSeen % 25 == 0)
                {
                    Report(filePath);
                }
            }

            token.ThrowIfCancellationRequested();

            var orphanIds = existing
                .Where(kv => !seenPaths.Contains(kv.Key))
                .Select(kv => kv.Value.Id)
                .ToList();
            filesRemoved = _db.DeleteTracksByIds(orphanIds);
            _db.CleanupOrphans();
            Report("", complete: true);
        }
        catch (OperationCanceledException)
        {
            Report("", complete: true, error: "Scan cancelled.");
        }
        catch (Exception ex)
        {
            Report("", complete: true, error: ex.Message);
        }
    }

    private static IEnumerable<string> EnumerateAudioFiles(string root, CancellationToken token)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var dir = pending.Pop();

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                string? attributesError = null;
                FileAttributes attrs = 0;
                try
                {
                    attrs = File.GetAttributes(entry);
                }
                catch (Exception ex)
                {
                    attributesError = ex.Message;
                }

                if (attributesError is not null)
                {
                    continue;
                }

                if ((attrs & FileAttributes.Directory) != 0)
                {
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    pending.Push(entry);
                    continue;
                }

                var ext = Path.GetExtension(entry);
                if (AudioExtensions.Contains(ext))
                {
                    yield return entry;
                }
            }
        }
    }

}
