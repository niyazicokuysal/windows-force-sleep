using System.Windows;
using System.Windows.Threading;

namespace ForceSleep.Ui;

/// <summary>"Putting the PC to sleep in 5 seconds" with Cancel and Now.</summary>
internal partial class CountdownWindow : Window
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DateTime _end;
    private readonly TimeSpan _duration;

    private CountdownWindow(string title, string? reason, TimeSpan duration)
    {
        InitializeComponent();
        WindowStyling.RoundCorners(this);
        TitleText.Text = title;
        ReasonText.Text = reason ?? "";
        ReasonText.Visibility = reason is null ? Visibility.Collapsed : Visibility.Visible;
        _duration = duration;
        _end = DateTime.Now + duration;

        _timer.Tick += (_, _) => Update();
        Update();
        _timer.Start();
        Closed += (_, _) => Finish(false);
    }

    public static Task<bool> RunAsync(string title, string? reason, TimeSpan duration)
    {
        var window = new CountdownWindow(title, reason, duration);
        window.Show();
        window.Activate();
        return window._result.Task;
    }

    private void Update()
    {
        var left = _end - DateTime.Now;
        if (left <= TimeSpan.Zero)
        {
            Finish(true);
            return;
        }
        SecondsText.Text = L.CountdownIn((int)Math.Ceiling(left.TotalSeconds));
        Progress.Value = 1 - left / _duration;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Finish(false);

    private void OnNow(object sender, RoutedEventArgs e) => Finish(true);

    private void Finish(bool proceed)
    {
        if (!_result.TrySetResult(proceed))
            return;
        _timer.Stop();
        Close();
    }
}
