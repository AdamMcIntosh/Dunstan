using System.Text.Json;
using Dustan.Models;
using Dustan.Playback;
using Dustan.Services;

namespace Dustan.Bridge;

/// <summary>Maps bridge methods onto the existing Dustan.Core services.</summary>
public sealed class DustanApi : IDisposable
{
    private readonly BridgeHost _host;
    private readonly AppServices _app;
    private readonly LibraryDb _reader;
    private readonly SemaphoreSlim _readGate = new(1);
    private readonly object _playerGate = new();
    private readonly string? _engineError;
    private readonly Timer _ticker;
    private readonly object _videoScanGate = new();
    private CancellationTokenSource? _videoScan;
    private object? _lastVideoScan;
    private object? _lastMusicScan;
    private bool _disposed;

    public DustanApi(BridgeHost host, AppServices app, string? engineError)
    {
        _host = host;
        _app = app;
        _engineError = engineError;
        _reader = LibraryDb.OpenReader(AppPaths.DatabasePath);

        Register();
        app.Playback.StateChanged += (_, _) => EmitPlayer();
        app.Playback.CurrentTrackChanged += (_, _) => EmitPlayer();
        app.Playback.TrackFailed += (_, track) => _host.Emit("trackFailed", new { title = track.Title, path = track.Path });
        app.Scanner.ProgressChanged += (_, p) =>
        {
            var data = new { seen = p.FilesSeen, tagged = p.FilesTagged, skipped = p.FilesSkipped, removed = p.FilesRemoved,
                current = p.CurrentPath, complete = p.IsComplete, error = p.Error };
            _lastMusicScan = data;
            _host.Emit("musicScan", data);
            if (p.IsComplete) _host.Emit("libraryChanged", new { kind = "music" });
        };
        LibraryEvents.Changed += (_, _) => _host.Emit("libraryChanged", new { kind = "music" });
        _ticker = new Timer(_ =>
        {
            lock (_playerGate)
            {
                if (!_disposed && _app.Playback.IsPlaying)
                    _host.Emit("playerTime", new { position = _app.Playback.Position.TotalSeconds, duration = _app.Playback.Duration.TotalSeconds });
            }
        }, null, 500, 500);
    }

    private void Register()
    {
        _host.Register("app.info", _ => Info());
        _host.Register("app.setMusicPath", p =>
        {
            var path = p.Str("path").Trim();
            if (path.Length == 0) throw new BridgeException("Enter a mounted folder or UNC path.");
            _app.Settings.SetLibraryPath(path);
            _app.Playback.SetLibraryRoot(_app.Settings.Current.LibraryPath);
            return _app.Settings.Current.LibraryPath;
        });

        // Music library
        _host.Register("music.albums", p => Read(db => SortAlbums(db.GetAlbums(Search(p)), p.Str("sort", "artist")).Select(AlbumDto).ToList()));
        _host.Register("music.album", p => Read(db =>
        {
            var album = db.GetAlbum(p.Long("id")) ?? throw new BridgeException("This album is no longer in the library.");
            var tracks = db.GetTracksForAlbum(album.Id);
            return new { album = AlbumDto(album), tracks = tracks.Select(t => TrackDto(t, album.ArtworkPath)).ToList(),
                duration = tracks.Sum(t => t.DurationSeconds), genre = tracks.Select(t => t.Genre).FirstOrDefault(g => g.Length > 0) ?? "",
                discs = tracks.Select(t => t.DiscNumber).Distinct().Count() };
        }));
        _host.Register("music.artists", p => Read(db =>
        {
            var art = ArtistArtwork(db);
            return db.GetArtists(Search(p)).Select(a => ArtistDto(a, art)).ToList();
        }));
        _host.Register("music.artist", p => Read(db =>
        {
            var artist = db.GetArtist(p.Long("id")) ?? throw new BridgeException("This artist is no longer in the library.");
            var albums = db.GetAlbumsForArtist(artist.Id);
            var art = albums.ToDictionary(a => a.Id, a => a.ArtworkPath);
            var tracks = db.GetTracksForArtist(artist.Id);
            return new { artist = ArtistDto(artist, ArtistArtwork(db)), albums = albums.OrderByDescending(a => a.Year).Select(AlbumDto).ToList(),
                tracks = tracks.Select(t => TrackDto(t, t.AlbumId is long id && art.TryGetValue(id, out var a) ? a : null)).ToList(),
                duration = tracks.Sum(t => t.DurationSeconds) };
        }));
        _host.Register("music.tracks", p => Read(db =>
        {
            var art = db.GetAlbums().ToDictionary(a => a.Id, a => a.ArtworkPath);
            return db.GetTracks(Search(p)).Select(t => TrackDto(t, t.AlbumId is long id && art.TryGetValue(id, out var a) ? a : null)).ToList();
        }));
        _host.Register("music.cover", p => Read(db =>
        {
            var album = db.GetAlbum(p.Long("id"));
            return album is null ? null : CoverArt.HighResolution(album, () => db.GetTracksForAlbum(album.Id).FirstOrDefault()?.Path);
        }));
        _host.Register("music.play", p => PlayMusic(p));
        _host.Register("music.scan", _ => StartMusicScan());
        _host.Register("music.cancelScan", _ => { _app.Scanner.Cancel(); return true; });
        _host.Register("music.editTrack", p => EditTagsAsync(p, TagEditScope.Track));
        _host.Register("music.editAlbum", p => EditTagsAsync(p, TagEditScope.Album));

        // Music playback
        _host.Register("player.state", _ => PlayerState());
        _host.Register("player.queue", _ =>
        {
            var order = _app.Playback.PlayOrder;
            var art = Read(db => db.GetAlbums().ToDictionary(a => a.Id, a => a.ArtworkPath));
            return new { index = _app.Playback.PlayOrderIndex,
                tracks = order.Select(t => TrackDto(t, t.AlbumId is long id && art.TryGetValue(id, out var a) ? a : null)).ToList() };
        });
        _host.Register("player.command", p =>
        {
            lock (_playerGate)
            {
                if (_disposed) throw new BridgeException("Dustan is shutting down.");
                switch (p.Str("action"))
                {
                    case "toggle": _app.Playback.TogglePlayPause(); break;
                    case "play": _app.Playback.Play(); break;
                    case "pause": _app.Playback.Pause(); break;
                    case "next": _app.Playback.Next(); break;
                    case "previous": _app.Playback.Previous(); break;
                    case "shuffle": _app.Playback.ToggleShuffle(); break;
                    case "repeat": _app.Playback.CycleRepeat(); break;
                    default: throw new BridgeException("Unknown player action.");
                }
            }
            return PlayerState();
        });
        _host.Register("player.seek", p => Player(pb => pb.Seek(TimeSpan.FromSeconds(p.Num("seconds")))));
        _host.Register("player.volume", p => Player(pb => pb.SetVolume(p.Num("value", 0.8))));
        _host.Register("player.jump", p => Player(pb => pb.JumpTo((int)p.Long("index", -1))));

        // Video catalog
        _host.Register("video.overview", p => VideoOverview(p));
        _host.Register("video.show", p =>
        {
            var key = p.Str("key");
            var show = Shows().FirstOrDefault(s => s.Key == key) ?? throw new BridgeException("This title is no longer in the video library.");
            return new { show = ShowDto(show), seasons = show.Seasons.Select(s => new { number = s.Number, title = s.Title,
                episodes = s.Episodes.Select(e => EpisodeDto(e, show)).ToList() }).ToList() };
        });
        _host.Register("video.after", p =>
        {
            var path = p.Str("path");
            foreach (var show in Shows())
            {
                var episodes = show.Episodes.ToList();
                var index = episodes.FindIndex(e => PathSafety.Comparer.Equals(e.Video.Path, path));
                if (index >= 0) return index + 1 < episodes.Count ? EpisodeDto(episodes[index + 1], show) : null;
            }
            return null;
        });
        _host.Register("video.item", p =>
        {
            var path = p.Str("path");
            foreach (var show in Shows())
                foreach (var e in show.Episodes)
                    if (PathSafety.Comparer.Equals(e.Video.Path, path)) return EpisodeDto(e, show);
            return null;
        });
        _host.Register("video.progress", p =>
        {
            _app.Videos.SaveProgress(p.Str("path"), p.Num("seconds"), p.Bool("completed"));
            return true;
        });
        _host.Register("video.setWatched", p =>
        {
            var paths = p.Strings("paths").ToList();
            if (p.Has("showKey"))
            {
                var show = Shows().FirstOrDefault(s => s.Key == p.Str("showKey"));
                if (show is not null) paths.AddRange(show.Episodes.Select(e => e.Video.Path));
            }
            _app.Videos.SetWatched(paths, p.Bool("watched", true));
            _host.Emit("libraryChanged", new { kind = "video" });
            return true;
        });
        _host.Register("video.scan", p => StartVideoScan(p.Str("path", _app.Videos.Root)));
        _host.Register("video.cancelScan", _ => { lock (_videoScanGate) _videoScan?.Cancel(); return true; });
        _host.Register("video.pauseMusic", _ => Player(pb => pb.Pause()));

        // TV channels
        _host.Register("tv.channels", _ => Channels());
        _host.Register("tv.guide", p => Guide(p));
        _host.Register("tv.tune", p => Tune(p.Str("id")));
        _host.Register("tv.sources", _ => ChannelSources());
        _host.Register("tv.create", p => CreateChannel(p));
        _host.Register("tv.remove", p =>
        {
            _app.Videos.RemoveChannel(p.Str("id"));
            _host.Emit("libraryChanged", new { kind = "tv" });
            return true;
        });
    }

    private bool Player(Action<PlaybackService> action)
    {
        lock (_playerGate)
        {
            if (_disposed) throw new BridgeException("Dustan is shutting down.");
            action(_app.Playback);
        }
        return true;
    }

    // ---- app ----

    private object Info()
    {
        var videos = _app.Videos.Videos;
        var counts = Read(db => new { tracks = db.GetTrackCount(), albums = db.GetAlbums().Count, artists = db.GetArtists().Count });
        return new
        {
            version = typeof(DustanApi).Assembly.GetName().Version?.ToString(3),
            dataDirectory = AppPaths.DataDirectory,
            musicRoot = _app.Settings.Current.LibraryPath,
            musicRootAvailable = Directory.Exists(_app.Settings.Current.LibraryPath),
            videoRoot = _app.Videos.Root,
            videoRootAvailable = _app.Videos.Root.Length > 0 && Directory.Exists(_app.Videos.Root),
            counts = new { counts.tracks, counts.albums, counts.artists, videos = videos.Count,
                unknownDurations = videos.Count(v => v.DurationSeconds < 1), channels = _app.Videos.Channels.Count },
            videoLoadError = _app.Videos.LoadError,
            musicEngineError = _engineError,
            ffprobe = Tools.Find("ffprobe"),
            musicScanning = _app.Scanner.IsScanning,
            videoScanning = _videoScan is not null,
            lastMusicScan = _lastMusicScan,
            lastVideoScan = _lastVideoScan,
        };
    }

    // ---- music ----

    private static string? Search(JsonElement p) => p.Str("search") is { Length: > 0 } s ? s : null;

    private T Read<T>(Func<LibraryDb, T> query)
    {
        _readGate.Wait();
        try { return query(_reader); }
        finally { _readGate.Release(); }
    }

    private static IEnumerable<AlbumInfo> SortAlbums(IEnumerable<AlbumInfo> albums, string sort) => sort switch
    {
        "title" => albums.OrderBy(a => a.Name, NaturalComparer.Instance),
        "year" => albums.OrderByDescending(a => a.Year).ThenBy(a => a.AlbumArtist, StringComparer.OrdinalIgnoreCase),
        "recent" => albums.OrderByDescending(a => a.Id),
        _ => albums,
    };

    private static Dictionary<string, string?> ArtistArtwork(LibraryDb db) =>
        db.GetAlbums().Where(a => a.ArtworkPath is not null)
            .GroupBy(a => a.AlbumArtist, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Year).First().ArtworkPath, StringComparer.OrdinalIgnoreCase);

    private static object AlbumDto(AlbumInfo a) => new
    {
        id = a.Id, name = a.Name, artist = a.AlbumArtist, year = a.Year, trackCount = a.TrackCount, art = ExistingFile(a.ArtworkPath),
    };

    private static object ArtistDto(ArtistInfo a, Dictionary<string, string?> art) => new
    {
        id = a.Id, name = a.Name, albumCount = a.AlbumCount, trackCount = a.TrackCount,
        art = art.TryGetValue(a.Name, out var path) ? ExistingFile(path) : null,
    };

    private static object TrackDto(TrackInfo t, string? art) => new
    {
        id = t.Id, title = t.Title, artist = t.Artist, album = t.Album, albumArtist = t.AlbumArtist, albumId = t.AlbumId,
        artistId = t.ArtistId, trackNumber = t.TrackNumber, discNumber = t.DiscNumber, year = t.Year, genre = t.Genre,
        duration = t.DurationSeconds, file = Path.GetFileName(t.Path), art = ExistingFile(art),
    };

    private static string? ExistingFile(string? path) => !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;

    private object PlayMusic(JsonElement p)
    {
        if (_engineError is not null) throw new BridgeException(_engineError);
        var tracks = Read(db => p.Str("source") switch
        {
            "album" => db.GetTracksForAlbum(p.Long("id")),
            "artist" => db.GetTracksForArtist(p.Long("id")),
            "tracks" => db.GetTracks(Search(p)),
            _ => throw new BridgeException("Unknown music source."),
        });
        if (tracks.Count == 0) throw new BridgeException("There are no tracks to play.");
        var start = 0;
        if (p.Has("trackId"))
        {
            var id = p.Long("trackId");
            start = Math.Max(0, tracks.ToList().FindIndex(t => t.Id == id));
        }
        lock (_playerGate)
        {
            if (_disposed) throw new BridgeException("Dustan is shutting down.");
            var shuffle = p.Bool("shuffle");
            if (shuffle != _app.Playback.IsShuffleEnabled && p.Has("shuffle")) _app.Playback.ToggleShuffle();
            if (shuffle && !p.Has("trackId")) start = Random.Shared.Next(tracks.Count);
            _app.Playback.PlayList(tracks, start);
        }
        return PlayerState();
    }

    private object StartMusicScan()
    {
        var root = _app.Settings.Current.LibraryPath;
        if (string.IsNullOrWhiteSpace(root)) throw new BridgeException("Choose your music folder first.");
        if (!Directory.Exists(root)) throw new BridgeException($"The music folder {root} is unavailable. Check that the NAS share is mounted.");
        _app.Playback.SetLibraryRoot(root);
        var task = _app.Scanner.StartScanAsync(root);
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted) _host.Emit("musicScan", new { complete = true, error = t.Exception?.GetBaseException().Message });
        }, TaskScheduler.Default);
        return true;
    }

    private async Task<object?> EditTagsAsync(JsonElement p, TagEditScope scope)
    {
        var albumId = p.Long("albumId");
        var tracks = Read(db => db.GetTracksForAlbum(albumId));
        if (scope == TagEditScope.Track)
        {
            var id = p.Long("trackId");
            tracks = tracks.Where(t => t.Id == id).ToList();
            if (tracks.Count == 0) tracks = Read(db => db.GetTracks()).Where(t => t.Id == id).ToList();
        }
        if (tracks.Count == 0) throw new BridgeException("The selected music is no longer in the library.");
        var first = tracks[0];
        var artworkFile = p.Str("artworkFile");
        var edit = new TagEdit
        {
            Title = p.Str("title", first.Title), Artist = p.Str("artist", first.Artist), Album = p.Str("album", first.Album),
            AlbumArtist = p.Str("albumArtist", first.AlbumArtist), TrackNumber = (int)p.Long("trackNumber", first.TrackNumber),
            DiscNumber = (int)p.Long("discNumber", first.DiscNumber), Year = (int)p.Long("year", first.Year), Genre = p.Str("genre", first.Genre),
            ArtworkBytes = artworkFile.Length > 0 ? await File.ReadAllBytesAsync(artworkFile) : null,
            ArtworkChanged = artworkFile.Length > 0, RemoveArtwork = p.Bool("removeArtwork"),
        };
        var result = scope == TagEditScope.Album ? await _app.Tags.ApplyAlbumAsync(tracks, edit) : await _app.Tags.ApplyTrackAsync(first, edit);
        _host.Emit("libraryChanged", new { kind = "music" });
        return new { succeeded = result.Succeeded, failed = result.Failed, errors = result.Errors, albumId = result.AlbumId };
    }

    private object PlayerState()
    {
        var playback = _app.Playback;
        var track = playback.CurrentTrack;
        string? art = null;
        if (track?.AlbumId is long albumId) art = Read(db => db.GetAlbum(albumId)?.ArtworkPath);
        // Every LibVLC read goes through the player gate so shutdown cannot release the player mid-call.
        lock (_playerGate)
        {
            if (_disposed) throw new BridgeException("Dustan is shutting down.");
            return PlayerStateUnlocked(playback, track, art);
        }
    }

    private object PlayerStateUnlocked(PlaybackService playback, TrackInfo? track, string? art)
    {
        return new
        {
            track = track is null ? null : TrackDto(track, art),
            playing = playback.IsPlaying, position = playback.Position.TotalSeconds, duration = playback.Duration.TotalSeconds,
            volume = playback.Volume, shuffle = playback.IsShuffleEnabled,
            repeat = playback.Repeat switch { RepeatMode.Album => "all", RepeatMode.One => "one", _ => "off" },
            index = playback.PlayOrderIndex, queueLength = playback.Queue.Count, engineError = _engineError,
        };
    }

    private void EmitPlayer()
    {
        if (_disposed) return;
        try { _host.Emit("player", PlayerState()); }
        catch (Exception ex) { Console.Error.WriteLine("player state: " + ex.Message); }
    }

    // ---- video ----

    private IReadOnlyList<VideoShow> Shows() =>
        VideoCatalog.Group(_app.Videos.Videos, Path.GetFileName(_app.Videos.Root.TrimEnd('/', '\\')));

    private static long? Ms(DateTimeOffset? value) => value?.ToUnixTimeMilliseconds();

    private static object EpisodeDto(VideoEpisode e, VideoShow show) => new
    {
        path = e.Video.Path, name = e.Name, code = e.Code, season = e.Season, episode = e.Episode, fileTitle = e.Video.Title,
        duration = e.Video.DurationSeconds, position = e.Video.PositionSeconds, watched = e.Video.Watched,
        poster = e.Video.PosterPath, folder = e.Video.Folder, lastPlayed = Ms(e.Video.LastPlayedUtc),
        showKey = show.Key, showTitle = show.Title, isMovie = show.IsMovie, collection = show.Collection,
    };

    private static object ShowDto(VideoShow s)
    {
        var episodes = s.Episodes.ToList();
        var next = s.NextUp;
        return new
        {
            key = s.Key, title = s.Title, collection = s.Collection, isMovie = s.IsMovie,
            episodeCount = episodes.Count, seasonCount = s.Seasons.Count(x => x.Number > 0),
            watchedCount = episodes.Count(e => e.Video.Watched), duration = episodes.Sum(e => e.Video.DurationSeconds),
            poster = episodes.Select(e => e.Video.PosterPath).FirstOrDefault(p => p is not null),
            thumbPath = episodes[0].Video.Path, lastPlayed = Ms(episodes.Max(e => e.Video.LastPlayedUtc)),
            nextUp = EpisodeDto(next, s),
        };
    }

    private object VideoOverview(JsonElement p)
    {
        var search = p.Str("search").Trim();
        var filter = p.Str("filter", "all");
        var shows = Shows();
        var continueWatching = shows
            .SelectMany(s => s.Episodes.Select(e => (Show: s, Episode: e)))
            .Where(x => x.Episode.Video.PositionSeconds > 0 && !x.Episode.Video.Watched)
            .OrderByDescending(x => x.Episode.Video.LastPlayedUtc ?? DateTimeOffset.MinValue)
            .Take(24)
            .Select(x => EpisodeDto(x.Episode, x.Show)).ToList();
        IEnumerable<VideoShow> titles = shows;
        if (search.Length > 0)
            titles = titles.Where(s => s.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || s.Collection.Contains(search, StringComparison.OrdinalIgnoreCase)
                || s.Episodes.Any(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
        titles = filter switch
        {
            "shows" => titles.Where(s => !s.IsMovie),
            "movies" => titles.Where(s => s.IsMovie),
            "unwatched" => titles.Where(s => s.Episodes.Any(e => !e.Video.Watched)),
            "progress" => titles.Where(s => s.Episodes.Any(e => e.Video.Watched || e.Video.PositionSeconds > 0) && s.Episodes.Any(e => !e.Video.Watched)),
            "watched" => titles.Where(s => s.Episodes.All(e => e.Video.Watched)),
            _ => titles,
        };
        var list = titles.Select(ShowDto).ToList();
        return new
        {
            root = _app.Videos.Root, loadError = _app.Videos.LoadError, scanning = _videoScan is not null,
            counts = new { videos = _app.Videos.Videos.Count, shows = shows.Count(s => !s.IsMovie), movies = shows.Count(s => s.IsMovie) },
            continueWatching, titles = list,
        };
    }

    private object StartVideoScan(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new BridgeException("Choose your video folder first.");
        if (Tools.Find("ffprobe") is null)
            throw new BridgeException("Video scanning needs FFmpeg's ffprobe. Install FFmpeg (including ffprobe), then scan again.");
        CancellationTokenSource cancellation;
        lock (_videoScanGate)
        {
            if (_videoScan is not null) throw new BridgeException("A video scan is already running.");
            _videoScan = cancellation = new CancellationTokenSource();
        }
        _ = Task.Run(async () =>
        {
            var lastReport = DateTime.MinValue;
            var progress = new Progress<string>(message =>
            {
                if ((DateTime.UtcNow - lastReport).TotalMilliseconds < 120) return;
                lastReport = DateTime.UtcNow;
                _host.Emit("videoScan", new { state = "running", message });
            });
            object result;
            try
            {
                using var probe = new VideoProbe(Tools.Find("ffprobe"));
                var count = await _app.Videos.ScanAsync(root, probe.GetDurationAsync, progress, cancellation.Token);
                var unknown = _app.Videos.Videos.Count(v => v.DurationSeconds < 1);
                result = new { state = "done", count, unknown,
                    message = $"{count} videos indexed." + (unknown > 0 ? $" {unknown} have unknown durations and can't be scheduled on TV until a rescan reads them." : "") };
            }
            catch (OperationCanceledException) { result = new { state = "cancelled", message = "Scan cancelled. Your previous video library was kept." }; }
            catch (Exception ex) { result = new { state = "failed", message = ex.Message }; }
            finally
            {
                lock (_videoScanGate) { _videoScan = null; }
                cancellation.Dispose();
            }
            _lastVideoScan = result;
            _host.Emit("videoScan", result);
            _host.Emit("libraryChanged", new { kind = "video" });
        });
        return true;
    }

    // ---- TV ----

    private Dictionary<string, (string Title, string Subtitle)> ProgrammeNames()
    {
        var names = new Dictionary<string, (string, string)>(PathSafety.Comparer);
        foreach (var show in Shows())
            foreach (var e in show.Episodes)
                names[e.Video.Path] = show.IsMovie ? (show.Title, show.Collection) : (show.Title, string.IsNullOrEmpty(e.Code) ? e.Name : $"{e.Code} · {e.Name}");
        return names;
    }

    private static object ProgrammeDto(TvProgramme p, Dictionary<string, (string Title, string Subtitle)> names, DateTimeOffset? now = null)
    {
        var (title, subtitle) = names.TryGetValue(p.Video.Path, out var n) ? n : (VideoCatalog.CleanTitle(p.Video.Title), p.Video.Folder);
        return new
        {
            path = p.Video.Path, title, subtitle, start = p.Start.ToUnixTimeMilliseconds(), end = p.End.ToUnixTimeMilliseconds(),
            offset = now is { } at ? p.OffsetAt(at).TotalSeconds : 0, available = File.Exists(p.Video.Path),
        };
    }

    private object Channels()
    {
        var now = DateTimeOffset.UtcNow;
        var names = ProgrammeNames();
        return _app.Videos.Channels.Select(c =>
        {
            var upcoming = TvSchedule.GetGuide(c, now, TimeSpan.FromHours(4));
            return new
            {
                id = c.Id, name = c.Name, epoch = c.Epoch.ToUnixTimeMilliseconds(), videoCount = c.Videos.Count,
                cycle = c.Videos.Sum(v => v.DurationSeconds),
                now = upcoming.Count > 0 ? ProgrammeDto(upcoming[0], names, now) : null,
                next = upcoming.Count > 1 ? ProgrammeDto(upcoming[1], names) : null,
            };
        }).ToList();
    }

    private object Guide(JsonElement p)
    {
        var from = p.Has("from") ? DateTimeOffset.FromUnixTimeMilliseconds(p.Long("from")) : DateTimeOffset.UtcNow;
        var window = TimeSpan.FromHours(Math.Clamp(p.Num("hours", 6), 0.5, 48));
        var names = ProgrammeNames();
        return new
        {
            from = from.ToUnixTimeMilliseconds(), until = (from + window).ToUnixTimeMilliseconds(),
            channels = _app.Videos.Channels.Select(c => new
            {
                id = c.Id, name = c.Name, videoCount = c.Videos.Count,
                programmes = TvSchedule.GetGuide(c, from, window).Select(x => ProgrammeDto(x, names)).ToList(),
            }).ToList(),
        };
    }

    private object Tune(string id)
    {
        var channel = _app.Videos.Channels.FirstOrDefault(c => c.Id == id) ?? throw new BridgeException("This channel was removed.");
        var now = DateTimeOffset.UtcNow;
        var programme = TvSchedule.Now(channel, now) ?? throw new BridgeException("This channel has no playable schedule.");
        return new { id = channel.Id, name = channel.Name, programme = ProgrammeDto(programme, ProgrammeNames(), now) };
    }

    private const char UnitSeparator = '\u001f';

    private object ChannelSources() => Shows().Select(s => new
    {
        key = s.Key, title = s.Title, collection = s.Collection, isMovie = s.IsMovie,
        seasons = s.Seasons.Select(season => new
        {
            key = s.Key + UnitSeparator + season.Number, title = s.IsMovie ? "Film" : season.Title,
            count = season.Episodes.Count(e => e.Video.DurationSeconds >= 1),
            unknown = season.Episodes.Count(e => e.Video.DurationSeconds < 1),
            duration = season.Episodes.Sum(e => e.Video.DurationSeconds),
        }).Where(x => x.count > 0).ToList(),
    }).Where(s => s.seasons.Count > 0).ToList();

    private object CreateChannel(JsonElement p)
    {
        var units = p.Strings("units");
        var shows = Shows().ToDictionary(s => s.Key);
        var videos = new List<VideoInfo>();
        foreach (var unit in units)
        {
            var split = unit.LastIndexOf(UnitSeparator);
            if (split < 0 || !shows.TryGetValue(unit[..split], out var show)) continue;
            var number = int.Parse(unit[(split + 1)..]);
            videos.AddRange(show.Seasons.Where(s => s.Number == number).SelectMany(s => s.Episodes)
                .Select(e => e.Video).Where(v => v.DurationSeconds >= 1));
        }
        if (p.Bool("shuffle")) videos = videos.OrderBy(_ => Random.Shared.Next()).ToList();
        var channel = _app.Videos.AddChannel(p.Str("name"), videos, DateTimeOffset.UtcNow);
        _host.Emit("libraryChanged", new { kind = "tv" });
        return new { id = channel.Id, name = channel.Name, videoCount = channel.Videos.Count };
    }

    public void Dispose()
    {
        using (var stopped = new ManualResetEvent(false))
        {
            if (_ticker.Dispose(stopped)) stopped.WaitOne(TimeSpan.FromSeconds(2));
        }
        lock (_videoScanGate) _videoScan?.Cancel();
        _app.Scanner.Cancel();
        lock (_playerGate)
        {
            _disposed = true;
            _app.Playback.Dispose();
        }
        _reader.Dispose();
    }
}

internal static class Tools
{
    /// <summary>Finds a helper executable beside the bridge, beside the app (one folder up) or on PATH.</summary>
    public static string? Find(string name)
    {
        var file = OperatingSystem.IsWindows() ? name + ".exe" : name;
        foreach (var dir in new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "..") })
        {
            var bundled = Path.GetFullPath(Path.Combine(dir, file));
            if (File.Exists(bundled)) return bundled;
        }
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, file);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
