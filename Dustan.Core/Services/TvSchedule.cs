using Dustan.Models;

namespace Dustan.Services;

/// <summary>A repeating, UTC-based schedule. Persisted channel snapshots keep guides stable after rescans.</summary>
public static class TvSchedule
{
    public static IReadOnlyList<TvProgramme> GetGuide(TvChannel channel, DateTimeOffset from, TimeSpan window)
    {
        if (window <= TimeSpan.Zero || channel.Videos.Count == 0) return [];
        var durations = channel.Videos.Select(v => v.DurationSeconds).ToArray();
        if (durations.Any(d => !double.IsFinite(d) || d < 1)) return [];
        var cycle = durations.Sum();
        var elapsed = (from - channel.Epoch).TotalSeconds;
        var start = channel.Epoch.AddSeconds(Math.Floor(elapsed / cycle) * cycle);
        var until = from + window;
        var result = new List<TvProgramme>();
        // Limit work for malformed or enormous guide requests.
        for (var i = 0; start < until && result.Count < 10000; i = (i + 1) % channel.Videos.Count)
        {
            var end = start.AddSeconds(durations[i]);
            if (end > from) result.Add(new TvProgramme(channel.Videos[i], start, end));
            start = end;
        }
        return result;
    }

    public static TvProgramme? Now(TvChannel channel, DateTimeOffset now) =>
        GetGuide(channel, now, TimeSpan.FromSeconds(1)).FirstOrDefault();
}
