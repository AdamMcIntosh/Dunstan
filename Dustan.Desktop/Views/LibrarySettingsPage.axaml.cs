using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Dustan.Models;
using Dustan.Services;

namespace Dustan.Views;

public partial class LibrarySettingsPage : UserControl
{
    public LibrarySettingsPage()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            LibraryPathBox.Text = AppServices.Instance.Settings.Current.LibraryPath;
            AppServices.Instance.Scanner.ProgressChanged -= ScannerOnProgressChanged;
            AppServices.Instance.Scanner.ProgressChanged += ScannerOnProgressChanged;
            UpdateIdleStatus();
        };
        DetachedFromVisualTree += (_, _) =>
            AppServices.Instance.Scanner.ProgressChanged -= ScannerOnProgressChanged;
    }

    private void UpdateIdleStatus()
    {
        var count = AppServices.Instance.Library.GetTrackCount();
        var path = AppServices.Instance.Settings.Current.LibraryPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusTitle.Text = "No library configured";
            StatusMessage.Text = "Choose a mounted NAS folder and scan.";
        }
        else
        {
            StatusTitle.Text = "Library ready";
            StatusMessage.Text = $"{count} tracks indexed from {path}";
        }
    }

    private async void BrowseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select music library folder",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            LibraryPathBox.Text = path;
        }
    }

    private async void SaveScanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = LibraryPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusTitle.Text = "Path required";
            StatusMessage.Text = "Enter a UNC path, mapped drive, or mounted folder.";
            return;
        }

        AppServices.Instance.Settings.SetLibraryPath(path);
        LibraryPathBox.Text = AppServices.Instance.Settings.Current.LibraryPath;
        AppServices.Instance.Playback.SetLibraryRoot(AppServices.Instance.Settings.Current.LibraryPath);
        await StartScanAsync();
    }

    private async void RescanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = AppServices.Instance.Settings.Current.LibraryPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = LibraryPathBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusTitle.Text = "Path required";
                StatusMessage.Text = "Save a library path first.";
                return;
            }

            AppServices.Instance.Settings.SetLibraryPath(path);
            AppServices.Instance.Playback.SetLibraryRoot(AppServices.Instance.Settings.Current.LibraryPath);
        }

        LibraryPathBox.Text = AppServices.Instance.Settings.Current.LibraryPath;
        await StartScanAsync();
    }

    private void CancelScanButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        AppServices.Instance.Scanner.Cancel();

    private async Task StartScanAsync()
    {
        var path = AppServices.Instance.Settings.Current.LibraryPath;
        StatusTitle.Text = "Scanning…";
        StatusMessage.Text = "Walking the library on a background thread.";
        SaveScanButton.IsEnabled = false;
        RescanButton.IsEnabled = false;

        try
        {
            await AppServices.Instance.Scanner.StartScanAsync(path);
        }
        catch (Exception ex)
        {
            StatusTitle.Text = "Scan failed";
            StatusMessage.Text = ex.Message;
        }
        finally
        {
            SaveScanButton.IsEnabled = true;
            RescanButton.IsEnabled = true;
        }
    }

    private void ScannerOnProgressChanged(object? sender, ScanProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressSummary.Text =
                $"Seen {progress.FilesSeen} · Tagged {progress.FilesTagged} · Skipped {progress.FilesSkipped} · Removed {progress.FilesRemoved}";
            ProgressPath.Text = progress.CurrentPath;

            if (!progress.IsComplete)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(progress.Error))
            {
                StatusTitle.Text = "Scan finished with issues";
                StatusMessage.Text = progress.Error!;
            }
            else
            {
                var count = AppServices.Instance.Library.GetTrackCount();
                StatusTitle.Text = "Scan complete";
                StatusMessage.Text = $"{count} tracks indexed. Unchanged files were skipped.";
            }
        });
    }
}
