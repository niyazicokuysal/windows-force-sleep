using System.Windows;

namespace ForceSleep.Ui;

/// <summary>The quick panel that opens above the tray icon, like Windows 11's quick settings.</summary>
internal partial class FlyoutWindow : Window
{
    private const double ScreenMargin = 12;
    private bool _closing;

    public FlyoutWindow(AppController controller)
    {
        InitializeComponent();
        WindowStyling.RoundCorners(this);

        var home = new HomeView(controller);
        home.ActionStarted += Close;
        Home.Content = home;

        // Sit in the bottom-right corner of the work area, just above the taskbar.
        SizeChanged += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - ScreenMargin;
            Top = area.Bottom - ActualHeight - ScreenMargin;
        };

        // Like other tray flyouts, clicking anywhere else dismisses it.
        Deactivated += (_, _) => Close();
        Closing += (_, _) => _closing = true;
    }

    public new void Close()
    {
        if (!_closing)
            base.Close();
    }

    private void OnWhy(object sender, RoutedEventArgs e) => App.Current.ShowMainWindow(Page.Why);

    private void OnSettings(object sender, RoutedEventArgs e) => App.Current.ShowMainWindow(Page.Settings);

    private void OnOpen(object sender, RoutedEventArgs e) => App.Current.ShowMainWindow(Page.Home);
}
