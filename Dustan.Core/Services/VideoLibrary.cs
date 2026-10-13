using System.Text.Json;
using Dustan.Models;

namespace Dustan.Services;

public sealed class VideoLibrary
{
    private sealed class State
    {
        public string Root { get; set; } = "";
        public List<VideoInfo> Videos { get; set; } = [];
        public List<TvChannel> Channels { get; set; } = [];
    }

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mkv", ".mp4", ".m4v", ".avi", ".mov", ".wmv", ".webm", ".ts", ".m2ts", ".mpg", ".mpeg" };
    private readonly object _gate = new();
    private readonly SemaphoreSlim _scanGate = new(1);
    private readonly string _file;
    private State _state;
    public string? LoadError { get; }

    public VideoLibrary(string file)
    {
        _file = file;
        try { _state = File.Exists(file) ? JsonSerializer.Deserialize<State>(File.ReadAllText(file)) ?? new() : new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _state = new();
            LoadError = $"Could not load video library: {ex.Message}. Back up or repair {file} before saving.";
        }
    }

    public string Root { get { lock (_gate) return _state.Root; } }
    public IReadOnlyList<VideoInfo> Videos { get { lock (_gate) return _state.Videos.ToArray(); } }
    public IReadOnlyList<TvChannel> Channels { get { lock (_gate) return _state.Channels.Select(c => c with { Videos = c.Videos.ToList() }).ToArray(); } }

    public async Task<int> ScanAsync(string root, Func<string, CancellationToken, Task<double>> probe,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        root = Path.GetFullPath(root.Trim());
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Video folder is unavailable. Check your NAS mount.");
        await _scanGate.WaitAsync(cancellationToken);
        try
        {
            var existing = Videos.ToDictionary(v => v.Path, PathSafety.Comparer);
            var scanned = await Task.Run(async () =>
            {
                var result = new List<VideoInfo>();
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (var path in Directory.EnumerateFiles(root, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Extensions.Contains(Path.GetExtension(path))) continue;
                    progress?.Report($"{result.Count + 1}: {Path.GetFileName(path)}");
                    var file = new FileInfo(path);
                    existing.TryGetValue(path, out var old);
                    var duration = old is not null && old.FileSize == file.Length && old.ModifiedUtcTicks == file.LastWriteTimeUtc.Ticks && old.DurationSeconds > 0
                        ? old.DurationSeconds : await probe(path, cancellationToken);
                    var folder = Path.GetDirectoryName(path)!;
                    var poster = new[] { Path.ChangeExtension(path, ".jpg"), Path.ChangeExtension(path, ".png"),
                        Path.Combine(folder, "poster.jpg"), Path.Combine(folder, "folder.jpg") }.FirstOrDefault(File.Exists);
                    result.Add(new VideoInfo { Path = path, Title = Path.GetFileNameWithoutExtension(path).Replace('.', ' ').Replace('_', ' '),
                        Folder = Path.GetRelativePath(root, folder), PosterPath = poster, FileSize = file.Length,
                        ModifiedUtcTicks = file.LastWriteTimeUtc.Ticks, DurationSeconds = double.IsFinite(duration) && duration > 0 ? duration : 0 });
                }
                return result.OrderBy(v => v.Path, PathSafety.Comparer).ToList();
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                var latest = _state.Videos.ToDictionary(v => v.Path, PathSafety.Comparer);
                scanned = scanned.Select(v => latest.TryGetValue(v.Path, out var old)
                    ? v with { PositionSeconds = old.PositionSeconds, Watched = old.Watched, LastPlayedUtc = old.LastPlayedUtc } : v).ToList();
                Commit(new State { Root = root, Videos = scanned, Channels = _state.Channels });
            }
            return scanned.Count;
        }
        finally { _scanGate.Release(); }
    }

    public void SaveProgress(string path, double seconds, bool completed = false)
    {
        lock (_gate)
        {
            var videos = _state.Videos.Select(v => PathSafety.Comparer.Equals(v.Path, path)
                ? v with { PositionSeconds = completed ? 0 : Math.Max(0, double.IsFinite(seconds) ? seconds : 0), Watched = v.Watched || completed,
                    LastPlayedUtc = DateTimeOffset.UtcNow } : v).ToList();
            Commit(new State { Root = _state.Root, Videos = videos, Channels = _state.Channels });
        }
    }

    /// <summary>Marks videos watched or unwatched in one write; either way their resume points are cleared.</summary>
    public void SetWatched(IEnumerable<string> paths, bool watched)
    {
        var targets = paths.ToHashSet(PathSafety.Comparer);
        lock (_gate)
        {
            var videos = _state.Videos.Select(v => targets.Contains(v.Path)
                ? v with { PositionSeconds = 0, Watched = watched } : v).ToList();
            Commit(new State { Root = _state.Root, Videos = videos, Channels = _state.Channels });
        }
    }

    public TvChannel AddChannel(string name, IEnumerable<VideoInfo> videos, DateTimeOffset now)
    {
        var list = videos.ToList();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a channel name.");
        if (list.Count == 0 || list.Any(v => !double.IsFinite(v.DurationSeconds) || v.DurationSeconds < 1))
            throw new ArgumentException("Select videos with known durations of at least one second. Rescan files with unknown durations.");
        lock (_gate)
        {
            var channel = new TvChannel { Name = name.Trim(), Epoch = now, Videos = list };
            Commit(new State { Root = _state.Root, Videos = _state.Videos, Channels = [.. _state.Channels, channel] });
            return channel;
        }
    }

    public void RemoveChannel(string id)
    {
        lock (_gate) Commit(new State { Root = _state.Root, Videos = _state.Videos, Channels = _state.Channels.Where(c => c.Id != id).ToList() });
    }

    private void Commit(State state)
    {
        if (LoadError is not null) throw new IOException(LoadError);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file))!);
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state));
        File.Move(temp, _file, overwrite: true);
        _state = state;
    }
}
