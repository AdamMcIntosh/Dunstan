using Avalonia;
using System;
using Dustan.Services;

namespace Dustan;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = new SingleInstance(AppPaths.DataDirectory);
        if (!instance.IsOwner)
        {
            Console.Error.WriteLine("Dustan is already running. Switch to its existing window.");
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
