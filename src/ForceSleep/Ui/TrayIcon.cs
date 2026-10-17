using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Forms = System.Windows.Forms;

namespace ForceSleep.Ui;

/// <summary>
/// The moon in the notification area. Left click opens the quick panel, right click a short
/// menu. WPF has no tray icon, so this is the Windows Forms one.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly App _app;
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _idle;
    private readonly System.Drawing.Icon _busy;
    private bool _notificationOpensDiagnostics;

    public TrayIcon(App app)
    {
        _app = app;
        var size = Forms.SystemInformation.SmallIconSize.Width;
        _idle = MoonIcon.Create(size, MoonIcon.Gold);
        _busy = MoonIcon.Create(size, MoonIcon.Gray);

        _icon = new Forms.NotifyIcon { Icon = _idle, Visible = true };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                _app.ToggleFlyout();
            else if (e.Button == Forms.MouseButtons.Right)
                ShowMenu();
        };
        _icon.BalloonTipClicked += (_, _) =>
        {
            if (_notificationOpensDiagnostics)
                _app.ShowMainWindow(Page.Why);
        };

        app.Controller.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppController.Busy) or nameof(AppController.StatusTitle))
                UpdateState();
        };
        ApplyTexts();
    }

    public void ApplyTexts() => UpdateState();

    private void UpdateState()
    {
        var controller = _app.Controller;
        _icon.Icon = controller.Busy ? _busy : _idle;
        var text = $"Force Sleep – {controller.StatusTitle}";
        _icon.Text = text.Length > 127 ? text[..127] : text;
    }

    public void ShowNotification(string message, bool warning, bool opensDiagnostics)
    {
        _notificationOpensDiagnostics = opensDiagnostics;
        _icon.ShowBalloonTip(5000, "Force Sleep", message, warning ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item(L.SleepNow, "", () => _ = _app.Controller.SleepAsync(new SleepTrigger.Manual())));
        menu.Items.Add(Item(L.OpenApp, "", () => _app.ShowMainWindow(Page.Home)));
        menu.Items.Add(Item(L.WhyDidntItSleep, "", () => _app.ShowMainWindow(Page.Why)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(L.Exit, "", _app.Quit));

        // A context menu only closes on an outside click if our app is in the foreground, so
        // open it from an invisible window that we activate first.
        var anchor = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            Width = 0,
            Height = 0,
            Topmost = true,
        };
        anchor.Show();
        anchor.Activate();
        menu.Placement = PlacementMode.MousePoint;
        menu.Closed += (_, _) => anchor.Close();
        menu.IsOpen = true;
    }

    private static MenuItem Item(string text, string glyph, Action action)
    {
        var item = new MenuItem
        {
            Header = text,
            Icon = new TextBlock { Text = glyph, Style = (Style)Application.Current.Resources["Icon"] },
        };
        item.Click += (_, _) => action();
        return item;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _idle.Dispose();
        _busy.Dispose();
    }
}
