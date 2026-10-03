using Dustan.Models;
using TagLib;
using File = System.IO.File;

namespace Dustan.Services;

public sealed class TagEditor
{
    private readonly LibraryDb _db;
    private readonly IPlaybackService _playback;
    private readonly Func<string> _libraryRoot;

    public TagEditor(LibraryDb db, IPlaybackService playback, Func<string> libraryRoot)
    {
        _db = db;
        _playback = playback;
        _libraryRoot = libraryRoot;
    }

    public async Task<TagWriteResult> ApplyTrackAsync(TrackInfo track, TagEdit edit, CancellationToken cancellationToken = default)
        => await ApplyAsync([track], edit, TagEditScope.Track, cancellationToken).ConfigureAwait(false);

    public async Task<TagWriteResult> ApplyAlbumAsync(IReadOnlyList<TrackInfo> tracks, TagEdit edit, CancellationToken cancellationToken = default)
        => await ApplyAsync(tracks, edit, TagEditScope.Album, cancellationToken).ConfigureAwait(false);

    private async Task<TagWriteResult> ApplyAsync(
        IReadOnlyList<TrackInfo> tracks,
        TagEdit edit,
        TagEditScope scope,
        CancellationToken cancellationToken)
    {
        if (tracks.Count == 0)
        {
            return new TagWriteResult { Errors = ["No tracks to update."] };
        }

        var root = _libraryRoot();
        var playingPath = _playback.CurrentTrack?.Path;
        if (!string.IsNullOrEmpty(playingPath)
            && tracks.Any(t => string.Equals(t.Path, playingPath, PathSafety.Comparison)))
        {
            _playback.ReleaseIfPlaying(tracks.Select(t => t.Path));
        }

        var normalize = NormalizeEdit(edit);
        // Album-level artist changes must move the album in Artists. Only skip
        // overwriting per-track performers for Various Artists compilations.
        var updateTrackArtists = scope == TagEditScope.Album && !IsVariousArtist(normalize.AlbumArtist);
        var sourceAlbumId = scope == TagEditScope.Album
            ? tracks.Select(t => t.AlbumId).FirstOrDefault(id => id is > 0)
            : null;

        var succeeded = 0;
        var failed = 0;
        var errors = new List<string>();
        long? lastAlbumId = null;

        await Task.Run(async () =>
        {
            foreach (var track in tracks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!PathSafety.IsUnderLibraryRoot(track.Path, root))
                    {
                        failed++;
                        errors.Add($"{track.Path}: outside library root.");
                        continue;
                    }

                    if (!File.Exists(track.Path))
                    {
                        failed++;
                        errors.Add($"{track.Path}: file not found.");
                        continue;
                    }

                    WriteTags(track, normalize, scope, updateTrackArtists);
                    lastAlbumId = await ReindexAsync(
                            track.Path,
                            normalize,
                            scope,
                            updateTrackArtists,
                            sourceAlbumId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    // After the first successful album reindex, keep rewriting the same row.
                    if (scope == TagEditScope.Album && lastAlbumId is long resolved)
                    {
                        sourceAlbumId = resolved;
                    }

                    succeeded++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    errors.Add($"{Path.GetFileName(track.Path)}: {ex.Message}");
                }
            }

            _db.CleanupOrphans();
        }, cancellationToken).ConfigureAwait(false);

        if (succeeded > 0)
        {
            LibraryEvents.RaiseChanged();
        }

        return new TagWriteResult
        {
            Succeeded = succeeded,
            Failed = failed,
            AlbumId = lastAlbumId,
            Errors = errors
        };
    }

    private static TagEdit NormalizeEdit(TagEdit edit) => new()
    {
        Title = edit.Title?.Trim() ?? "",
        Artist = string.IsNullOrWhiteSpace(edit.Artist) ? "Unknown Artist" : edit.Artist.Trim(),
        Album = string.IsNullOrWhiteSpace(edit.Album) ? "Unknown Album" : edit.Album.Trim(),
        AlbumArtist = string.IsNullOrWhiteSpace(edit.AlbumArtist)
            ? (string.IsNullOrWhiteSpace(edit.Artist) ? "Unknown Artist" : edit.Artist.Trim())
            : edit.AlbumArtist.Trim(),
        TrackNumber = Math.Max(0, edit.TrackNumber),
        DiscNumber = Math.Max(0, edit.DiscNumber),
        Year = Math.Max(0, edit.Year),
        Genre = edit.Genre?.Trim() ?? "",
        ArtworkBytes = edit.ArtworkBytes,
        ArtworkChanged = edit.ArtworkChanged,
        RemoveArtwork = edit.RemoveArtwork
    };

    private static bool IsVariousArtist(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var n = name.Trim();
        return n.Equals("Various Artists", StringComparison.OrdinalIgnoreCase)
               || n.Equals("Various Artist", StringComparison.OrdinalIgnoreCase)
               || n.Equals("Various", StringComparison.OrdinalIgnoreCase)
               || n.Equals("VA", StringComparison.OrdinalIgnoreCase)
               || n.Equals("V/A", StringComparison.OrdinalIgnoreCase)
               || n.Equals("V.A.", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteTags(TrackInfo track, TagEdit edit, TagEditScope scope, bool updateTrackArtists)
    {
        using var file = TagLib.File.Create(track.Path);
        var tag = file.Tag;

        if (scope == TagEditScope.Track)
        {
            tag.Title = edit.Title;
            tag.Performers = [edit.Artist];
            tag.Track = (uint)edit.TrackNumber;
            tag.Disc = (uint)edit.DiscNumber;
        }
        else if (updateTrackArtists)
        {
            tag.Performers = [edit.AlbumArtist];
        }

        tag.Album = edit.Album;
        tag.AlbumArtists = [edit.AlbumArtist];
        tag.Year = (uint)edit.Year;
        tag.Genres = string.IsNullOrWhiteSpace(edit.Genre) ? [] : [edit.Genre];

        if (edit.RemoveArtwork)
        {
            tag.Pictures = [];
        }
        else if (edit.ArtworkChanged && edit.ArtworkBytes is { Length: > 0 } bytes)
        {
            tag.Pictures =
            [
                new Picture
                {
                    Type = PictureType.FrontCover,
                    MimeType = GuessMime(bytes),
                    Description = "Cover",
                    Data = new ByteVector(bytes)
                }
            ];
        }

        file.Save();
    }

    private async Task<long> ReindexAsync(
        string path,
        TagEdit edit,
        TagEditScope scope,
        bool updateTrackArtists,
        long? sourceAlbumId,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        // Prefer the edit we just wrote so index never lags a flaky tag readback.
        var read = TagReader.Read(path, info.Length, info.LastWriteTimeUtc.Ticks);

        var albumName = edit.Album;
        var albumArtist = edit.AlbumArtist;
        var artistName = scope == TagEditScope.Track || updateTrackArtists
            ? (scope == TagEditScope.Track ? edit.Artist : edit.AlbumArtist)
            : (string.IsNullOrWhiteSpace(read.Artist) ? albumArtist : read.Artist);
        var title = scope == TagEditScope.Track ? edit.Title : read.Title;
        var trackNumber = scope == TagEditScope.Track ? edit.TrackNumber : read.TrackNumber;
        var discNumber = scope == TagEditScope.Track ? edit.DiscNumber : read.DiscNumber;
        var year = edit.Year;
        var genre = edit.Genre;

        byte[]? art = null;
        if (edit.RemoveArtwork)
        {
            art = null;
        }
        else if (edit.ArtworkChanged && edit.ArtworkBytes is { Length: > 0 })
        {
            art = edit.ArtworkBytes;
        }
        else
        {
            art = TagReader.ReadArtwork(path);
        }

        var trackFolder = ArtworkCache.NormalizeFolder(Path.GetDirectoryName(path));
        var albumKey = ArtworkCache.AlbumKey(albumArtist, albumName, trackFolder);
        var artworkPath = edit.RemoveArtwork
            ? null
            : await ArtworkCache.ReplaceThumbnailAsync(
                albumKey,
                art,
                trackFolder,
                cancellationToken).ConfigureAwait(false);

        var artistId = _db.UpsertArtist(artistName);
        // Also ensure the album-artist row exists so Artists navigation can find it.
        _db.UpsertArtist(albumArtist);

        long albumId;
        if (scope == TagEditScope.Album && sourceAlbumId is long existingAlbumId)
        {
            albumId = _db.UpdateAlbumIdentity(existingAlbumId, albumName, albumArtist, year, artworkPath);
        }
        else
        {
            albumId = _db.UpsertAlbum(albumName, albumArtist, year, artworkPath, trackFolder);
        }

        _db.SetAlbumArtwork(albumId, edit.RemoveArtwork ? null : artworkPath);

        _db.UpsertTrack(new TrackInfo
        {
            Path = path,
            FileSize = info.Length,
            ModifiedUtcTicks = info.LastWriteTimeUtc.Ticks,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(path) : title,
            Artist = artistName,
            Album = albumName,
            AlbumArtist = albumArtist,
            TrackNumber = trackNumber,
            DiscNumber = discNumber,
            Year = year,
            Genre = genre,
            DurationSeconds = read.DurationSeconds,
            AlbumId = albumId,
            ArtistId = artistId
        }, artistId, albumId);

        return albumId;
    }

    private static string GuessMime(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        return "image/jpeg";
    }
}
