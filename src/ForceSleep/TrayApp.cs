using System.Reflection;

namespace ForceSleep;

internal sealed class TrayApp : ApplicationContext
{
    private const string AppName = "Force Sleep";

    // Waking this soon after a sleep was most likely not intended, so say so.
    private static readonly TimeSpan ShortSleepNotice = TimeSpan.FromSeconds(30);

    private readonly Settings _settings;
    private readonly SleepController _controller = new(PowerCapabilities.Query());
    private readonly NotifyIcon _tray;
    private readonly Icon _idleIcon;
    private readonly Icon _busyIcon;
    private readonly ToolStripMenuItem _sleepItem;
    private readonly ToolStripMenuItem _hibernateItem;
    private readonly ToolStripMenuItem _fallbackItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _whyItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly ToolStripMenuItem _languageAutoItem;
    private readonly ToolStripMenuItem _languageTurkishItem;
    private readonly ToolStripMenuItem _languageEnglishItem;
    private readonly Control _uiInvoker = new();
    private readonly RegisteredWaitHandle _activationWait;
    private readonly RegisteredWaitHandle _quitWait;
    private DiagnosticsForm? _diagnostics;
    private bool _balloonOpensDiagnostics;
    private bool _busy;

    public TrayApp(Settings settings, EventWaitHandle activate, EventWaitHandle quit, bool quietStart)
    {
        _settings = settings;

        // Force the handle so other threads can BeginInvoke onto the UI thread.
        _ = _uiInvoker.Handle;

        var caps = _controller.Capabilities;
        var iconSize = SystemInformation.SmallIconSize.Width;
        _idleIcon = MoonIcon.Create(iconSize, MoonIcon.Gold);
        _busyIcon = MoonIcon.Create(iconSize, MoonIcon.Gray);

        // Texts are set in ApplyTexts so they can follow a language change.
        _sleepItem = new ToolStripMenuItem("", null, (_, _) => RunAsync(hibernate: false))
        {
            Font = new Font(SystemFonts.MenuFont ?? Control.DefaultFont, FontStyle.Bold),
            Enabled = _controller.CanDoAnything,
        };
        _hibernateItem = new ToolStripMenuItem("", null, (_, _) => RunAsync(hibernate: true))
        {
            Enabled = caps.CanHibernate,
        };
        _whyItem = new ToolStripMenuItem("", null, (_, _) => ShowDiagnostics());
        _fallbackItem = new ToolStripMenuItem("", null, OnFallbackToggled)
        {
            Checked = _settings.HibernateFallback && caps.CanHibernate,
            Enabled = caps.CanHibernate && caps.CanSleep && !caps.ModernStandby,
        };
        _autoStartItem = new ToolStripMenuItem("", null, OnAutoStartToggled)
        {
            Checked = AutoStart.IsEnabled,
        };
        _languageAutoItem = new ToolStripMenuItem("", null, (_, _) => SetLanguage(null));
        _languageTurkishItem = new ToolStripMenuItem("Türkçe", null, (_, _) => SetLanguage("tr"));
        _languageEnglishItem = new ToolStripMenuItem("English", null, (_, _) => SetLanguage("en"));
        _languageItem = new ToolStripMenuItem("", null, [_languageAutoItem, _languageTurkishItem, _languageEnglishItem]);
        _exitItem = new ToolStripMenuItem("", null, (_, _) => ExitThread());

        var menu = new ContextMenuStrip { ShowItemToolTips = true };
        menu.Items.AddRange(
        [
            _sleepItem,
            _hibernateItem,
            _whyItem,
            new ToolStripSeparator(),
            _fallbackItem,
            _autoStartItem,
            _languageItem,
            new ToolStripSeparator(),
            _exitItem,
        ]);

        _tray = new NotifyIcon
        {
            Icon = _idleIcon,
            ContextMenuStrip = menu,
            Visible = true,
        };
        ApplyTexts();
        _tray.MouseUp += OnTrayMouseUp;
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_balloonOpensDiagnostics)
                ShowDiagnostics();
        };

        Log.Write($"Started {Program.Version}. {caps}");

        if (!_controller.CanDoAnything)
            Notify(L.NoSleepSupport, ToolTipIcon.Warning);
        else if (!quietStart)
            Notify(L.Running);

        _activationWait = ThreadPool.RegisterWaitForSingleObject(activate,
            (_, _) => _uiInvoker.BeginInvoke(() => Notify(L.AlreadyRunning)),
            null, Timeout.Infinite, executeOnlyOnce: false);

        // Lets the installer close us before updating or uninstalling.
        _quitWait = ThreadPool.RegisterWaitForSingleObject(quit,
            (_, _) => _uiInvoker.BeginInvoke(ExitThread),
            null, Timeout.Infinite, executeOnlyOnce: true);
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        // Open the menu on left click too; non-technical users rarely think to right-click.
        // ShowContextMenu is the private method NotifyIcon itself uses, which handles focus correctly.
        if (e.Button == MouseButtons.Left)
            typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(_tray, null);
    }

    private async void RunAsync(bool hibernate)
    {
        if (_busy)
            return;

        SetBusy(true);
        try
        {
            var fallback = _settings.HibernateFallback && _controller.Capabilities.CanHibernate;
            var result = hibernate
                ? await _controller.HibernateAsync()
                : await _controller.SleepAsync(fallback);
            Report(result, requestedHibernate: hibernate);
        }
        catch (Exception e)
        {
            Log.Write($"Unexpected error: {e}");
            Notify(L.UnexpectedError, ToolTipIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Report(SleepResult result, bool requestedHibernate)
    {
        switch (result)
        {
            case { Status: SleepStatus.Slept, Method: SleepMethod.Forced }:
                Notify(L.ForcedSleep, diagnostics: true);
                break;
            case { Status: SleepStatus.Slept, Method: SleepMethod.Hibernate } when !requestedHibernate:
                Notify(L.HibernatedInstead, diagnostics: true);
                break;
            case { Status: SleepStatus.Slept } when result.TimeAsleep < ShortSleepNotice:
                Notify(L.WokeAfter(result.TimeAsleep), diagnostics: true);
                break;
            case { Status: SleepStatus.Cancelled }:
                Notify(L.Cancelled);
                break;
            case { Status: SleepStatus.Failed }:
                Notify(L.SleepFailed(suggestHibernate: _controller.Capabilities.CanHibernate && !_settings.HibernateFallback),
                    ToolTipIcon.Warning, diagnostics: true);
                break;
        }
    }

    private void ShowDiagnostics()
    {
        if (_diagnostics is { IsDisposed: false })
        {
            _diagnostics.WindowState = FormWindowState.Normal;
            _diagnostics.Activate();
            return;
        }

        _diagnostics = new DiagnosticsForm(_idleIcon, _settings);
        _diagnostics.Show();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _sleepItem.Enabled = !busy && _controller.CanDoAnything;
        _hibernateItem.Enabled = !busy && _controller.Capabilities.CanHibernate;
        _tray.Icon = busy ? _busyIcon : _idleIcon;
        _tray.Text = $"{AppName} – {(busy ? L.TooltipBusy : L.TooltipIdle)}";
    }

    private void ApplyTexts()
    {
        _sleepItem.Text = L.SleepNow;
        _hibernateItem.Text = L.Hibernate;
        _hibernateItem.ToolTipText = _controller.Capabilities.CanHibernate ? null : L.HibernateUnavailable;
        _whyItem.Text = L.WhyDidntItSleep;
        _fallbackItem.Text = L.HibernateFallback;
        _autoStartItem.Text = L.StartWithWindows;
        _languageItem.Text = L.LanguageMenu;
        _languageAutoItem.Text = L.LanguageAuto;
        _languageAutoItem.Checked = _settings.Language is null;
        _languageTurkishItem.Checked = _settings.Language == "tr";
        _languageEnglishItem.Checked = _settings.Language == "en";
        _exitItem.Text = L.Exit;
        _tray.Text = $"{AppName} – {(_busy ? L.TooltipBusy : L.TooltipIdle)}";
    }

    private void SetLanguage(string? language)
    {
        _settings.Language = language;
        _settings.Save();
        L.Use(language);
        ApplyTexts();

        // The window is built from texts at creation time, so let it reopen in the new language.
        _diagnostics?.Close();
    }

    private void OnFallbackToggled(object? sender, EventArgs e)
    {
        _fallbackItem.Checked = !_fallbackItem.Checked;
        _settings.HibernateFallback = _fallbackItem.Checked;
        _settings.Save();
    }

    private void OnAutoStartToggled(object? sender, EventArgs e)
    {
        try
        {
            AutoStart.Set(!_autoStartItem.Checked);
            _autoStartItem.Checked = AutoStart.IsEnabled;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write($"Could not change auto start: {ex.Message}");
            Notify(L.AutoStartFailed, ToolTipIcon.Warning);
        }
    }

    private void Notify(string message, ToolTipIcon icon = ToolTipIcon.Info, bool diagnostics = false)
    {
        _balloonOpensDiagnostics = diagnostics;
        _tray.ShowBalloonTip(5000, AppName, message, icon);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _activationWait.Unregister(null);
            _quitWait.Unregister(null);
            _diagnostics?.Dispose();
            _uiInvoker.Dispose();
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _idleIcon.Dispose();
            _busyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
