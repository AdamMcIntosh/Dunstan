using Dustan;
using Dustan.Bridge;
using Dustan.Playback;
using Dustan.Services;

// stdout carries the protocol; keep stray library logging on stderr.
var protocolOut = Console.OpenStandardOutput();
Console.SetOut(Console.Error);
var host = new BridgeHost(Console.OpenStandardInput(), protocolOut);

using var instance = new SingleInstance(AppPaths.DataDirectory);
if (!instance.IsOwner)
{
    // Another Dustan owns this library. Report it, then wait for the UI to close us.
    host.Register("app.info", _ => throw new BridgeException("Dustan is already running. Switch to its existing window."));
    host.Emit("fatal", new { message = "Dustan is already running. Switch to its existing window." });
    host.RunAsync().GetAwaiter().GetResult();
    return 3;
}

IMediaEngine engine;
string? engineError = null;
try
{
    engine = new VlcMediaEngine();
}
catch (Exception ex)
{
    // Browsing, video and TV keep working without LibVLC; music playback reports why it is unavailable.
    engineError = "Music playback needs LibVLC. " + (OperatingSystem.IsLinux()
        ? "Install VLC (for example: sudo pacman -S vlc or sudo apt install vlc), then restart Dustan."
        : "Reinstall Dustan to restore the bundled LibVLC.") + $" ({ex.GetBaseException().Message})";
    engine = new UnavailableMediaEngine(engineError);
}

var services = AppServices.Initialize(engine);
using var api = new DustanApi(host, services, engineError);
host.Emit("ready", new { version = typeof(DustanApi).Assembly.GetName().Version?.ToString(3) });
// Block instead of awaiting: the instance mutex must be released by the thread that acquired it.
host.RunAsync().GetAwaiter().GetResult();
return 0;

internal sealed class UnavailableMediaEngine(string reason) : IMediaEngine
{
    public event EventHandler? StateChanged { add { } remove { } }
    public event EventHandler? Ended { add { } remove { } }
    public event EventHandler? Failed { add { } remove { } }
    public double Volume { get; set; } = 0.8;
    public bool IsPlaying => false;
    public TimeSpan Position { get => TimeSpan.Zero; set { } }
    public TimeSpan Duration => TimeSpan.Zero;
    public Task PlayFileAsync(string path) => throw new InvalidOperationException(reason);
    public void Play() { }
    public void Pause() { }
    public void StopAndRelease() { }
    public void Dispose() { }
}
