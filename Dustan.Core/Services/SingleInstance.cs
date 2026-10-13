using System.Security.Cryptography;
using System.Text;

namespace Dustan.Services;

/// <summary>Holds an OS mutex for the lifetime of the desktop process.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsOwner { get; }

    public SingleInstance(string dataDirectory)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory))));
        _mutex = new Mutex(false, "Dustan-" + key);
        try { IsOwner = _mutex.WaitOne(TimeSpan.Zero); }
        catch (AbandonedMutexException) { IsOwner = true; }
    }

    public void Dispose()
    {
        if (IsOwner) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
