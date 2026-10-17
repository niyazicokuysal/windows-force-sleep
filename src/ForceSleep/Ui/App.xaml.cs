using System.Windows;
using System.Windows.Threading;

namespace ForceSleep.Ui;

internal enum Page { Home, Why, Settings }

/// <summary>Owns the tray icon and windows, and gives <see cref="AppController"/> its UI.</summary>
internal partial class App : Application, IUserInterface
{
    private readonly Settings _settings;
    private readonly EventWaitHandle _activate;
    private readonly EventWaitHandle _quit;
    private readonly Page? _startPage;
    private readonly List<RegisteredWaitHandle> _waits = [];
    private TrayIcon? _tray;
    private MainWindow? _main;
    private FlyoutWindow? _flyout;
    private bool _toldWhereTheIconIs;
    private DateTime _flyoutClosedAt;

    public App(Settings settings, EventWaitHandle activate, EventWaitHandle quit, Page? startPage)
    {
        _settings = settings;
        _activate = activate;
        _quit = quit;
        _startPage = startPage;
        InitializeComponent();
    }

    public AppController Controller { get; private set; } = null!;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyTheme();
#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("--english"))
            L.Use("en");
#endif

        Controller = new AppController(_settings, this);
        _tray = new TrayIcon(this);

        if (!Controller.CanSleep)
            Notify(L.NoSleepSupport, warning: true);

        if (_startPage is { } page)
            ShowMainWindow(page);

#if DEBUG
        // For looking at the UI during development; never in release builds.
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--dark")) ThemeMode = ThemeMode.Dark;
        if (args.Contains("--light")) ThemeMode = ThemeMode.Light;
        if (args.Contains("--panel")) ToggleFlyout();
        if (args.Contains("--countdown")) _ = CountdownWindow.RunAsync(L.CountdownSleep, L.ReasonAwayMode, TimeSpan.FromMinutes(1));
#endif

        // Starting Force Sleep again (e.g. from the Start menu) opens the running copy's window.
        _waits.Add(ThreadPool.RegisterWaitForSingleObject(_activate,
            (_, _) => Dispatcher.BeginInvoke(() => ShowMainWindow(Page.Home)), null, Timeout.Infinite, false));
        // Lets the installer close us before updating or uninstalling.
        _waits.Add(ThreadPool.RegisterWaitForSingleObject(_quit,
            (_, _) => Dispatcher.BeginInvoke(Quit), null, Timeout.Infinite, true));
    }

    // ── Windows ───────────────────────────────────────────────────────────

    public void ShowMainWindow(Page page)
    {
        _flyout?.Close();
        if (_main is null)
        {
            _main = new MainWindow(Controller);
            _main.Closed += (_, _) =>
            {
                _main = null;
                // Closing the window doesn't quit; say once where Force Sleep went.
                if (!_toldWhereTheIconIs)
                {
                    _toldWhereTheIconIs = true;
                    Notify(L.Running);
                }
            };
        }

        _main.Navigate(page);
        _main.Show();
        if (_main.WindowState == WindowState.Minimized)
            _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    public void ToggleFlyout()
    {
        if (_flyout is not null)
        {
            _flyout.Close();
            return;
        }
        // Clicking the tray icon while the panel is open first closes it (it lost focus);
        // that same click must not open it again.
        if (DateTime.Now - _flyoutClosedAt < TimeSpan.FromMilliseconds(400))
            return;

        _flyout = new FlyoutWindow(Controller);
        _flyout.Closed += (_, _) =>
        {
            _flyout = null;
            _flyoutClosedAt = DateTime.Now;
        };
        _flyout.Show();
        _flyout.Activate();
    }

    // ── Preferences that affect every window ──────────────────────────────

    public void ApplyTheme() => ThemeMode = _settings.Theme switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        _ => ThemeMode.System,
    };

    public void SetLanguage(string? language)
    {
        _settings.Language = language;
        _settings.Save();
        L.Use(language);
        _tray?.ApplyTexts();

        // Windows read their texts when built, so rebuild the open one in the new language.
        if (_main is not null)
        {
            var main = _main;
            _main = null;
            main.Close();
            ShowMainWindow(Page.Settings);
        }
    }

    public void Quit()
    {
        foreach (var wait in _waits)
            wait.Unregister(null);
        _flyout?.Close();
        _main?.Close();
        _tray?.Dispose();
        Controller.Dispose();
        Shutdown();
    }

    // ── IUserInterface ────────────────────────────────────────────────────

    public Task<bool> CountdownAsync(string title, string? reason, TimeSpan duration)
    {
        _flyout?.Close();
        return CountdownWindow.RunAsync(title, reason, duration);
    }

    public void Notify(string message, bool warning = false, bool opensDiagnostics = false) =>
        _tray?.ShowNotification(message, warning, opensDiagnostics);
}
