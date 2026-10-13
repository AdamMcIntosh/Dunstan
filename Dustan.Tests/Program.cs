using Dustan.Models;
using Dustan.Playback;
using Dustan.Services;

if (args.Length > 1 && args[0] == "--probe-fixture")
{
    switch (Path.GetFileName(args[^1]))
    {
        case "failed.mkv": Environment.Exit(139); break;
        case "invalid.mkv": Console.WriteLine("not json"); break;
        case "unknown.mkv": Console.WriteLine("{\"format\":{\"duration\":\"N/A\"}}"); break;
        case "hang.mkv":
            File.WriteAllText(args[^1] + ".pid", Environment.ProcessId.ToString());
            Thread.Sleep(30000); break;
        default: Console.WriteLine("{\"format\":{\"duration\":\"123.456\"}}"); break;
    }
    return;
}

if (args.Length == 2 && args[0] == "--instance-check")
{
    bool owns;
    using (var instance = new SingleInstance(args[1])) owns = instance.IsOwner;
    Environment.Exit(owns ? 2 : 0);
}

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}

var epoch = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
var first = new VideoInfo { Path = "/videos/first.mkv", Title = "First", DurationSeconds = 1800 };
var second = new VideoInfo { Path = "/videos/second.mp4", Title = "Second", DurationSeconds = 3600 };
var channel = new TvChannel { Epoch = epoch, Videos = [first, second] };
Check(TvSchedule.Now(channel, epoch)?.Video == first, "Epoch starts first program");
Check(TvSchedule.Now(channel, epoch.AddMinutes(30))?.Video == second, "Exact boundary starts next program");
Check(TvSchedule.Now(channel, epoch.AddMinutes(90))?.Video == first, "Lineup loops at cycle boundary");
Check(TvSchedule.Now(channel, epoch.AddMinutes(40))?.OffsetAt(epoch.AddMinutes(40)) == TimeSpan.FromMinutes(10), "Tune-in uses elapsed program position");
Check(TvSchedule.Now(channel, epoch.AddSeconds(-1))?.Video == second, "Pre-epoch queries wrap consistently");
Check(TvSchedule.Now(channel, epoch.ToOffset(TimeSpan.FromHours(-4)))?.Start == epoch, "Timezone offsets do not shift schedule");
var guide = TvSchedule.GetGuide(channel, epoch.AddMinutes(20), TimeSpan.FromHours(6));
Check(guide.First().Start == epoch && guide.Last().End >= epoch.AddMinutes(380), "Guide includes overlapping edge programs");
Check(guide.Zip(guide.Skip(1)).All(p => p.First.End == p.Second.Start), "Guide has no gaps or overlaps across loops");
Check(TvSchedule.Now(channel with { Videos = [] }, epoch) is null, "Empty channel handled");
Check(TvSchedule.Now(channel with { Videos = [first with { DurationSeconds = double.NaN }] }, epoch) is null, "Invalid duration cannot hang scheduler");

var temp = Path.Combine(Path.GetTempPath(), "dustan-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var testAssembly = typeof(FakeMediaEngine).Assembly.Location;
    using (var probe = new VideoProbe("dotnet", TimeSpan.FromSeconds(2), [testAssembly, "--probe-fixture"]))
    {
        Check(await probe.GetDurationAsync(Path.Combine(temp, "movie with spaces.mkv"), CancellationToken.None) == 123.456, "Isolated probe preserves filename arguments and fractional runtime");
        Check(await probe.GetDurationAsync(Path.Combine(temp, "failed.mkv"), CancellationToken.None) == 0, "Parser process failure cannot terminate application");
        Check(await probe.GetDurationAsync(Path.Combine(temp, "invalid.mkv"), CancellationToken.None) == 0, "Malformed probe output leaves duration unknown");
        Check(await probe.GetDurationAsync(Path.Combine(temp, "unknown.mkv"), CancellationToken.None) == 0, "Unknown probe duration handled");
        var hang = Path.Combine(temp, "hang.mkv");
        Check(await probe.GetDurationAsync(hang, CancellationToken.None) == 0, "Hung parser times out without failing scan");
        var pid = int.Parse(File.ReadAllText(hang + ".pid"));
        bool alive;
        try { using var process = System.Diagnostics.Process.GetProcessById(pid); alive = !process.HasExited; }
        catch (ArgumentException) { alive = false; }
        Check(!alive, "Timed-out parser is reaped");
        using var cancelProbe = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var cancelled = false;
        try { await probe.GetDurationAsync(hang, cancelProbe.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Probe propagates user cancellation");
        pid = int.Parse(File.ReadAllText(hang + ".pid"));
        try { using var process = System.Diagnostics.Process.GetProcessById(pid); alive = !process.HasExited; }
        catch (ArgumentException) { alive = false; }
        Check(!alive, "Cancelled parser is reaped");
    }
    using (var missing = new VideoProbe(Path.Combine(temp, "missing-ffprobe")))
    {
        var explained = false;
        try { await missing.GetDurationAsync("video.mp4", CancellationToken.None); }
        catch (InvalidOperationException ex) { explained = ex.Message.Contains("Install FFmpeg"); }
        Check(explained, "Missing ffprobe reports actionable installation error");
    }
    using (var instance = new SingleInstance(temp))
    {
        Check(instance.IsOwner, "First process acquires instance lock");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(typeof(VideoLibrary).Assembly.Location.Replace("Dustan.Core.dll", "Dustan.Tests.dll"));
        start.ArgumentList.Add("--instance-check"); start.ArgumentList.Add(temp);
        using var process = System.Diagnostics.Process.Start(start)!;
        Check(process.WaitForExit(10000) && process.ExitCode == 0, "Second process cannot acquire same library lock");
    }
    using (var nextInstance = new SingleInstance(temp)) Check(nextInstance.IsOwner, "Instance lock releases on exit");

    var musicDb = Path.Combine(temp, "music.db");
    using (var db = new LibraryDb(musicDb))
    {
        db.Initialize();
        var artist = db.UpsertArtist("Artist");
        var album = db.UpsertAlbum("Album", "Artist", 2000, "cover.jpg", "");
        foreach (var folder in new[] { "copy-one", "copy-two" })
            db.UpsertTrack(new TrackInfo { Path = Path.Combine(temp, folder, "song.flac"), Album = "Album", AlbumArtist = "Artist", Title = "Song", Year = 2000 }, artist, album);
        using var cmd = db.GetConnection().CreateCommand();
        cmd.CommandText = "DROP INDEX idx_albums_unique; ALTER TABLE albums DROP COLUMN folder_path; CREATE UNIQUE INDEX idx_albums_unique ON albums(name COLLATE NOCASE, album_artist COLLATE NOCASE); PRAGMA user_version=0;";
        cmd.ExecuteNonQuery();
        db.Initialize();
        Check(db.GetTrackCount() == 2 && db.GetAlbums().Count == 2, "Legacy migration preserves tracks and separates album folders");
        Check(db.GetAlbums().All(a => a.ArtworkPath == "cover.jpg"), "Legacy migration preserves artwork");
        cmd.CommandText = "PRAGMA user_version;";
        Check(Convert.ToInt64(cmd.ExecuteScalar()) == 1, "Successful migration records schema version");
        cmd.CommandText = "PRAGMA query_only=ON;"; cmd.ExecuteNonQuery();
        db.Initialize();
        Check(db.GetTrackCount() == 2, "Repeat startup succeeds with all database writes prohibited");
    }
    using (var db = new LibraryDb(musicDb))
    {
        using var writer = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={musicDb};Pooling=False"); writer.Open();
        using var transaction = writer.BeginTransaction();
        db.Initialize();
        Check(db.GetTrackCount() == 2, "Migrated startup succeeds while another connection holds writer lock");
        using var cmd = db.GetConnection().CreateCommand(); cmd.CommandText = "PRAGMA foreign_keys;";
        Check(Convert.ToInt64(cmd.ExecuteScalar()) == 1, "Foreign key enforcement survives fast startup");
    }
    var root = Path.Combine(temp, "media"); Directory.CreateDirectory(root);
    var path = Path.Combine(root, "Movie.MKV");
    File.WriteAllText(path, "fixture");
    File.WriteAllText(Path.Combine(root, "music.mp3"), "music fixture");
    File.WriteAllText(Path.Combine(root, "poster.jpg"), "poster fixture");
    var stateFile = Path.Combine(temp, "videos.json");
    var library = new VideoLibrary(stateFile);
    var probes = 0;
    Task<double> Probe(string _, CancellationToken __) { probes++; return Task.FromResult(120d); }
    await library.ScanAsync(root, Probe, null, CancellationToken.None);
    Check(library.Videos.Count == 1 && probes == 1, "Scan includes videos and excludes music");
    Check(library.Videos[0].PosterPath == Path.Combine(root, "poster.jpg"), "Local poster discovered");
    library.SaveProgress(path, 45);
    var savedChannel = library.AddChannel("Cinema", library.Videos, epoch);
    await library.ScanAsync(root, Probe, null, CancellationToken.None);
    Check(probes == 1 && library.Videos[0].PositionSeconds == 45, "Unchanged scan caches runtime and preserves resume");
    var restored = new VideoLibrary(stateFile);
    Check(restored.LoadError is null && restored.Videos.Single().PositionSeconds == 45, "Resume state survives restart");
    Check(restored.Channels.Single().Epoch == epoch && restored.Channels.Single().Id == savedChannel.Id, "Channel schedule survives restart");
    var snapshot = File.ReadAllText(stateFile);
    try { await library.ScanAsync(Path.Combine(temp, "offline"), Probe, null, CancellationToken.None); }
    catch (DirectoryNotFoundException) { }
    Check(File.ReadAllText(stateFile) == snapshot, "Offline NAS does not erase catalog");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { await library.ScanAsync(root, Probe, null, cancel.Token); }
    catch (OperationCanceledException) { }
    Check(File.ReadAllText(stateFile) == snapshot, "Cancelled scan does not commit partial library");
    try { library.AddChannel("Invalid", [first with { DurationSeconds = 0 }], epoch); }
    catch (ArgumentException) { }
    Check(library.Channels.Count == 1, "Unknown durations rejected for TV");
    library.SaveProgress(path, 120, completed: true);
    Check(library.Videos[0].Watched && library.Videos[0].PositionSeconds == 0, "Completed video marks watched and clears resume");
    File.AppendAllText(path, "changed");
    await library.ScanAsync(root, (_, _) => Task.FromResult(240d), null, CancellationToken.None);
    Check(library.Videos[0].DurationSeconds == 240 && library.Channels[0].Videos[0].DurationSeconds == 120, "Rescan cannot shift saved channel schedule");
    File.Delete(path);
    await library.ScanAsync(root, Probe, null, CancellationToken.None);
    Check(library.Videos.Count == 0 && library.Channels.Count == 1, "Completed rescan removes missing video while preserving channel snapshot");
    library.RemoveChannel(savedChannel.Id);
    Check(new VideoLibrary(stateFile).Channels.Count == 0, "Channel removal persists");
    File.WriteAllText(stateFile, "{broken");
    var corrupt = new VideoLibrary(stateFile);
    Check(corrupt.LoadError is not null, "Corrupt catalog reports error");
    try { corrupt.SaveProgress(path, 1); } catch (IOException) { }
    Check(File.ReadAllText(stateFile) == "{broken", "Corrupt catalog protected from overwrite");

    using var engine = new FakeMediaEngine();
    using var playback = new PlaybackService(engine);
    playback.SetLibraryRoot(root);
    playback.PlayList([new TrackInfo { Path = Path.Combine(root, "music.mp3"), Title = "Music" }]);
    Check(engine.IsPlaying && playback.CurrentTrack?.Title == "Music", "Existing music playback still starts");
    playback.Pause(); Check(!engine.IsPlaying, "Music can pause before video playback");
    playback.Play(); Check(engine.IsPlaying, "Music can resume after video playback");

    await QtBridgeTests.RunAsync(Check, temp);
}
finally { Directory.Delete(temp, recursive: true); }

if (args.Length == 2 && args[0] == "--media")
{
    LibVLCSharp.Shared.Core.Initialize();
    using var probe = new VideoProbe();
    var duration = await probe.GetDurationAsync(Path.GetFullPath(args[1]), CancellationToken.None);
    Check(duration > 0, $"Isolated ffprobe reads video duration ({duration:F2}s)");
    using var vlc = new LibVLCSharp.Shared.LibVLC("--quiet", "--vout=dummy", "--aout=dummy");
    using var media = new LibVLCSharp.Shared.Media(vlc, Path.GetFullPath(args[1]), LibVLCSharp.Shared.FromType.FromPath);
    using var player = new LibVLCSharp.Shared.MediaPlayer(vlc);
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    player.Playing += (_, _) => started.TrySetResult();
    Check(player.Play(media), "Native VLC accepts video playback");
    await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
    await Task.Delay(500);
    Check(player.IsPlaying && player.Length > 0 && media.Statistics.DecodedVideo > 0, "Native VLC decodes video frames");
    player.Stop();
}
if (args.Length == 2 && args[0] == "--database-copy")
{
    using var db = new LibraryDb(args[1]);
    var timer = System.Diagnostics.Stopwatch.StartNew();
    db.Initialize();
    Console.WriteLine($"Library-copy migration: {timer.ElapsedMilliseconds}ms; {db.GetTrackCount()} tracks; {db.GetAlbums().Count} albums");
    timer.Restart(); db.Initialize();
    Check(timer.Elapsed < TimeSpan.FromSeconds(1), "Populated library repeat startup completes under one second");
    using var cmd = db.GetConnection().CreateCommand(); cmd.CommandText = "PRAGMA quick_check;";
    Check((string?)cmd.ExecuteScalar() == "ok", "Migrated library copy passes integrity check");
}
Console.WriteLine($"{passed} checks passed.");

if (args.Length == 3 && args[0] == "--scan-library")
{
    var library = new VideoLibrary(args[2]);
    using var probe = new VideoProbe();
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var lastReport = TimeSpan.Zero;
    var progress = new Progress<string>(_ =>
    {
        if (timer.Elapsed - lastReport > TimeSpan.FromSeconds(20))
        {
            lastReport = timer.Elapsed;
            Console.WriteLine($"NAS scan in progress: {timer.Elapsed.TotalSeconds:F0}s");
        }
    });
    var count = await library.ScanAsync(args[1], probe.GetDurationAsync, progress, CancellationToken.None);
    Console.WriteLine($"NAS scan saved {count} videos in {timer.Elapsed.TotalSeconds:F1}s; {library.Videos.Count(v => v.DurationSeconds <= 0)} unknown durations");
    Check(new VideoLibrary(args[2]).Videos.Count == count, "Full NAS catalog saves and reloads");
}

sealed class FakeMediaEngine : IMediaEngine
{
    public event EventHandler? StateChanged { add { } remove { } }
    public event EventHandler? Ended { add { } remove { } }
    public event EventHandler? Failed { add { } remove { } }
    public double Volume { get; set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration => TimeSpan.FromMinutes(3);
    public Task PlayFileAsync(string path) { IsPlaying = true; return Task.CompletedTask; }
    public void Play() => IsPlaying = true;
    public void Pause() => IsPlaying = false;
    public void StopAndRelease() => IsPlaying = false;
    public void Dispose() { }
}
