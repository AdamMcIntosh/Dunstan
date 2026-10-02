using Dustan.Models;
using Dustan.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Dustan.Views;

public sealed partial class LibrarySettingsPage : Page
{
    public LibrarySettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        LibraryPathBox.Text = AppServices.Instance.Settings.Current.LibraryPath;
        AppServices.Instance.Scanner.ProgressChanged += ScannerOnProgressChanged;
        UpdateIdleStatus();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        AppServices.Instance.Scanner.ProgressChanged -= ScannerOnProgressChanged;
    }

    private void UpdateIdleStatus()
    {
        var count = AppServices.Instance.Library.GetTrackCount();
        var path = AppServices.Instance.Settings.Current.LibraryPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusBar.Title = "No library configured";
            StatusBar.Message = "Enter a network path and scan.";
            StatusBar.Severity = InfoBarSeverity.Warning;
        }
        else
        {
            StatusBar.Title = "Library ready";
            StatusBar.Message = $"{count} tracks indexed from {path}";
            StatusBar.Severity = InfoBarSeverity.Success;
        }
    }

    private async void SaveScanButton_Click(object sender, RoutedEventArgs e)
    {
        var path = LibraryPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusBar.Title = "Path required";
            StatusBar.Message = "Enter a UNC or mapped drive path.";
            StatusBar.Severity = InfoBarSeverity.Error;
            return;
        }

        AppServices.Instance.Settings.SetLibraryPath(path);
        LibraryPathBox.Text = AppServices.Instance.Settings.Current.LibraryPath;
        AppServices.Instance.Playback.SetLibraryRoot(AppServices.Instance.Settings.Current.LibraryPath);
        await StartScanAsync();
    }

    private async void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        var path = AppServices.Instance.Settings.Current.LibraryPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = LibraryPathBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusBar.Title = "Path required";
                StatusBar.Message = "Save a library path first.";
                StatusBar.Severity = InfoBarSeverity.Error;
                return;
            }

            AppServices.Instance.Settings.SetLibraryPath(path);
            AppServices.Instance.Playback.SetLibraryRoot(AppServices.Instance.Settings.Current.LibraryPath);
        }

        LibraryPathBox.Text = AppServices.Instance.Settings.Current.LibraryPath;
        await StartScanAsync();
    }

    private void CancelScanButton_Click(object sender, RoutedEventArgs e) =>
        AppServices.Instance.Scanner.Cancel();

    private async Task StartScanAsync()
    {
        var path = AppServices.Instance.Settings.Current.LibraryPath;
        StatusBar.Title = "Scanning…";
        StatusBar.Message = "Walking the library on a background thread.";
        StatusBar.Severity = InfoBarSeverity.Informational;
        SaveScanButton.IsEnabled = false;
        RescanButton.IsEnabled = false;

        try
        {
            await AppServices.Instance.Scanner.StartScanAsync(path);
        }
        catch (Exception ex)
        {
            StatusBar.Title = "Scan failed";
            StatusBar.Message = ex.Message;
            StatusBar.Severity = InfoBarSeverity.Error;
        }
        finally
        {
            SaveScanButton.IsEnabled = true;
            RescanButton.IsEnabled = true;
        }
    }

    private void ScannerOnProgressChanged(object? sender, ScanProgress progress)
    {
        DispatcherQueue.TryEnqueue(() =>
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
                StatusBar.Title = "Scan finished with issues";
                StatusBar.Message = progress.Error!;
                StatusBar.Severity = InfoBarSeverity.Warning;
            }
            else
            {
                var count = AppServices.Instance.Library.GetTrackCount();
                StatusBar.Title = "Scan complete";
                StatusBar.Message = $"{count} tracks indexed. Unchanged files were skipped.";
                StatusBar.Severity = InfoBarSeverity.Success;
            }
        });
    }
}
