using Avalonia.Controls;
using Avalonia.Input;
using Dustan.Helpers;

namespace Dustan.Views;

public partial class MainWindow : Window
{
    private string? _currentSearch;
    private bool _suppressNav;
    private readonly Stack<Control> _backStack = new();
    private string _currentTag = "albums";

    public MainWindow()
    {
        InitializeComponent();

        var settings = AppServices.Instance.Settings.Current;
        if (string.IsNullOrWhiteSpace(settings.LibraryPath) || AppServices.Instance.Library.GetTrackCount() == 0)
        {
            NavigateTo("library", clearBackStack: true);
        }
        else
        {
            NavigateTo("albums", clearBackStack: true);
        }
    }

    public void NavigatePage(Control page, bool addToBackStack)
    {
        if (addToBackStack && ContentHost.Content is Control current)
        {
            _backStack.Push(current);
        }

        ContentHost.Content = page;
        BackButton.IsVisible = _backStack.Count > 0;
    }

    public void NavigateTo(string tag, bool clearBackStack = false)
    {
        _currentTag = tag;
        if (clearBackStack)
        {
            _backStack.Clear();
        }

        Control page = tag switch
        {
            "artists" => new ArtistsPage(_currentSearch),
            "tracks" => new TracksPage(_currentSearch),
            "library" => new LibrarySettingsPage(),
            _ => new AlbumsPage(_currentSearch)
        };

        NavigatePage(page, addToBackStack: false);
        SelectNav(tag);
        BackButton.IsVisible = _backStack.Count > 0;
    }

    private void SelectNav(string tag)
    {
        _suppressNav = true;
        try
        {
            foreach (var item in NavList.Items.OfType<ListBoxItem>())
            {
                if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                {
                    NavList.SelectedItem = item;
                    break;
                }
            }
        }
        finally
        {
            _suppressNav = false;
        }
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressNav)
        {
            return;
        }

        if (NavList.SelectedItem is ListBoxItem { Tag: string tag })
        {
            NavigateTo(tag, clearBackStack: true);
        }
    }

    private void BackButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TryGoBack();

    private bool TryGoBack()
    {
        if (_backStack.Count == 0)
        {
            return false;
        }

        ContentHost.Content = _backStack.Pop();
        BackButton.IsVisible = _backStack.Count > 0;
        return true;
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _currentSearch = string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();
        if (ContentHost.Content is ISearchablePage searchable)
        {
            searchable.ApplySearch(_currentSearch);
            return;
        }

        if (_currentTag is "albums" or "artists" or "tracks")
        {
            NavigateTo(_currentTag, clearBackStack: true);
        }
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (TryGoBack())
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key != Key.Space)
        {
            return;
        }

        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        AppServices.Instance.Playback.TogglePlayPause();
        e.Handled = true;
    }
}
