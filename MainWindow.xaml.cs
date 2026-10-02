using Dustan.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Dustan;

public sealed partial class MainWindow : Window
{
    private string? _currentSearch;
    private bool _suppressNavSelection;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        try
        {
            AppWindow.SetIcon("Assets/AppIcon.ico");
        }
        catch
        {
            // icon optional when running from unexpected cwd
        }

        ContentFrame.Navigated += ContentFrame_Navigated;
        RootGrid.Loaded += (_, _) => RootGrid.Focus(FocusState.Programmatic);

        var settings = AppServices.Instance.Settings.Current;
        if (string.IsNullOrWhiteSpace(settings.LibraryPath) || AppServices.Instance.Library.GetTrackCount() == 0)
        {
            NavigateTo("library");
            SelectNavItem("library");
        }
        else
        {
            NavigateTo("albums");
            SelectNavItem("albums");
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        NavView.IsBackEnabled = ContentFrame.CanGoBack;
        SyncNavSelectionToPage(e.SourcePageType);
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        TryGoBack();
    }

    private bool TryGoBack()
    {
        if (!ContentFrame.CanGoBack)
        {
            return false;
        }

        ContentFrame.GoBack();
        return true;
    }

    private void SyncNavSelectionToPage(Type pageType)
    {
        string? tag = pageType.Name switch
        {
            nameof(AlbumsPage) => "albums",
            nameof(ArtistsPage) => "artists",
            nameof(TracksPage) => "tracks",
            nameof(LibrarySettingsPage) => "library",
            // Detail pages keep the section the user came from (Albums or Artists).
            _ => null
        };

        if (tag is null)
        {
            return;
        }

        _suppressNavSelection = true;
        try
        {
            SelectNavItem(tag);
        }
        finally
        {
            _suppressNavSelection = false;
        }
    }

    private void SelectNavItem(string tag)
    {
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                NavView.SelectedItem = item;
                break;
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNavSelection)
        {
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag, clearBackStack: true);
        }
    }

    private void NavigateTo(string tag, bool clearBackStack = false)
    {
        var search = string.IsNullOrWhiteSpace(_currentSearch) ? null : _currentSearch;
        Type? pageType = tag switch
        {
            "albums" => typeof(AlbumsPage),
            "artists" => typeof(ArtistsPage),
            "tracks" => typeof(TracksPage),
            "library" => typeof(LibrarySettingsPage),
            _ => null
        };

        if (pageType is null)
        {
            return;
        }

        // Top-level pane picks start a fresh stack so Back stays scoped to drill-in pages.
        if (clearBackStack)
        {
            ContentFrame.BackStack.Clear();
            ContentFrame.ForwardStack.Clear();
        }

        object? parameter = tag == "library" ? null : search;
        if (ContentFrame.Content?.GetType() == pageType && !clearBackStack)
        {
            return;
        }

        ContentFrame.Navigate(pageType, parameter);
        if (clearBackStack)
        {
            ContentFrame.BackStack.Clear();
            NavView.IsBackEnabled = false;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ApplySearch(args.QueryText);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplySearch(sender.Text);
        }
    }

    private void ApplySearch(string? query)
    {
        _currentSearch = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        switch (ContentFrame.Content)
        {
            case AlbumsPage albums:
                albums.ApplySearch(_currentSearch);
                break;
            case ArtistsPage artists:
                artists.ApplySearch(_currentSearch);
                break;
            case TracksPage tracks:
                tracks.ApplySearch(_currentSearch);
                break;
            default:
                if (NavView.SelectedItem is NavigationViewItem item && item.Tag is string tag
                    && tag is "albums" or "artists" or "tracks")
                {
                    NavigateTo(tag);
                }
                break;
        }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.GoBack
            || (e.Key == Windows.System.VirtualKey.Left
                && Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
        {
            if (TryGoBack())
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key != Windows.System.VirtualKey.Space)
        {
            return;
        }

        if (SearchBox.FocusState != FocusState.Unfocused)
        {
            return;
        }

        if (FocusManager.GetFocusedElement(RootGrid.XamlRoot) is TextBox)
        {
            return;
        }

        AppServices.Instance.Playback.TogglePlayPause();
        e.Handled = true;
    }
}
