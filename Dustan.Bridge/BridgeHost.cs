using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Dustan.Bridge;

/// <summary>
/// Newline-delimited JSON over a pair of streams.
/// Requests:  {"id":1,"method":"music.albums","params":{...}}
/// Responses: {"id":1,"result":...} or {"id":1,"error":"message"}
/// Events:    {"event":"player","data":{...}}
/// </summary>
public sealed class BridgeHost
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Channel<string> _outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, Func<JsonElement, Task<object?>>> _handlers = new(StringComparer.Ordinal);
    private readonly HashSet<Task> _inFlight = [];

    public BridgeHost(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public void Register(string method, Func<JsonElement, Task<object?>> handler) => _handlers[method] = handler;

    public void Register(string method, Func<JsonElement, object?> handler) =>
        _handlers[method] = p => Task.FromResult(handler(p));

    public void Emit(string name, object? data) =>
        _outbox.Writer.TryWrite(JsonSerializer.Serialize(new { @event = name, data }, Json));

    /// <summary>Serves requests until the input stream closes, i.e. the UI process has exited.</summary>
    public async Task RunAsync()
    {
        var writer = Task.Run(WriteLoopAsync);
        using (var reader = new StreamReader(_input, new UTF8Encoding(false)))
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var task = Task.Run(() => DispatchAsync(line));
                lock (_inFlight) _inFlight.Add(task);
                _ = task.ContinueWith(t => { lock (_inFlight) _inFlight.Remove(t); }, TaskScheduler.Default);
            }
        }
        // Let running requests finish before the caller disposes the services they use.
        Task[] pending;
        lock (_inFlight) pending = _inFlight.ToArray();
        await Task.WhenAny(Task.WhenAll(pending), Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
        _outbox.Writer.TryComplete();
        await writer.ConfigureAwait(false);
    }

    public async Task<string> DispatchAsync(string line)
    {
        JsonNode? id = null;
        string response;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var idElement)) id = JsonNode.Parse(idElement.GetRawText());
            var method = root.GetProperty("method").GetString() ?? "";
            var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;
            if (!_handlers.TryGetValue(method, out var handler)) throw new BridgeException($"Unknown method '{method}'.");
            var result = await handler(parameters).ConfigureAwait(false);
            response = JsonSerializer.Serialize(new { id, result }, Json);
        }
        catch (Exception ex)
        {
            var message = ex is BridgeException or ArgumentException or IOException or DirectoryNotFoundException or InvalidOperationException
                ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
            response = JsonSerializer.Serialize(new { id, error = message }, Json);
        }
        _outbox.Writer.TryWrite(response);
        return response;
    }

    private async Task WriteLoopAsync()
    {
        var newline = new byte[] { (byte)'\n' };
        await foreach (var message in _outbox.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await _output.WriteAsync(Encoding.UTF8.GetBytes(message)).ConfigureAwait(false);
                await _output.WriteAsync(newline).ConfigureAwait(false);
                await _output.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                return; // The UI went away; RunAsync ends when stdin closes.
            }
        }
    }
}

public sealed class BridgeException(string message) : Exception(message);

internal static class Params
{
    public static string Str(this JsonElement p, string name, string fallback = "") =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    public static long Long(this JsonElement p, string name, long fallback = 0) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : fallback;

    public static double Num(this JsonElement p, string name, double fallback = 0) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;

    public static bool Bool(this JsonElement p, string name, bool fallback = false) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;

    public static bool Has(this JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null;

    public static IReadOnlyList<string> Strings(this JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList() : [];
}
