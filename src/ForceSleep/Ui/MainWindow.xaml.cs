using System.Windows;
using System.Windows.Controls;

namespace ForceSleep.Ui;

/// <summary>The full window: Home, "Why didn't it sleep?" and Settings, with navigation on the left.</summary>
internal partial class MainWindow : Window
{
    private readonly AppController _controller;

    public MainWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
    }

    public void Navigate(Page page)
    {
        var item = page switch
        {
            Page.Why => NavWhy,
            Page.Settings => NavSettings,
            _ => NavHome,
        };
        if (item.IsChecked == true)
            ShowPage(page);
        else
            item.IsChecked = true;
    }

    private void OnNavigate(object sender, RoutedEventArgs e) =>
        ShowPage(sender == NavWhy ? Page.Why : sender == NavSettings ? Page.Settings : Page.Home);

    private void ShowPage(Page page)
    {
        // Pages are rebuilt on every visit so they always show fresh information.
        PageHost.Content = page switch
        {
            Page.Why => new DiagnosticsView(_controller),
            Page.Settings => new SettingsView(_controller),
            _ => HomePage(),
        };
        Scroller.ScrollToTop();
    }

    private UIElement HomePage()
    {
        var home = new HomeView(_controller) { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        var page = new StackPanel();
        page.Children.Add(new TextBlock { Text = L.NavHome, Style = (Style)FindResource("TitleText"), Margin = new Thickness(0, 0, 0, 16) });
        page.Children.Add(home);
        return page;
    }
}
