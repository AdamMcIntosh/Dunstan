using System.Text.RegularExpressions;
using Dustan.Models;

namespace Dustan.Services;

public sealed record VideoEpisode(VideoInfo Video, int Season, int Episode, string Code, string Name);

public sealed record VideoSeason(int Number, string Title, IReadOnlyList<VideoEpisode> Episodes);

/// <summary>A browsable title: a series grouped from season folders, or a single film.</summary>
public sealed record VideoShow(string Key, string Title, string Collection, bool IsMovie, IReadOnlyList<VideoSeason> Seasons)
{
    public IEnumerable<VideoEpisode> Episodes => Seasons.SelectMany(s => s.Episodes);

    /// <summary>The episode to offer next: an in-progress one, the one after the last finished, or the first unwatched.</summary>
    public VideoEpisode NextUp
    {
        get
        {
            var all = Episodes.ToList();
            var recent = all.Where(e => e.Video.LastPlayedUtc is not null).MaxBy(e => e.Video.LastPlayedUtc);
            if (recent is not null)
            {
                if (!recent.Video.Watched && recent.Video.PositionSeconds > 0) return recent;
                var index = all.IndexOf(recent);
                if (index + 1 < all.Count) return all[index + 1];
            }
            return all.FirstOrDefault(e => !e.Video.Watched) ?? all[0];
        }
    }
}

/// <summary>
/// Presentation grouping over the flat video catalog. Nothing here is persisted; the catalog in
/// videos.json stays a flat list so scanning, resume and TV schedules are unaffected.
/// </summary>
public static partial class VideoCatalog
{
    [GeneratedRegex(@"\b(?:season|series|saison|staffel)\W*(\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonWord();
    [GeneratedRegex(@"^s(\d{1,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonShort();
    [GeneratedRegex(@"^(?:specials?|extras?|featurettes?|bonus)$", RegexOptions.IgnoreCase)]
    private static partial Regex Specials();
    [GeneratedRegex(@"(?:^|[^a-z0-9])s(\d{1,2})\s*[ .\-_]?e(\d{1,3})(?![0-9])|(?:^|[^0-9])(\d{1,2})x(\d{2,3})(?![0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeCode();
    [GeneratedRegex(@"\[[^\]]*\]|\{[^}]*\}")]
    private static partial Regex Brackets();
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
    // Disc-rip suffixes such as "t00", "B1 t00", "-A5 t00" and "-FPL MainFeature t00".
    [GeneratedRegex(@"(?:[\s\-_]*(?:FPL[\s_]*MainFeature|[A-Z]\d))?[\s\-_]+t\d{2}$|[\s\-_]+FPL[\s_]*MainFeature$", RegexOptions.IgnoreCase)]
    private static partial Regex RipSuffix();
    [GeneratedRegex(@"\s+(?:\d{3,4}p|x26[45]|h\.?26[45]|HEVC|BluRay|BDRip|WEB-?DL|WEBRip|DTS|AC3|AAC|YIFY|REMUX)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseTags();
    [GeneratedRegex(@"^(?:[A-Z]\d|title\s*\d+|t\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscCode();
    [GeneratedRegex(@"^\s*(?:[&+\-]\s*e\d{1,3})+", RegexOptions.IgnoreCase)]
    private static partial Regex ExtraEpisodeCodes();

    public static IReadOnlyList<VideoShow> Group(IEnumerable<VideoInfo> videos, string rootName = "")
    {
        var groups = new Dictionary<string, (List<string> Parts, List<(VideoInfo Video, int? Season)> Items)>(PathSafety.Comparer);
        foreach (var video in videos)
        {
            var parts = SplitFolder(video.Folder);
            int? season = null;
            // Peel season and specials folders off the end; the folder above them names the series.
            while (parts.Count > 0 && TrySeasonFolder(parts[^1], out var number))
            {
                season ??= number;
                parts.RemoveAt(parts.Count - 1);
            }
            var key = string.Join('/', parts);
            if (!groups.TryGetValue(key, out var group)) groups[key] = group = (parts, []);
            group.Items.Add((video, season));
        }

        var shows = new List<VideoShow>();
        foreach (var (key, (parts, items)) in groups)
        {
            var collection = parts.Count > 0 ? parts[0] : rootName;
            var seasonFolders = items.Any(i => i.Season is not null);
            // Loose files in a top-level folder, or a folder holding a single file, are films.
            if (!seasonFolders && (parts.Count <= 1 || items.Count == 1))
            {
                foreach (var (video, _) in items)
                {
                    var film = FilmTitle(video.Title, parts.Count > 1 ? parts[^1] : null);
                    shows.Add(new VideoShow(video.Path, film, collection, true,
                        [new VideoSeason(0, "", [ToEpisode(video, null) with { Name = film }])]));
                }
                continue;
            }

            var title = parts.Count > 0 ? CleanTitle(parts[^1]) : rootName;
            var middle = parts.Count > 2 ? string.Join(" · ", parts.Skip(1).Take(parts.Count - 2)) : "";
            var episodes = items.Select(i => ToEpisode(i.Video, i.Season)).ToList();
            var seasons = episodes
                .GroupBy(e => e.Season)
                .OrderBy(g => g.Key == 0 ? int.MaxValue : g.Key)
                .Select(g => new VideoSeason(g.Key, g.Key != 0 ? $"Season {g.Key}" : seasonFolders || episodes.Any(e => e.Season > 0) ? "Specials & extras" : "Episodes",
                    g.OrderBy(e => e.Episode == 0 ? int.MaxValue : e.Episode)
                     .ThenBy(e => e.Video.Path, NaturalComparer.Instance).ToList()))
                .ToList();
            shows.Add(new VideoShow(key, title, string.IsNullOrEmpty(middle) ? collection : $"{collection} · {middle}", false, seasons));
        }

        return shows.OrderBy(s => s.Title, NaturalComparer.Instance).ThenBy(s => s.Key, NaturalComparer.Instance).ToList();
    }

    private static VideoEpisode ToEpisode(VideoInfo video, int? folderSeason)
    {
        var match = EpisodeCode().Match(video.Title);
        int season = folderSeason ?? 0, episode = 0;
        var name = video.Title;
        if (match.Success)
        {
            var s = int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value);
            episode = int.Parse(match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value);
            season = folderSeason ?? s;
            name = ExtraEpisodeCodes().Replace(video.Title[(match.Index + match.Length)..], "");
        }
        name = Spaces().Replace(Brackets().Replace(name, " "), " ").Trim(' ', '-', '–', '.', '_');
        var code = episode > 0 ? (season > 0 ? $"S{season} · E{episode}" : $"E{episode}") : "";
        if (string.IsNullOrEmpty(name)) name = episode > 0 ? $"Episode {episode}" : CleanTitle(video.Title);
        return new VideoEpisode(video, season, episode, code, name);
    }

    private static bool TrySeasonFolder(string segment, out int? number)
    {
        number = null;
        var word = SeasonWord().Match(segment);
        if (word.Success) { number = int.Parse(word.Groups[1].Value); return true; }
        var shortForm = SeasonShort().Match(segment.Trim());
        if (shortForm.Success) { number = int.Parse(shortForm.Groups[1].Value); return true; }
        if (Specials().IsMatch(segment.Trim())) { number = 0; return true; }
        return false;
    }

    private static List<string> SplitFolder(string folder) =>
        folder is "" or "." ? [] : folder.Split('/', '\\').Where(p => p.Length > 0 && p != ".").ToList();

    /// <summary>A readable film title, falling back to the folder name when the file is only a disc code.</summary>
    public static string FilmTitle(string fileTitle, string? folder)
    {
        var title = CleanTitle(ReleaseTags().Replace(RipSuffix().Replace(fileTitle.Trim(), ""), ""));
        if ((title.Length < 3 || DiscCode().IsMatch(title)) && !string.IsNullOrWhiteSpace(folder))
            title = CleanTitle(folder.Replace('_', ' '));
        return TitleCaseIfShouting(title);
    }

    private static string TitleCaseIfShouting(string value) =>
        value.Length > 4 && value.Any(char.IsLetter) && !value.Any(char.IsLower)
            ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant())
            : value;

    public static string CleanTitle(string value)
    {
        var text = Brackets().Replace(value, " ");
        if (!text.Contains(' ')) text = text.Replace('.', ' ').Replace('_', ' ');
        return Spaces().Replace(text, " ").Trim(' ', '-', '.');
    }
}

/// <summary>Orders "Episode 2" before "Episode 10".</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var a = x[si..i].TrimStart('0');
                var b = y[sj..j].TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                var digits = string.CompareOrdinal(a, b);
                if (digits != 0) return digits;
                continue;
            }
            var c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
            if (c != 0) return c;
            i++; j++;
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
