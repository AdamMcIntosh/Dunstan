namespace Dustan.Models;

public sealed class TagEdit
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string AlbumArtist { get; set; } = "";
    public int TrackNumber { get; set; }
    public int DiscNumber { get; set; }
    public int Year { get; set; }
    public string Genre { get; set; } = "";
    public byte[]? ArtworkBytes { get; set; }
    public bool ArtworkChanged { get; set; }
    public bool RemoveArtwork { get; set; }
}

public enum TagEditScope
{
    Track,
    Album
}

public sealed class TagWriteResult
{
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public long? AlbumId { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool HasFailures => Failed > 0;
}
