using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Dustan.Playback;

public sealed class VideoProbe : IDisposable
{
    private readonly string _executable;
    private readonly TimeSpan _timeout;
    private readonly string[] _prefixArguments;

    public VideoProbe(string? executable = null, TimeSpan? timeout = null, IEnumerable<string>? prefixArguments = null)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        _executable = executable ?? (File.Exists(bundled) ? bundled : "ffprobe");
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
        _prefixArguments = prefixArguments?.ToArray() ?? [];
    }

    public async Task<double> GetDurationAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in _prefixArguments) start.ArgumentList.Add(argument);
        // ArgumentList preserves spaces and special characters in NAS filenames.
        foreach (var argument in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "json", "-i", Path.GetFullPath(path) })
            start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Cannot start ffprobe. Install FFmpeg (including ffprobe), then scan again. " + ex.Message, ex);
        }

        var output = process.StandardOutput.ReadToEndAsync();
        // Drain stderr concurrently, without retaining potentially large parser diagnostics.
        var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await errors.ConfigureAwait(false);
            var json = await output.ConfigureAwait(false);
            if (process.ExitCode != 0) return 0;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("format", out var format)
                    && format.ValueKind == JsonValueKind.Object
                    && format.TryGetProperty("duration", out var duration)
                    && double.TryParse(duration.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    && double.IsFinite(seconds) && seconds > 0)
                    return seconds;
            }
            catch (JsonException) { /* Unreadable metadata leaves duration unknown. */ }
            return 0;
        }
        catch (OperationCanceledException)
        {
            // Never leave a native parser running after cancellation or timeout.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* Process exited between checking and killing. */ }
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(output, errors).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return 0;
        }
    }

    public void Dispose() { }
}
