namespace Dustan.Models;

public sealed record VideoInfo
{
    public string Path { get; init; } = "";
    public string Title { get; init; } = "";
    public string Folder { get; init; } = "";
    public string? PosterPath { get; init; }
    public long FileSize { get; init; }
    public long ModifiedUtcTicks { get; init; }
    public double DurationSeconds { get; init; }
    public double PositionSeconds { get; init; }
    public bool Watched { get; init; }
    /// <summary>Last time playback progress was saved; absent in catalogs written by older builds.</summary>
    public DateTimeOffset? LastPlayedUtc { get; init; }
    public string DurationLabel => DurationSeconds > 0 ? TimeSpan.FromSeconds(DurationSeconds).ToString(@"hh\:mm\:ss") : "Duration unavailable";
    public string ProgressLabel => Watched ? "Watched" : PositionSeconds > 0 ? "Resume at " + TimeSpan.FromSeconds(PositionSeconds).ToString(@"hh\:mm\:ss") : "Unwatched";
}

public sealed record TvChannel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public DateTimeOffset Epoch { get; init; } = DateTimeOffset.UtcNow;
    public List<VideoInfo> Videos { get; init; } = [];
    public override string ToString() => Name;
}

public sealed record TvProgramme(VideoInfo Video, DateTimeOffset Start, DateTimeOffset End)
{
    public TimeSpan OffsetAt(DateTimeOffset now) => now <= Start ? TimeSpan.Zero : now >= End ? End - Start : now - Start;
}
