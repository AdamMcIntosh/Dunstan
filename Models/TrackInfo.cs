namespace Dustan.Models;

public sealed class TrackInfo
{
    public long Id { get; init; }
    public string Path { get; init; } = "";
    public long FileSize { get; init; }
    public long ModifiedUtcTicks { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public string AlbumArtist { get; init; } = "";
    public int TrackNumber { get; init; }
    public int DiscNumber { get; init; }
    public int Year { get; init; }
    public string Genre { get; init; } = "";
    public double DurationSeconds { get; init; }
    public long? AlbumId { get; init; }
    public long? ArtistId { get; init; }
    public bool OpenFailed { get; set; }

    public string DurationDisplay => TimeSpan.FromSeconds(DurationSeconds).ToString(DurationSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
}

public sealed class AlbumInfo
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string AlbumArtist { get; init; } = "";
    public int Year { get; init; }
    public int TrackCount { get; init; }
    public string? ArtworkPath { get; init; }
    /// <summary>Parent folder of the tracks; used for identity, not shown in UI.</summary>
    public string FolderPath { get; init; } = "";
}

public sealed class ArtistInfo
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public int AlbumCount { get; init; }
    public int TrackCount { get; init; }
}

public enum RepeatMode
{
    Off,
    /// <summary>Loop the current queue (typically the album you started).</summary>
    Album,
    One
}

public sealed class ScanProgress
{
    public int FilesSeen { get; init; }
    public int FilesTagged { get; init; }
    public int FilesSkipped { get; init; }
    public int FilesRemoved { get; init; }
    public string CurrentPath { get; init; } = "";
    public bool IsComplete { get; init; }
    public string? Error { get; init; }
}
