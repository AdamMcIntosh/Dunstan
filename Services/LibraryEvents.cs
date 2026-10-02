namespace Dustan.Services;

public static class LibraryEvents
{
    public static event EventHandler? Changed;

    public static void RaiseChanged() => Changed?.Invoke(null, EventArgs.Empty);
}
