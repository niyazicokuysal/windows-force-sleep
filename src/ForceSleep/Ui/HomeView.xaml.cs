using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ForceSleep.Ui;

internal partial class HomeView : UserControl
{
    private readonly AppController _controller;

    /// <summary>Raised before an action that hands control to another window (countdown, sleep).</summary>
    public event Action? ActionStarted;

    public HomeView(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        DataContext = controller;

        if (!controller.Capabilities.CanHibernate)
            HibernateButton.Visibility = Visibility.Collapsed;

        // Chip labels depend on the language: Tag is minutes, 0 meaning "no limit".
        foreach (var chip in Descendants<Button>(this).Where(b => b.Tag is string))
        {
            var minutes = int.Parse((string)chip.Tag);
            chip.Content = minutes == 0 ? L.UntilStopped : minutes < 60 ? L.Minutes(minutes) : L.Hours(minutes / 60);
        }
    }

    private void OnSleep(object sender, RoutedEventArgs e)
    {
        ActionStarted?.Invoke();
        _ = _controller.SleepAsync(new SleepTrigger.Manual());
    }

    private void OnHibernate(object sender, RoutedEventArgs e)
    {
        ActionStarted?.Invoke();
        _ = _controller.SleepAsync(new SleepTrigger.Manual(Hibernate: true));
    }

    private void OnScreenOff(object sender, RoutedEventArgs e)
    {
        ActionStarted?.Invoke();
        _controller.TurnScreenOff();
    }

    private void OnTimer(object sender, RoutedEventArgs e) =>
        _controller.StartSleepTimer(TimeSpan.FromMinutes(int.Parse((string)((Button)sender).Tag)));

    private void OnCancelTimer(object sender, RoutedEventArgs e) => _controller.CancelSleepTimer();

    private void OnKeepAwake(object sender, RoutedEventArgs e)
    {
        var minutes = int.Parse((string)((Button)sender).Tag);
        _controller.StartKeepAwake(minutes == 0 ? null : TimeSpan.FromMinutes(minutes));
    }

    private void OnStopKeepAwake(object sender, RoutedEventArgs e) => _controller.StopKeepAwake();

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match)
                yield return match;
            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }
}
