using System.Diagnostics;
using System.Text.Json;
using Dustan.Bridge;
using Dustan.Models;
using Dustan.Services;

/// <summary>Checks for the Qt app's service layer: catalog grouping, new library APIs and the bridge protocol.</summary>
static class QtBridgeTests
{
    public static async Task RunAsync(Action<bool, string> check, string temp)
    {
        CatalogGrouping(check);
        LibraryAdditions(check, temp);
        PlaybackOrder(check, temp);
        ArtistCredits(check, temp);
        await ProtocolAsync(check);
        await BridgeProcessAsync(check, temp);
    }

    static VideoInfo Video(string folder, string title, double duration = 1200, string ext = ".mkv") => new()
    {
        Path = "/videos/" + folder + "/" + title + ext, Folder = folder, Title = title, DurationSeconds = duration,
    };

    static void CatalogGrouping(Action<bool, string> check)
    {
        var videos = new List<VideoInfo>
        {
            Video("TV SHOWS/Deep Space/Star.Trek.DS9.HQ.x264.ac3.5.1-Season.7-MEECH", "Star Trek DS9-s07e09 Covenant"),
            Video("TV SHOWS/Deep Space/Star.Trek.DS9.HQ.x264.ac3.5.1-Season.1-MEECH", "Star Trek DS9-s01e01&e02 Emissary"),
            Video("TV SHOWS/Show Name/Season 2", "Show s02e10 Later"),
            Video("TV SHOWS/Show Name/Season 2", "Show s02e02 Earlier"),
            Video("TV SHOWS/Show Name/Specials", "Show Behind the Scenes"),
            Video("TV SHOWS/Loose Show", "Loose Show S01E03 Third"),
            Video("TV SHOWS/Loose Show", "Loose Show S02E01 Next Season"),
            Video("Movies/GODFATHER1_RESTORED", "A1 t00"),
            Video("Movies/The Film (1999)", "The Film 1999 1080p BluRay x264", 6000),
            Video("Movies", "Loose Film"),
        };
        var shows = VideoCatalog.Group(videos, "videos");
        var ds9 = shows.Single(s => s.Title == "Deep Space");
        check(ds9.Seasons.Select(s => s.Number).SequenceEqual([1, 7]), "Release-style season folders become numbered seasons");
        check(ds9.Seasons[0].Episodes[0].Name == "Emissary" && ds9.Seasons[0].Episodes[0].Code == "S1 · E1", "Double-episode codes are stripped from episode names");
        var show = shows.Single(s => s.Title == "Show Name");
        check(show.Seasons[0].Number == 2 && show.Seasons[^1].Title == "Specials & extras", "Specials folder sorts after numbered seasons");
        check(show.Seasons[0].Episodes.Select(e => e.Episode).SequenceEqual([2, 10]), "Episodes order numerically, not alphabetically");
        var loose = shows.Single(s => s.Title == "Loose Show");
        check(loose.Seasons.Select(s => s.Number).SequenceEqual([1, 2]) && !loose.IsMovie, "Seasons come from SxxEyy when there are no season folders");
        check(shows.Single(s => s.Key.EndsWith("A1 t00.mkv")).Title == "Godfather1 Restored", "Disc-rip titles fall back to the folder name");
        check(shows.Any(s => s.IsMovie && s.Title == "The Film 1999"), "Release tags are removed from film titles");
        check(shows.Any(s => s.IsMovie && s.Title == "Loose Film" && s.Collection == "Movies"), "Loose files in a collection are films");
        check(videos.Count == shows.Sum(s => s.Episodes.Count()), "Grouping keeps every video exactly once");

        var watched = videos[3] with { Watched = true, LastPlayedUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        var regrouped = VideoCatalog.Group([watched, videos[2]], "videos").Single();
        check(regrouped.NextUp.Video.Path == videos[2].Path, "Next up follows the most recently finished episode");
        var resuming = VideoCatalog.Group([videos[3] with { PositionSeconds = 30, LastPlayedUtc = DateTimeOffset.UtcNow }, videos[2]], "videos").Single();
        check(resuming.NextUp.Video.Path == videos[3].Path, "Next up resumes an episode in progress");
        check(new NaturalComparer().Compare("Episode 2", "Episode 10") < 0, "Natural comparer orders embedded numbers");
    }

    static void LibraryAdditions(Action<bool, string> check, string temp)
    {
        var state = Path.Combine(temp, "qt-videos.json");
        // A catalog written by the previous release has no LastPlayedUtc field.
        File.WriteAllText(state, """{"Root":"/videos","Videos":[{"Path":"/videos/a.mkv","Title":"a","DurationSeconds":100,"PositionSeconds":12},{"Path":"/videos/b.mkv","Title":"b","DurationSeconds":100}],"Channels":[]}""");
        var library = new VideoLibrary(state);
        check(library.LoadError is null && library.Videos[0].LastPlayedUtc is null && library.Videos[0].PositionSeconds == 12, "Catalogs from earlier versions load unchanged");
        library.SaveProgress("/videos/a.mkv", 40);
        check(new VideoLibrary(state).Videos[0].LastPlayedUtc is not null, "Saving progress records when the video was last played");
        library.SetWatched(["/videos/a.mkv", "/videos/b.mkv"], true);
        var reloaded = new VideoLibrary(state).Videos;
        check(reloaded.All(v => v.Watched && v.PositionSeconds == 0), "Batch mark-watched updates every video and clears resume points");
        library.SetWatched(["/videos/b.mkv"], false);
        check(!new VideoLibrary(state).Videos[1].Watched && new VideoLibrary(state).Videos[0].Watched, "Mark unwatched only touches the given videos");
    }

    static void PlaybackOrder(Action<bool, string> check, string temp)
    {
        var root = Path.Combine(temp, "media");
        Directory.CreateDirectory(root);
        var tracks = Enumerable.Range(1, 6).Select(i => new TrackInfo { Id = i, Path = Path.Combine(root, $"{i}.mp3"), Title = $"T{i}" }).ToList();
        using var engine = new RecordingEngine();
        using var playback = new PlaybackService(engine);
        playback.SetLibraryRoot(root);
        playback.PlayList(tracks, 2);
        check(playback.PlayOrderIndex == 2 && playback.PlayOrder[2].Id == 3, "Play order reports the current position");
        playback.ToggleShuffle();
        check(playback.PlayOrder.Select(t => t.Id).OrderBy(i => i).SequenceEqual([1, 2, 3, 4, 5, 6]) && playback.PlayOrder[playback.PlayOrderIndex].Id == 3,
            "Shuffled play order keeps every track and the current one");
        playback.JumpTo(4);
        SpinWait.SpinUntil(() => engine.LastPath == playback.PlayOrder[4].Path, 2000);
        check(engine.LastPath == playback.PlayOrder[4].Path && playback.PlayOrderIndex == 4, "Jumping within the queue plays that track");
    }

    static void ArtistCredits(Action<bool, string> check, string temp)
    {
        using var db = new LibraryDb(Path.Combine(temp, "artists.db"));
        db.Initialize();
        var performer = db.UpsertArtist("Guest Singer");
        var band = db.UpsertArtist("The Band");
        var album = db.UpsertAlbum("Live", "The Band", 2001, null, Path.Combine(temp, "live"));
        db.UpsertTrack(new TrackInfo { Path = Path.Combine(temp, "live", "1.flac"), Title = "One", Artist = "The Band", Album = "Live", AlbumArtist = "The Band" }, band, album);
        db.UpsertTrack(new TrackInfo { Path = Path.Combine(temp, "live", "2.flac"), Title = "Two", Artist = "Guest Singer", Album = "Live", AlbumArtist = "The Band" }, performer, album);
        var artists = db.GetArtists().ToDictionary(a => a.Name);
        check(artists["The Band"].AlbumCount == 1 && artists["The Band"].TrackCount == 2, "Album artists are credited with every track on their albums");
        check(artists["Guest Singer"].AlbumCount == 1 && artists["Guest Singer"].TrackCount == 1, "Track artists are credited with their own tracks and albums");
        check(db.GetArtists("guest").Single().Name == "Guest Singer", "Artist search still filters by name");
    }

    static async Task ProtocolAsync(Action<bool, string> check)
    {
        var host = new BridgeHost(Stream.Null, Stream.Null);
        host.Register("echo", p => p.GetProperty("text").GetString());
        host.Register("fail", _ => throw new BridgeException("Nope."));
        using var ok = JsonDocument.Parse(await host.DispatchAsync("""{"id":7,"method":"echo","params":{"text":"hi"}}"""));
        check(ok.RootElement.GetProperty("id").GetInt32() == 7 && ok.RootElement.GetProperty("result").GetString() == "hi", "Bridge replies carry the request id and result");
        using var failed = JsonDocument.Parse(await host.DispatchAsync("""{"id":8,"method":"fail"}"""));
        check(failed.RootElement.GetProperty("error").GetString() == "Nope.", "Handler errors become readable error replies");
        using var unknown = JsonDocument.Parse(await host.DispatchAsync("""{"id":9,"method":"missing"}"""));
        check(unknown.RootElement.GetProperty("error").GetString()!.Contains("Unknown method"), "Unknown methods are reported, not fatal");
        using var malformed = JsonDocument.Parse(await host.DispatchAsync("{not json"));
        check(malformed.RootElement.TryGetProperty("error", out _), "Malformed requests are answered with an error");
    }

    static async Task BridgeProcessAsync(Action<bool, string> check, string temp)
    {
        var dataDir = Path.Combine(temp, "bridge-data");
        await using var first = await BridgeProcess.StartAsync(dataDir);
        check(first.Ready, "Bridge process starts and announces ready");
        var info = await first.CallAsync("app.info");
        check(info.GetProperty("result").GetProperty("counts").GetProperty("tracks").GetInt32() == 0
              && info.GetProperty("result").GetProperty("dataDirectory").GetString() == Path.GetFullPath(dataDir), "Bridge reports an empty library in its own data folder");
        var albums = await first.CallAsync("music.albums");
        check(albums.GetProperty("result").GetArrayLength() == 0, "Empty music library lists no albums");
        var channel = await first.CallAsync("tv.create", new { name = "Empty", units = Array.Empty<string>() });
        check(channel.GetProperty("error").GetString()!.Contains("known durations"), "Channels without schedulable videos are rejected with a reason");
        var play = await first.CallAsync("music.play", new { source = "tracks" });
        check(play.TryGetProperty("error", out _), "Playing an empty library reports an error instead of failing silently");

        await using var second = await BridgeProcess.StartAsync(dataDir);
        check(second.FatalMessage?.Contains("already running") == true, "A second Dustan on the same library is refused");
        await second.DisposeAsync();
        var exit = await first.StopAsync();
        check(exit == 0, "Bridge exits cleanly when the UI closes its input");
    }

    sealed class RecordingEngine : IMediaEngine
    {
        public event EventHandler? StateChanged { add { } remove { } }
        public event EventHandler? Ended { add { } remove { } }
        public event EventHandler? Failed { add { } remove { } }
        public volatile string? LastPath;
        public double Volume { get; set; }
        public bool IsPlaying { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration => TimeSpan.FromMinutes(3);
        public Task PlayFileAsync(string path) { LastPath = path; IsPlaying = true; return Task.CompletedTask; }
        public void Play() => IsPlaying = true;
        public void Pause() => IsPlaying = false;
        public void StopAndRelease() => IsPlaying = false;
        public void Dispose() { }
    }

    /// <summary>Runs the published bridge exactly as the Qt app does: JSON lines over stdin/stdout.</summary>
    sealed class BridgeProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private int _nextId;
        public bool Ready { get; private set; }
        public string? FatalMessage { get; private set; }

        private BridgeProcess(Process process) => _process = process;

        public static async Task<BridgeProcess> StartAsync(string dataDir)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Dustan.Bridge.dll"));
            start.Environment["DUSTAN_DATA_DIR"] = dataDir;
            var bridge = new BridgeProcess(Process.Start(start)!);
            _ = bridge._process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var line = await bridge._process.StandardOutput.ReadLineAsync(timeout.Token);
            using var first = JsonDocument.Parse(line ?? "{}");
            var name = first.RootElement.TryGetProperty("event", out var e) ? e.GetString() : null;
            bridge.Ready = name == "ready";
            if (name == "fatal") bridge.FatalMessage = first.RootElement.GetProperty("data").GetProperty("message").GetString();
            return bridge;
        }

        public async Task<JsonElement> CallAsync(string method, object? parameters = null)
        {
            var id = ++_nextId;
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters ?? new { } }));
            await _process.StandardInput.FlushAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (await _process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
            {
                using var reply = JsonDocument.Parse(line);
                if (reply.RootElement.TryGetProperty("id", out var replyId) && replyId.ValueKind == JsonValueKind.Number && replyId.GetInt32() == id)
                    return reply.RootElement.Clone();
            }
            throw new InvalidOperationException("Bridge closed its output.");
        }

        public async Task<int> StopAsync()
        {
            _process.StandardInput.Close();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await _process.WaitForExitAsync(timeout.Token);
            return _process.ExitCode;
        }

        private bool _disposed;

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            if (_process.HasExited) { _process.Dispose(); return; }
            try { await StopAsync(); }
            catch (OperationCanceledException) { _process.Kill(entireProcessTree: true); }
            _process.Dispose();
        }
    }
}
